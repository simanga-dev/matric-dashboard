#!/usr/bin/env python3
"""Extract per-school subject results from the DBE School Subject Report PDFs.

The reports list every school in every province with, for the 11 gateway
subjects, the number of candidates and the pass rate. Schools are grouped by
province and each report covers two exam years.

Layouts changed over the years:

    2012-2013  2 metrics per subject (Wrote, %achieved), single exam year
    2014       2 metrics, two exam years, one file per province
    2015-2017  2 metrics, two exam years, stacked rows per school
    2018-2019  3 metrics (Total Wrote, Achieved, %achieved)
    2020-2024  3 metrics, column order varies (Exam Date before/after Centre)

Column positions come from `pdftotext -bbox`. Subject bands are located by
matching the printed subject headers left to right; metric columns are then
assigned to subjects with a dynamic program so whole-blank subjects do not
shift the remaining values onto the wrong subject.

Output: the shared SQLite database (default data/nsc/matric.db) with a
`subjects` dimension, a `subject_aliases` table, a `report_sources` provenance
table and a `subject_results` fact table keyed by (centre_number, subject_id,
year). Only these tables are refreshed; other tables in the same database (the
overall school results) are left untouched.

Usage:
    python3 scripts/extract_subject_results.py \
        [--pdf-dir data/school_subject_reports] [--pdf-dir data/nsc/subject_pdfs] \
        [--db data/nsc/matric.db]
"""

import argparse
import glob
import hashlib
import os
import re
import sqlite3
import subprocess
import sys
from html import unescape

# Canonical 11 gateway subjects in printed (left to right) order. `keys` are
# the header words that anchor the subject band; the first match in x order
# wins, which also disambiguates the two "Maths" headers in older reports.
SUBJECTS = [
    ("accounting", "Accounting", {"accounting"}),
    ("agricultural-sciences", "Agricultural Sciences", {"agricultural"}),
    ("business-studies", "Business Studies", {"business"}),
    ("economics", "Economics", {"economics"}),
    ("english-fal", "English First Additional Language", {"english"}),
    ("geography", "Geography", {"geography"}),
    ("history", "History", {"history"}),
    ("life-sciences", "Life Sciences", {"life"}),
    ("mathematical-literacy", "Mathematical Literacy", {"mathematical", "maths"}),
    ("mathematics", "Mathematics", {"mathematics", "maths"}),
    ("physical-sciences", "Physical Sciences", {"physical"}),
]

# Printed header aliases kept in the database for traceability.
SUBJECT_ALIASES = {
    "english-fal": ["English (FAL)", "English First Additional Language"],
    "mathematical-literacy": ["Maths Lit", "Mathematical Literacy"],
    "mathematics": ["Maths", "Mathematics"],
}

# Metric columns per subject, by report year.
METRICS_PER_SUBJECT = {
    2012: 2, 2013: 2, 2014: 2, 2015: 2, 2016: 2, 2017: 2,
    2018: 3, 2019: 3, 2020: 3, 2021: 3, 2022: 3, 2023: 3, 2024: 3,
}

WORD = re.compile(
    r'<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">(.*?)</word>',
    re.DOTALL,
)
CENTRE = re.compile(r"^\d{7}$")
YEAR = re.compile(r"^(20\d\d)$")
NUMBER = re.compile(r"^\d+(?:[.,]\d+)?$")

# Column centres within this distance are treated as the same column.
COLUMN_TOLERANCE = 8.0
ROW_TOLERANCE = 2.0
# A token's centre within this distance of a column belongs to it.
CELL_TOLERANCE = 8.0
# A metric token's centre may sit slightly left of the header's left edge
# (single digits are narrower than the header word) but never as far left as
# the quintile/identity columns.
METRIC_MARGIN = 6.0
# Maximum vertical distance between a data row and the school's centre row.
BLOCK_MAX_Y = 40.0


