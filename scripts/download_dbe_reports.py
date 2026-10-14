#!/usr/bin/env python3
"""Download every document listed on the DBE Reports page.

The page (https://www.education.gov.za/Resources/Reports.aspx) lists documents
grouped into sections. Each row carries a title link and a forced-download link
that both point at `/LinkClick.aspx?fileticket=...`. This script scrapes those
rows and saves each document under `data/reports/<section>/`.

Filenames come from the server's Content-Disposition header when present,
otherwise the title is used with an extension inferred from Content-Type.

Usage:
    python3 scripts/download_dbe_reports.py [--out-dir data/reports] [--workers 8]
"""

import argparse
import hashlib
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed

PAGE_URL = "https://www.education.gov.za/Resources/Reports.aspx"
BASE_URL = "https://www.education.gov.za"
USER_AGENT = (
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/120.0 Safari/537.36"
)

SECTION_RE = re.compile(
    r'eds_containerTitle">(?P<section>[^<]+)</span>', re.IGNORECASE
)
ROW_RE = re.compile(
    r'class="TitleCell">\s*<a[^>]*href="(?P<href>[^"]+)"[^>]*>(?P<title>.*?)</a>',
    re.IGNORECASE | re.DOTALL,
)

CONTENT_TYPE_EXT = {
    "application/pdf": ".pdf",
    "application/msword": ".doc",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document": ".docx",
    "application/vnd.ms-excel": ".xls",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet": ".xlsx",
    "application/vnd.ms-powerpoint": ".ppt",
    "application/vnd.openxmlformats-officedocument.presentationml.presentation": ".pptx",
    "application/zip": ".zip",
    "text/plain": ".txt",
}

TAG_RE = re.compile(r"<[^>]+>")
WS_RE = re.compile(r"\s+")
ILLEGAL_RE = re.compile(r'[<>:"/\\|?*\x00-\x1f]')


def fetch(url: str) -> bytes:
    """Fetch a URL with a browser User-Agent; the site 403s default agents."""
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def clean_text(value: str) -> str:
    text = urllib.parse.unquote(value)
    text = TAG_RE.sub("", text)
    text = (
        text.replace("&amp;", "&")
        .replace("&nbsp;", " ")
        .replace("&#39;", "'")
        .replace("&quot;", '"')
    )
    return WS_RE.sub(" ", text).strip()


def safe_name(value: str, max_length: int = 150) -> str:
    name = ILLEGAL_RE.sub("_", value).strip(" .")
    name = WS_RE.sub(" ", name)
    return name[:max_length].strip()


def parse_page(html: str) -> list[dict]:
    """Return [{section, title, download_url}] for every listed document."""
    sections = [
        (match.start(), clean_text(match.group("section")))
        for match in SECTION_RE.finditer(html)
    ]
    documents: list[dict] = []
    seen_tickets: set[str] = set()

    def section_for(position: int) -> str:
        current = "Uncategorised"
        for start, name in sections:
            if start <= position:
                current = name
            else:
                break
        return current

    for match in ROW_RE.finditer(html):
        href = match.group("href").replace("&amp;", "&")
        if "LinkClick.aspx" not in href:
            continue
        title = clean_text(match.group("title"))
        if not title or title.lower() == "download":
            continue
        parts = urllib.parse.urlparse(href)
        query = dict(urllib.parse.parse_qsl(parts.query))
        ticket = query.get("fileticket", "")
        if not ticket or ticket in seen_tickets:
            continue
        seen_tickets.add(ticket)
        query["forcedownload"] = "true"
        download_url = f"{BASE_URL}{parts.path}?{urllib.parse.urlencode(query)}"
        documents.append(
            {
                "section": section_for(match.start()),
                "title": title,
                "download_url": download_url,
            }
        )
    return documents


def filename_from_response(doc: dict, response) -> str:
    disposition = response.headers.get("Content-Disposition", "")
    match = re.search(r"filename\*?=(?:UTF-8''|\")?([^\";]+)", disposition, re.IGNORECASE)
    if match:
        name = clean_text(match.group(1))
        if name:
            return safe_name(name)

    content_type = (response.headers.get("Content-Type") or "").split(";")[0].strip()
    extension = CONTENT_TYPE_EXT.get(content_type, "")
    if not extension:
        extension = os.path.splitext(urllib.parse.urlparse(doc["download_url"]).path)[1]
    base = safe_name(doc["title"]) or hashlib.sha1(
        doc["download_url"].encode()
    ).hexdigest()[:12]
    return f"{base}{extension or '.bin'}"


def download(doc: dict, out_dir: str) -> tuple[str, str]:
    """Download one document; return (status, path-or-message)."""
    section_dir = os.path.join(out_dir, safe_name(doc["section"]) or "Uncategorised")
    os.makedirs(section_dir, exist_ok=True)

    request = urllib.request.Request(
        doc["download_url"], headers={"User-Agent": USER_AGENT}
    )
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            name = filename_from_response(doc, response)
            path = os.path.join(section_dir, name)
            if os.path.exists(path) and os.path.getsize(path) > 0:
                return "skipped", path
            with open(path, "wb") as handle:
                while chunk := response.read(1 << 16):
                    handle.write(chunk)
            return "downloaded", path
    except (urllib.error.URLError, urllib.error.HTTPError, OSError) as error:
        return "failed", f"{doc['title']} ({doc['download_url']}): {error}"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out-dir", default="data/reports")
    parser.add_argument("--workers", type=int, default=8)
    parser.add_argument("--page", default=PAGE_URL, help="Reports page URL")
    args = parser.parse_args()

    print(f"Fetching index: {args.page}")
    documents = parse_page(fetch(args.page).decode("utf-8", "replace"))
    print(f"Found {len(documents)} documents across "
          f"{len({d['section'] for d in documents})} sections")

    counts = {"downloaded": 0, "skipped": 0, "failed": 0}
    failures: list[str] = []
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = {pool.submit(download, doc, args.out_dir): doc for doc in documents}
        for future in as_completed(futures):
            status, detail = future.result()
            counts[status] += 1
            if status == "failed":
                failures.append(detail)
                print(f"FAILED  {detail}", file=sys.stderr)
            else:
                print(f"{status.upper():<10} {detail}")

    print(
        f"\nDone: {counts['downloaded']} downloaded, "
        f"{counts['skipped']} skipped, {counts['failed']} failed"
    )
    for failure in failures:
        print(f"  - {failure}", file=sys.stderr)
    return 1 if counts["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
