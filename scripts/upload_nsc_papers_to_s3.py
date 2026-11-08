#!/usr/bin/env python3
"""Upload the downloaded NSC / SC past exam papers to MinIO (S3-compatible).

Uploads every file under ``data/nsc/exam_papers/`` to the storage bucket,
preserving the on-disk layout as the object key:

    exam_papers/<session>/<subject>/<paper or memo>.<ext>

Object keys therefore mirror the folder structure produced by
``scripts/download_nsc_exam_papers.py``.

Requirements: ``pip install boto3 Pillow`` and Poppler (``pdftoppm``).
Re-running also backfills missing thumbnails without re-uploading existing PDFs.

Configuration (environment variables):

    FILE_STORAGE_ENDPOINT   S3 API endpoint host, e.g. http://localhost:9000
    FILE_STORAGE_ACCESS_KEY Access key / MinIO root user
    FILE_STORAGE_SECRET_KEY Secret key / MinIO root password
    FILE_STORAGE_BUCKET     Bucket name (default: matric-dasbhoard-files)
    FILE_STORAGE_USE_SSL    "true"/"1" to use HTTPS (default: false)

Usage:
    export FILE_STORAGE_ENDPOINT=http://localhost:9000
    export FILE_STORAGE_ACCESS_KEY=$MINIO_ROOT_USER
    export FILE_STORAGE_SECRET_KEY=$MINIO_ROOT_PASSWORD
    python3 scripts/upload_nsc_papers_to_s3.py            # upload
    python3 scripts/upload_nsc_papers_to_s3.py --dry-run  # list what would upload
    python3 scripts/upload_nsc_papers_to_s3.py --workers 16
"""

from __future__ import annotations

import argparse
import os
import sys
import shutil
import subprocess
import tempfile
from io import BytesIO
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

DEFAULT_SOURCE_DIR = "data/nsc/exam_papers"
DEFAULT_BUCKET = "matric-dasbhoard-files"
# S3 uploads through the root/minio API are capped at one part this big unless
# multipart uploads are used; 11 MB is safely below that for these PDFs.
_MAX_BODY = 100 << 20


def env_str(name: str, default: str = "") -> str:
    value = os.environ.get(name, default)
    return value.strip() if value else ""


def env_bool(name: str, default: bool = False) -> bool:
    value = env_str(name)
    if not value:
        return default
    return value.lower() in {"1", "true", "yes", "on"}


def session_and_subject(rel_path: Path) -> tuple[str, str]:
    """Return (session, subject) relative to the source root.

    Layout is <root>/<session>/<subject>/<file>. We split off the filename and
    keep only the first two path components so deep or nested names still map
    to a stable prefix.
    """
    parts = list(rel_path.parts)
    # parts = [session, subject, file, ...]
    session = parts[0] if len(parts) > 0 else "Uncategorised"
    subject = parts[1] if len(parts) > 1 else "Uncategorised"
    return session, subject


def build_upload_list(source_dir: Path) -> list[tuple[Path, str]]:
    """Return [(local_path, object_key)] for every file under source_dir.

    We skip the manifest/README so only actual exam-paper files are uploaded.
    """
    uploads: list[tuple[Path, str]] = []
    skip_names = {"__manifest__.jsonl", "README.md"}
    for path in sorted(source_dir.rglob("*")):
        if not path.is_file() or path.name in skip_names:
            continue
        rel = path.relative_to(source_dir)
        session, subject = session_and_subject(rel)
        key = f"exam_papers/{session}/{subject}/{path.name}"
        uploads.append((path, key))
    return uploads


def chunked_upload(client, bucket: str, key: str, path: Path) -> None:
    """Upload a file, using multipart if it exceeds the single-put limit."""
    size = path.stat().st_size
    if size <= _MAX_BODY:
        client.upload_file(
            str(path), bucket, key,
            ExtraArgs={"ContentType": "application/pdf"},
        )
    else:
        # For any unexpectedly large files (e.g. bundled zips) fall back to a
        # multipart upload so we stay under the S3 single-part limit.
        transfer = client.get_transfer_config(
            multipart_threshold=_MAX_BODY,
            max_concurrency=4,
        )
        client.upload_file(
            str(path), bucket, key,
            Config=transfer,
            ExtraArgs={"ContentType": "application/pdf"},
        )