def read_pages(pdf_path: str) -> list[list[dict]]:
    """Return per-page word boxes (text, x, y) via pdftotext -bbox.

    Parsing is regex based rather than XML so embedded control glyphs (present
    in some reports) do not abort the whole document.
    """
    result = subprocess.run(
        ["pdftotext", "-bbox", pdf_path, "-"],
        capture_output=True,
        text=True,
        check=True,
    )
    pages: list[list[dict]] = []
    for chunk in re.split(r"<page[^>]*>", result.stdout)[1:]:
        words = []
        for xmin, ymin, xmax, ymax, text in WORD.findall(chunk):
            text = "".join(c for c in unescape(text) if c >= " ")
            if not text.strip():
                continue
            words.append(
                {
                    "text": text.strip(),
                    "xmin": float(xmin),
                    "xmax": float(xmax),
                    "ymin": float(ymin),
                    "ymax": float(ymax),
                }
            )
        pages.append(words)
    return pages


def centre_of(word: dict) -> float:
    """Horizontal centre of a word box."""
    return (word["xmin"] + word["xmax"]) / 2


def group_rows(words: list[dict]) -> list[list[dict]]:
    """Group words into visual rows by vertical proximity."""
    rows: list[dict] = []
    for word in sorted(words, key=lambda w: (w["ymin"], w["xmin"])):
        if rows and abs(word["ymin"] - rows[-1]["y"]) <= ROW_TOLERANCE:
            rows[-1]["words"].append(word)
        else:
            rows.append({"y": word["ymin"], "words": [word]})
    return [sorted(row["words"], key=lambda w: w["xmin"]) for row in rows]


def metric_header_y(words: list[dict]) -> float | None:
    """Return the y of the metric header row (Wrote/Total), or None."""
    ys = [
        w["ymin"]
        for w in words
        if w["text"].strip().lower() in ("wrote", "total")
    ]
    return min(ys) if ys else None


def metric_region_x(words: list[dict], header_y: float) -> float | None:
    """Return the left edge of the metric grid from header Wrote/Total tokens."""
    xs = [
        w["xmin"]
        for w in words
        if w["ymin"] <= header_y + ROW_TOLERANCE
        and w["text"].strip().lower() in ("wrote", "total")
    ]
    return min(xs) if xs else None


def subject_anchors(words: list[dict], header_max_y: float) -> list[float] | None:
    """Return the x-centre of each subject header in printed order, or None."""
    candidates = sorted(
        (w for w in words if w["ymin"] < header_max_y and w["text"].strip()),
        key=lambda w: w["xmin"],
    )
    used = [False] * len(candidates)
    anchors: list[float] = []
    last_x = -1.0
    for _, _, keys in SUBJECTS:
        match = None
        for index, word in enumerate(candidates):
            if used[index]:
                continue
            token = word["text"].strip().lower().strip(".,()")
            if token in keys and word["xmin"] > last_x:
                match = index
                break
        if match is None:
            return None
        used[match] = True
        word = candidates[match]
        anchors.append((word["xmin"] + word["xmax"]) / 2)
        last_x = word["xmin"]
    if anchors != sorted(anchors):
        return None
    return anchors


def cluster_columns(values: list[float]) -> list[float]:
    """Cluster right-edges into column centres (sorted ascending)."""
    centres: list[float] = []
    for value in sorted(values):
        if centres and abs(value - centres[-1]) <= COLUMN_TOLERANCE:
            centres[-1] = (centres[-1] + value) / 2
        else:
            centres.append(value)
    return centres


