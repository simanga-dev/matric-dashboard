#!/usr/bin/env python3
"""Download every NSC examination paper listed on the DBE past-papers index.

The index page
(https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)Examinations/NSCPastExaminationpapers.aspx)
lists many session pages (e.g. "2025 November NSC Examination Papers",
"2024 May/June NSC/SC Exam Papers", exemplars, Grade 10/11 common papers, ...).
Each session page renders one "Documents" module per subject; every row has a
Title link and a forced-download link, both pointing at `/LinkClick.aspx?fileticket=...`.

This script crawls the index, then each session page, extracting
(session, subject, title, download_url) and saves every PDF under
`data/nsc/exam_papers/<session>/<subject>/`.

Filenames come from the server's Content-Disposition header when present,
otherwise a name derived from the title.

Usage:
    python3 scripts/download_nsc_exam_papers.py [--out-dir data/nsc/exam_papers] [--workers 8]
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

INDEX_URL = (
    "https://www.education.gov.za/Curriculum/NationalSeniorCertificate(NSC)"
    "Examinations/NSCPastExaminationpapers.aspx"
)
BASE_URL = "https://www.education.gov.za"
USER_AGENT = (
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/120.0 Safari/537.36"
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

# Index: session links are <a ...>Session label</a> in the Links module.
SESSION_LINK_RE = re.compile(
    r'<a[^>]*href="(?P<href>[^"]+)"[^>]*>(?P<label>.*?)</a>', re.IGNORECASE | re.DOTALL
)

# Session page: each real subject module is "DNN_Documents" and the subject name is
# the preceding container title inside the same DnnModule block. Heading modules
# ("LANGUAGES", "NON LANGUAGES") are HTML modules with no documents grid, so we
# anchor on the DNN_Documents module boundary to avoid over-matching.
MODULE_RE = re.compile(
    r'DnnModule DnnModule-DNN_Documents(?:[^>]*)>.*?'
    r'eds_containerTitle">(?P<subject>[^<]+)</span>.*?'
    r'class="DNN_Documents">(?P<body>.*?)(?=DnnModule DnnModule-DNN_'
    r'|</div><!-- End_Module_)',
    re.IGNORECASE | re.DOTALL,
)
ROW_RE = re.compile(
    r"ctlTitle_\d+\"[^>]*href=\"(?P<title_href>[^\"]+)\"[^>]*>"
    r"(?P<title>.*?)</a>.*?"
    r"ctlDownloadLink_\d+\"[^>]*href=\"(?P<download_href>[^\"]+)\"",
    re.IGNORECASE | re.DOTALL,
)


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


def strip_suffix(url: str) -> str:
    return url.strip()


def resolve(url: str) -> str:
    """Resolve a possibly-relative URL against BASE_URL."""
    if url.lower().startswith("http://") or url.lower().startswith("https://"):
        return url
    return urllib.parse.urljoin(BASE_URL, url)


_YEAR_RE = re.compile(r"(19|20)\d\d")
_EXCLUDE = re.compile(r"pastexampaper", re.IGNORECASE)


def session_links(html: str) -> list[tuple[str, str]]:
    """Return [(label, url)] for session pages found on the index page.

    The index mixes navigation/footer links with the real "Links" module that
    lists each exam session. We keep only labels that carry a four-digit year
    (every exam-session entry does) and drop anything that looks like a menu.
    """
    links: list[tuple[str, str]] = []
    seen: set[str] = set()
    for match in SESSION_LINK_RE.finditer(html):
        href = strip_suffix(match.group("href").replace("&amp;", "&"))
        label = clean_text(match.group("label"))
        if not label or label.lower() in {"download", "home"}:
            continue
        if not href or not _YEAR_RE.search(label):
            continue
        if _EXCLUDE.search(label):
            continue
        url = resolve(href)
        if url in seen:
            continue
        seen.add(url)
        links.append((label, url))
    return links


def parse_session_page(html: str) -> list[dict]:
    """Return [{'subject', 'title', 'download_url'}] for a session page."""
    documents: list[dict] = []
    seen_tickets: set[str] = set()

    for container in MODULE_RE.finditer(html):
        subject = clean_text(container.group("subject"))
        if not subject:
            continue
        body = container.group("body")
        for row in ROW_RE.finditer(body):
            title_href = row.group("title_href").replace("&amp;", "&")
            if "LinkClick.aspx" not in title_href:
                continue
            title = clean_text(row.group("title"))
            if not title or title.lower() == "download":
                continue
            parts = urllib.parse.urlparse(title_href)
            query = dict(urllib.parse.parse_qsl(parts.query))
            ticket = query.get("fileticket", "")
            if not ticket or ticket in seen_tickets:
                continue
            seen_tickets.add(ticket)
            query["forcedownload"] = "true"
            download_url = f"{BASE_URL}{parts.path}?{urllib.parse.urlencode(query)}"
            documents.append(
                {"subject": subject, "title": title, "download_url": download_url}
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
    session_dir = os.path.join(out_dir, safe_name(doc["session"]) or "Uncategorised")
    subject_dir = os.path.join(session_dir, safe_name(doc["subject"]) or "Uncategorised")
    os.makedirs(subject_dir, exist_ok=True)

    request = urllib.request.Request(
        doc["download_url"], headers={"User-Agent": USER_AGENT}
    )
    try:
        with urllib.request.urlopen(request, timeout=300) as response:
            name = filename_from_response(doc, response)
            path = os.path.join(subject_dir, name)
            if os.path.exists(path) and os.path.getsize(path) > 0:
                return "skipped", path
            with open(path, "wb") as handle:
                while chunk := response.read(1 << 16):
                    handle.write(chunk)
            return "downloaded", path
    except (urllib.error.URLError, urllib.error.HTTPError, OSError) as error:
        return "failed", f"{doc['session']}/{doc['subject']} {doc['title']}: {error}"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out-dir", default="data/nsc/exam_papers")
    parser.add_argument("--workers", type=int, default=8)
    parser.add_argument("--index", default=INDEX_URL, help="NSC past-papers index URL")
    parser.add_argument(
        "--sessions",
        default=None,
        help="Comma-separated session page URLs to process (default: all on index)",
    )
    parser.add_argument(
        "--discover",
        action="store_true",
        help="Only list documents per session, do not download anything",
    )
    args = parser.parse_args()

    print(f"Fetching index: {args.index}")
    try:
        index_html = fetch(args.index).decode("utf-8", "replace")
    except (urllib.error.URLError, urllib.error.HTTPError) as error:
        print(f"ERROR fetching index: {error}", file=sys.stderr)
        return 1

    if args.sessions:
        sessions: list[tuple[str, str]] = []
        for raw in [s for s in args.sessions.split(",") if s.strip()]:
            sessions.append((raw.strip(), raw.strip()))
    else:
        sessions = session_links(index_html)

    print(f"Found {len(sessions)} session pages")
    for label, url in sessions:
        print(f"  - {label}: {url}")

    # Discover documents across all session pages.
    all_docs: list[dict] = []
    for label, url in sessions:
        try:
            html = fetch(url).decode("utf-8", "replace")
        except (urllib.error.URLError, urllib.error.HTTPError) as error:
            print(f"ERROR fetching session {label}: {error}", file=sys.stderr)
            continue
        docs = parse_session_page(html)
        for doc in docs:
            doc["session"] = label if not label.startswith("http") else safe_name(label)
        print(f"  {label}: {len(docs)} documents")
        all_docs.extend(docs)

    if args.discover:
        print(f"\nDiscovery complete: {len(all_docs)} documents "
              f"across {len({d['session'] for d in all_docs})} sessions")
        return 0

    print(f"\nTotal documents to download: {len(all_docs)}")
    if not all_docs:
        print("Nothing to download.", file=sys.stderr)
        return 1

    manifest_path = os.path.join(args.out_dir, "__manifest__.jsonl")
    os.makedirs(args.out_dir, exist_ok=True)
    import json as _json
    with open(manifest_path, "w") as handle:
        for doc in all_docs:
            handle.write(_json.dumps(doc) + "\n")
    print(f"Wrote manifest: {manifest_path}")

    counts = {"downloaded": 0, "skipped": 0, "failed": 0}
    failures: list[str] = []
    with ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = {pool.submit(download, doc, args.out_dir): doc for doc in all_docs}
        for future in as_completed(futures):
            status, detail = future.result()
            counts[status] += 1
            if status == "failed":
                failures.append(detail)
                print(f"FAILED  {detail}", file=sys.stderr)
            elif counts[status] % 25 == 0 or status != "downloaded":
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