def generate_thumbnail(path: Path) -> bytes:
    """Render only page one, bounded to 480 pixels, as a WebP image."""
    from PIL import Image

    with tempfile.TemporaryDirectory(prefix="paper-thumbnail-") as directory:
        prefix = str(Path(directory) / "cover")
        subprocess.run(
            ["pdftoppm", "-f", "1", "-singlefile", "-scale-to", "480",
             "-png", str(path), prefix],
            check=True, capture_output=True, timeout=60,
        )
        with Image.open(prefix + ".png") as image:
            output = BytesIO()
            image.convert("RGB").save(output, format="WEBP", quality=82)
            return output.getvalue()


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source-dir",
        default=DEFAULT_SOURCE_DIR,
        help="Root of the downloaded exam papers (default: %(default)s)",
    )
    parser.add_argument(
        "--bucket",
        default=None,
        help="Override FILE_STORAGE_BUCKET",
    )
    parser.add_argument(
        "--workers",
        type=int,
        default=8,
        help="Concurrent uploads (default: %(default)s)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Only list files that would be uploaded; do not contact S3",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Re-upload objects that already exist in the bucket",
    )
    parser.add_argument(
        "--thumbnails-only", action="store_true",
        help="Backfill thumbnails for locally available PDFs already in storage",
    )
    args = parser.parse_args()

    endpoint = env_str("FILE_STORAGE_ENDPOINT")
    access_key = env_str("FILE_STORAGE_ACCESS_KEY")
    secret_key = env_str("FILE_STORAGE_SECRET_KEY")
    bucket = args.bucket or env_str("FILE_STORAGE_BUCKET", DEFAULT_BUCKET)
    use_ssl = env_bool("FILE_STORAGE_USE_SSL")

    source_dir = Path(args.source_dir).resolve()
    if not source_dir.is_dir():
        print(f"ERROR: source directory not found: {source_dir}", file=sys.stderr)
        return 1

    uploads = build_upload_list(source_dir)
    print(f"Found {len(uploads)} files under {source_dir}")

    if args.dry_run:
        for _, key in uploads:
            print(f"  would upload: {key}")
        return 0

    if not (endpoint and access_key and secret_key):
        print(
            "ERROR: FILE_STORAGE_ENDPOINT, FILE_STORAGE_ACCESS_KEY and "
            "FILE_STORAGE_SECRET_KEY must be set (see usage).",
            file=sys.stderr,
        )
        return 1

    try:
        import boto3
        from botocore.config import Config
        from botocore.exceptions import BotoCoreError, ClientError
    except ImportError:
        print("ERROR: boto3 is required. Install with: pip install boto3", file=sys.stderr)
        return 1

    try:
        from PIL import Image
    except ImportError:
        print("ERROR: Pillow is required. Install with: pip install Pillow", file=sys.stderr)
        return 1
    if not shutil.which("pdftoppm"):
        print("ERROR: Poppler (pdftoppm) is required to render PDF thumbnails.", file=sys.stderr)
        return 1

    client = boto3.client(
        "s3",
        endpoint_url=endpoint,
        aws_access_key_id=access_key,
        aws_secret_access_key=secret_key,
        use_ssl=use_ssl,
        config=Config(
            s3={"addressing_style": "path"},
            signature_version="s3v4",
            max_pool_connections=args.workers * 2,
            # Older S3Mock builds add CRC64/x-amz-checksum headers on multipart
            # uploads and reject the newer checksums boto3 adds for large bodies.
            # "when_required" only sends checksums for endpoints that demand them.
            request_checksum_calculation="when_required",
            response_checksum_validation="when_required",
        ),
    )

    # Verify the bucket exists (auto-create for convenience in dev).
    try:
        client.head_bucket(Bucket=bucket)
        print(f"Bucket ready: {bucket}")
    except ClientError as error:
        code = error.response.get("Error", {}).get("Code", "")
        if code in {"404", "NoSuchBucket"}:
            try:
                client.create_bucket(Bucket=bucket)
                print(f"Created bucket: {bucket}")
            except ClientError as create_error:
                print(f"ERROR: could not create bucket '{bucket}': "
                      f"{create_error}", file=sys.stderr)
                return 1
        elif code in {"403", "AccessDenied"}:
            print(f"ERROR: access denied to bucket '{bucket}'. Check credentials "
                  f"and that the endpoint is reachable: {endpoint}", file=sys.stderr)
            return 1
        else:
            print(f"ERROR: could not reach bucket '{bucket}': {error}", file=sys.stderr)
            return 1

    # Pre-fetch existing objects so we can skip already-uploaded files.
    existing: set[str] = set()
    if not args.force or args.thumbnails_only:
        paginator = client.get_paginator("list_objects_v2")
        try:
            for page in paginator.paginate(Bucket=bucket):
                for obj in page.get("Contents", []):
                    existing.add(obj["Key"])
        except (BotoCoreError, ClientError) as error:
            print(f"WARNING: could not list existing objects: {error}", file=sys.stderr)
        print(f"Existing objects in bucket: {len(existing)}")

    if args.thumbnails_only:
        uploads = [u for u in uploads if u[0].suffix.lower() == ".pdf" and u[1] in existing]

    remaining = [
        u for u in uploads
        if args.force or u[1] not in existing
        or (u[0].suffix.lower() == ".pdf" and f"thumbnails/{u[1]}.webp" not in existing)
    ]
    skipped = len(uploads) - len(remaining)
    print(f"To upload: {len(remaining)} (skipping {skipped} already present)")

    counts = {"uploaded": 0, "failed": 0}
    failures: list[str] = []

    def do_upload(item: tuple[Path, str]) -> tuple[str, str]:
        path, key = item
        try:
            if not args.thumbnails_only and (args.force or key not in existing):
                chunked_upload(client, bucket, key, path)
            thumbnail_key = f"thumbnails/{key}.webp"
            if path.suffix.lower() == ".pdf" and (args.force or thumbnail_key not in existing):
                client.put_object(
                    Bucket=bucket, Key=thumbnail_key,
                    Body=generate_thumbnail(path), ContentType="image/webp",
                    CacheControl="public, max-age=86400",
                )
            return "uploaded", key
        except (BotoCoreError, ClientError, OSError, subprocess.SubprocessError, ImportError, ValueError) as error:
            return "failed", f"{path}: {error}"

    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(do_upload, item) for item in remaining]
        for future in as_completed(futures):
            status, detail = future.result()
            counts[status] += 1
            if status == "failed":
                failures.append(detail)
                print(f"FAILED  {detail}", file=sys.stderr)
            elif counts[status] % 50 == 0:
                print(f"  [{counts['uploaded']}/{len(remaining)}] uploaded")

    print(f"\nDone: {counts['uploaded']} uploaded, {skipped} skipped, "
          f"{counts['failed']} failed")
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    return 1 if counts["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())