def assign_columns(
    columns: list[float], anchors: list[float], metrics: int
) -> dict[int, list[int]] | None:
    """Match groups of `metrics` consecutive columns to subjects.

    A subject owns `metrics` consecutive columns or none at all. A dynamic
    program picks the assignment whose columns sit closest to the printed
    subject anchors, so blank subjects do not shift the remaining columns.
    """
    count = len(columns)
    subjects = len(SUBJECTS)
    infinity = float("inf")
    cost = [[infinity] * (count + 1) for _ in range(subjects + 1)]
    previous = [[None] * (count + 1) for _ in range(subjects + 1)]
    cost[0][0] = 0
    for i in range(1, subjects + 1):
        anchor = anchors[i - 1]
        for j in range(count + 1):
            if cost[i - 1][j] == infinity:
                continue
            if cost[i - 1][j] < cost[i][j]:
                cost[i][j] = cost[i - 1][j]
                previous[i][j] = (j, False)
            if j + metrics <= count:
                group = sum(abs(columns[j + k] - anchor) for k in range(metrics))
                if cost[i - 1][j] + group < cost[i][j + metrics]:
                    cost[i][j + metrics] = cost[i - 1][j] + group
                    previous[i][j + metrics] = (j, True)
    if cost[subjects][count] == infinity:
        return None

    mapping: dict[int, list[int]] = {}
    i, j = subjects, count
    while i > 0:
        prev, assigned = previous[i][j]
        if assigned:
            mapping[i - 1] = list(range(prev, prev + metrics))
            j = prev
        i -= 1
    return mapping


def to_int(raw: str) -> int | None:
    cleaned = raw.replace(" ", "").replace(",", "")
    return int(cleaned) if cleaned.isdigit() else None


def to_float(raw: str) -> float | None:
    try:
        return float(raw.replace(" ", "").replace(",", "."))
    except ValueError:
        return None


def extract_report(pdf_path: str, report_year: int, metrics: int) -> list[dict]:
    """Pull per-school subject rows from one School Subject Report."""
    records: list[dict] = []
    for words in read_pages(pdf_path):
        header_y = metric_header_y(words)
        if header_y is None:
            continue
        region_x = metric_region_x(words, header_y)
        if region_x is None:
            continue
        anchors = subject_anchors(words, header_y)
        if anchors is None:
            continue

        rows = group_rows(words)
        centre_rows = [
            (row[0]["ymin"], centre["text"], centre)
            for row in rows
            for centre in [next((w for w in row if CENTRE.match(w["text"])), None)]
            if centre is not None
        ]

        parsed: list[tuple[str, int, list[dict]]] = []
        column_edges: list[float] = []
        for row in rows:
            centre = next((w for w in row if CENTRE.match(w["text"])), None)
            year = next(
                (w for w in row if YEAR.match(w["text"]) and w["xmax"] < region_x),
                None,
            )
            # Skip title/heading/page-number rows: a real data row carries a
            # centre number or an exam year in the identity region.
            if centre is None and year is None:
                continue
            metrics_tokens = [
                w
                for w in row
                if NUMBER.match(w["text"])
                and centre_of(w) >= region_x - METRIC_MARGIN
            ]
            if not metrics_tokens:
                continue
            if centre is None:
                # Rows without a centre belong to the nearest centre row (two
                # exam years are stacked under one school, in either order).
                y = row[0]["ymin"]
                nearest = min(
                    centre_rows, key=lambda c: abs(c[0] - y), default=None
                )
                if nearest is None or abs(nearest[0] - y) > BLOCK_MAX_Y:
                    continue
                centre_number = nearest[1]
            else:
                centre_number = centre["text"]
            exam_year = int(year["text"]) if year else report_year
            parsed.append((centre_number, exam_year, metrics_tokens))
            column_edges.extend(centre_of(w) for w in metrics_tokens)

        columns = cluster_columns(column_edges)
        if len(columns) < metrics:
            continue
        mapping = assign_columns(columns, anchors, metrics)
        if mapping is None:
            continue

        for centre_number, exam_year, tokens in parsed:
            row_columns: dict[int, dict] = {}
            for token in tokens:
                index = min(
                    range(len(columns)),
                    key=lambda c: abs(columns[c] - centre_of(token)),
                )
                if abs(columns[index] - centre_of(token)) <= CELL_TOLERANCE:
                    row_columns[index] = token

            for subject, indices in mapping.items():
                values = [row_columns.get(index) for index in indices]
                if not any(values):
                    continue
                wrote = to_int(values[0]["text"]) if values[0] else None
                if metrics == 3:
                    achieved = to_int(values[1]["text"]) if values[1] else None
                    percent = to_float(values[2]["text"]) if values[2] else None
                else:
                    achieved = None
                    percent = to_float(values[1]["text"]) if values[1] else None
                if percent is None and wrote and achieved is not None:
                    percent = round(achieved / wrote * 100, 1)
                # A few older PDFs drop the decimal point (e.g. 60.0 -> 600);
                # recover from the counts rather than storing an impossible value.
                if percent is not None and percent > 100:
                    percent = (
                        round(achieved / wrote * 100, 1)
                        if wrote and achieved is not None
                        else None
                    )
                records.append(
                    {
                        "centre_number": centre_number,
                        "subject_id": subject + 1,
                        "subject": SUBJECTS[subject][1],
                        "year": exam_year,
                        "total_wrote": wrote,
                        "achieved": achieved,
                        "percent_achieved": percent,
                        "report_year": report_year,
                    }
                )
    return records


def report_year_from_name(path: str) -> int | None:
    years = re.findall(r"(20\d\d)", os.path.basename(path))
    return int(years[0]) if years else None


def province_from_name(path: str) -> str | None:
    match = re.search(r"SUBJECT REPORT (E?C|FS|GP|KZ|LP|MP|N[WC])", os.path.basename(path))
    return match.group(1) if match else None


def discover(directories: list[str]) -> list[tuple[str, int]]:
    """Return (path, report_year) pairs, one file per report/province."""
    seen: dict[tuple[int, str | None], str] = {}
    for directory in directories:
        for path in sorted(glob.glob(os.path.join(directory, "*.pdf"))):
            year = report_year_from_name(path)
            if year is None or year not in METRICS_PER_SUBJECT:
                continue
            key = (year, province_from_name(path))
            current = seen.get(key)
            # Prefer the normalised `subject_pdfs` copies, then shorter names.
            if current is None or (
                "subject_pdfs" in path and "subject_pdfs" not in current
            ) or (
                len(os.path.basename(path)) < len(os.path.basename(current))
                and ("subject_pdfs" in path) == ("subject_pdfs" in current)
            ):
                seen[key] = path
    return sorted(((path, year) for (year, _), path in seen.items()), key=lambda x: x[1])


SCHEMA = """
DROP TABLE IF EXISTS subject_results;
DROP TABLE IF EXISTS subject_aliases;
DROP TABLE IF EXISTS report_sources;
DROP TABLE IF EXISTS subjects;
CREATE TABLE subjects (
    subject_id INTEGER PRIMARY KEY,
    code       TEXT NOT NULL UNIQUE,
    name       TEXT NOT NULL UNIQUE,
    is_gateway INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE subject_aliases (
    subject_id INTEGER NOT NULL REFERENCES subjects(subject_id),
    alias      TEXT NOT NULL UNIQUE
);
CREATE TABLE report_sources (
    source_id   INTEGER PRIMARY KEY,
    file_name   TEXT NOT NULL,
    report_year INTEGER NOT NULL,
    sha256      TEXT,
    row_count   INTEGER
);
CREATE TABLE subject_results (
    centre_number   TEXT NOT NULL,
    subject_id      INTEGER NOT NULL REFERENCES subjects(subject_id),
    subject         TEXT NOT NULL,
    year            INTEGER NOT NULL,
    total_wrote     INTEGER,
    achieved        INTEGER,
    percent_achieved REAL,
    report_year     INTEGER NOT NULL,
    source_id       INTEGER REFERENCES report_sources(source_id),
    PRIMARY KEY (centre_number, subject_id, year)
);
CREATE INDEX idx_subject_results_year ON subject_results(year);
CREATE INDEX idx_subject_results_subject ON subject_results(subject_id, year);
"""


def build_database(db_path: str, records: list[dict], sources: list[dict]) -> None:
    os.makedirs(os.path.dirname(db_path) or ".", exist_ok=True)
    connection = sqlite3.connect(db_path)
    connection.executescript(SCHEMA)
    for index, (_, name, _) in enumerate(SUBJECTS, start=1):
        connection.execute(
            "INSERT INTO subjects(subject_id, code, name, is_gateway) VALUES (?,?,?,1)",
            (index, SUBJECTS[index - 1][0], name),
        )
        aliases = SUBJECT_ALIASES.get(SUBJECTS[index - 1][0], [name])
        for alias in aliases:
            connection.execute(
                "INSERT OR IGNORE INTO subject_aliases(subject_id, alias) VALUES (?,?)",
                (index, alias),
            )
    for source in sources:
        cursor = connection.execute(
            "INSERT INTO report_sources(file_name, report_year, sha256, row_count) VALUES (?,?,?,?)",
            (source["file_name"], source["report_year"], source["sha256"], source["row_count"]),
        )
        source["source_id"] = cursor.lastrowid
    connection.executemany(
        """
        INSERT INTO subject_results
            (centre_number, subject_id, subject, year, total_wrote, achieved,
             percent_achieved, report_year, source_id)
        VALUES (:centre_number, :subject_id, :subject, :year, :total_wrote,
                :achieved, :percent_achieved, :report_year, :source_id)
        ON CONFLICT (centre_number, subject_id, year) DO UPDATE SET
            subject=excluded.subject,
            total_wrote=excluded.total_wrote,
            achieved=excluded.achieved,
            percent_achieved=excluded.percent_achieved,
            report_year=excluded.report_year,
            source_id=excluded.source_id
        """,
        records,
    )
    connection.commit()
    count = connection.execute("SELECT COUNT(*) FROM subject_results").fetchone()[0]
    connection.close()
    print(f"Wrote {count} subject rows to {db_path}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--pdf-dir",
        action="append",
        default=None,
        help="Directory to search (repeatable). "
        "Defaults to data/school_subject_reports and data/nsc/subject_pdfs.",
    )
    parser.add_argument("--db", default="data/nsc/matric.db")
    args = parser.parse_args()

    directories = args.pdf_dir or [
        "data/school_subject_reports",
        "data/nsc/subject_pdfs",
    ]

    sources = discover(directories)
    if not sources:
        print("No report PDFs found", file=sys.stderr)
        return 1
    print(f"Extracting from {len(sources)} report files", file=sys.stderr)

    all_records: list[dict] = []
    source_rows: list[dict] = []
    for path, report_year in sources:
        with open(path, "rb") as handle:
            digest = hashlib.sha256(handle.read()).hexdigest()
        records = extract_report(path, report_year, METRICS_PER_SUBJECT[report_year])
        for record in records:
            record["source_id"] = len(source_rows) + 1
        centres = {r["centre_number"] for r in records}
        years = sorted({r["year"] for r in records})
        print(
            f"{report_year}: {os.path.basename(path)} -> {len(records)} rows, "
            f"{len(centres)} centres, years {years}",
            file=sys.stderr,
        )
        all_records.extend(records)
        source_rows.append(
            {
                "file_name": os.path.basename(path),
                "report_year": report_year,
                "sha256": digest,
                "row_count": len(records),
            }
        )

    build_database(args.db, all_records, source_rows)

    connection = sqlite3.connect(args.db)
    years = connection.execute(
        "SELECT MIN(year), MAX(year) FROM subject_results"
    ).fetchone()
    centres = connection.execute(
        "SELECT COUNT(DISTINCT centre_number) FROM subject_results"
    ).fetchone()[0]
    connection.close()
    print(f"{centres} centres, exam years {years[0]}-{years[1]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
