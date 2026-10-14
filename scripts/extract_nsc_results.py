#!/usr/bin/env python3
"""Extract per-school NSC results from the DBE School Performance Report PDFs.

The reports are laid out as one wide table per province. Each school row carries,
for three consecutive years, Progressed / Total Wrote / Total Achieved / % Achieved
(2020-2025 reports). Column positions are read from `pdftotext -bbox` so blank
cells do not shift the remaining values.

Output: the shared SQLite database (default data/nsc/matric.db) with a `results`
table keyed by (emis_number, year), plus a `schools` table of the latest identity
seen. Only the `schools` and `results` tables are refreshed; other tables (subject
results) in the same database are left untouched.

Usage:
    python3 scripts/extract_nsc_results.py [--pdf-dir data/nsc/pdfs] [--db data/nsc/matric.db]
"""

import argparse
import glob
import os
import re
import sqlite3
import subprocess
import sys
import xml.etree.ElementTree as ElementTree

# Reports 2020-2025 print Progressed / Wrote / Achieved / % per year, three years.
ACHIEVED_METRICS = ("progressed", "total_wrote", "total_achieved", "percent_achieved")
ACHIEVED_REPORTS = range(2020, 2026)

NUMBER = re.compile(r"^\d+(?:[.,]\d+)?$")
EMIS = re.compile(r"^\d{9}$")
CENTRE = re.compile(r"^\d{7}$")

# Column right-edges within this distance are treated as the same column.
COLUMN_TOLERANCE = 4.0


def local(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def read_pages(pdf_path: str) -> list[list[dict]]:
    """Return per-page word boxes (text, x, y) via pdftotext -bbox."""
    result = subprocess.run(
        ["pdftotext", "-bbox", pdf_path, "-"],
        capture_output=True,
        text=True,
        check=True,
    )
    root = ElementTree.fromstring(result.stdout)
    pages: list[list[dict]] = []
    for page in root.iter():
        if local(page.tag) != "page":
            continue
        words = []
        for word in page:
            if local(word.tag) != "word":
                continue
            words.append(
                {
                    "text": (word.text or "").strip(),
                    "xmin": float(word.get("xMin", 0)),
                    "xmax": float(word.get("xMax", 0)),
                    "ymin": float(word.get("yMin", 0)),
                    "ymax": float(word.get("yMax", 0)),
                }
            )
        pages.append(words)
    return pages


def cluster_columns(values: list[float]) -> list[float]:
    """Group right-edges into column centres (sorted ascending)."""
    centres: list[float] = []
    for value in sorted(values):
        if centres and abs(value - centres[-1]) <= COLUMN_TOLERANCE:
            centres[-1] = (centres[-1] + value) / 2
        else:
            centres.append(value)
    return centres


def nearest(centres: list[float], value: float) -> int | None:
    if not centres:
        return None
    best = min(range(len(centres)), key=lambda i: abs(centres[i] - value))
    return best if abs(centres[best] - value) <= COLUMN_TOLERANCE else None


def group_rows(words: list[dict]) -> list[list[dict]]:
    """Group words into visual rows by vertical proximity.

    Cells on the same line can differ slightly in y (decimal values sit a
    fraction of a point above integers), so cluster by proximity rather than a
    fixed bucket to keep a row's numbers together.
    """
    rows: list[dict] = []
    for word in sorted(words, key=lambda w: (w["ymin"], w["xmin"])):
        if rows and abs(word["ymin"] - rows[-1]["y"]) <= 2.0:
            rows[-1]["words"].append(word)
        else:
            rows.append({"y": word["ymin"], "words": [word]})
    return [sorted(row["words"], key=lambda w: w["xmin"]) for row in rows]


def extract_achieved(pages: list[list[dict]], report_year: int) -> list[dict]:
    """Pull school rows from a 2020-2025 report (three years, four metrics each)."""
    years = [report_year - 2, report_year - 1, report_year]
    records: list[dict] = []
    for words in pages:
        labels = {w["text"] for w in words}
        # Only the individual-results table carries all of these.
        if not {"Progressed", "Total", "Wrote", "Achieved"} <= labels:
            continue
        rows = group_rows(words)

        # Data rows begin with a nine-digit EMIS and a seven-digit centre. Metric
        # columns are then the right-aligned numbers; derive them from those rows
        # only, excluding the id columns so they cannot join the cluster set.
        data_rows = []
        for row in rows:
            emis_idx = next((i for i, w in enumerate(row) if EMIS.match(w["text"])), None)
            centre_idx = next(
                (i for i, w in enumerate(row) if CENTRE.match(w["text"])), None
            )
            if emis_idx is not None and centre_idx is not None and centre_idx > emis_idx:
                data_rows.append((row, emis_idx, centre_idx))
        numeric_edges = [
            w["xmax"]
            for row, emis_idx, centre_idx in data_rows
            for w in row
            if NUMBER.match(w["text"])
            and not EMIS.match(w["text"])
            and not CENTRE.match(w["text"])
        ]
        centres = cluster_columns(numeric_edges)
        metric_centres = centres[-12:] if len(centres) >= 12 else centres

        for row, emis_idx, centre_idx in data_rows:
            emis = row[emis_idx]["text"]
            centre = row[centre_idx]["text"]
            name_words = [w["text"] for w in row[centre_idx + 1 :] if not NUMBER.match(w["text"])]
            name = " ".join(name_words).strip()

            numeric = [w for w in row[centre_idx + 1 :] if NUMBER.match(w["text"])]
            if not numeric:
                continue
            # First number after the name is the quintile; the rest are the
            # three years of Progressed/Wrote/Achieved/%. A complete row maps
            # positionally, which avoids cluster drift; short rows (blank cells)
            # fall back to nearest-column placement.
            metrics = numeric[1:]
            if len(metrics) == 12:
                by_column = {i: metrics[i]["text"].replace(",", ".") for i in range(12)}
            else:
                by_column = {}
                for word in metrics:
                    idx = nearest(metric_centres, word["xmax"])
                    if idx is not None:
                        by_column[idx] = word["text"].replace(",", ".")
            if not by_column:
                continue

            for group, year in enumerate(years):
                values = [
                    by_column.get(group * 4 + offset) for offset in range(len(ACHIEVED_METRICS))
                ]
                if all(v is None for v in values):
                    continue

                def as_int(raw: str | None) -> int | None:
                    if raw is None or not raw.isdigit():
                        return None
                    return int(raw)

                progressed = as_int(values[0])
                wrote = as_int(values[1])
                achieved = as_int(values[2])
                percent = float(values[3]) if values[3] else None
                # The report's percentage is achieved/wrote; derive it when the
                # cell is blank or split across a wrapped line.
                if percent is None and wrote and achieved is not None:
                    percent = round(achieved / wrote * 100, 1)

                records.append(
                    {
                        "emis_number": emis,
                        "centre_number": centre,
                        "centre_name": name,
                        "report_year": report_year,
                        "year": year,
                        "progressed": progressed,
                        "total_wrote": wrote,
                        "total_achieved": achieved,
                        "percent_achieved": percent,
                    }
                )
    return records


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pdf-dir", default="data/nsc/pdfs")
    parser.add_argument("--db", default="data/nsc/matric.db")
    args = parser.parse_args()

    all_records: list[dict] = []
    for report_year in ACHIEVED_REPORTS:
        matches = glob.glob(os.path.join(args.pdf_dir, f"{report_year}_*.pdf"))
        if not matches:
            print(f"skip {report_year}: no PDF", file=sys.stderr)
            continue
        pages = read_pages(matches[0])
        records = extract_achieved(pages, report_year)
        print(f"{report_year}: {len(records)} school-year rows from {len(pages)} pages", file=sys.stderr)
        all_records.extend(records)

    os.makedirs(os.path.dirname(args.db), exist_ok=True)
    connection = sqlite3.connect(args.db)
    connection.executescript(
        """
        DROP TABLE IF EXISTS schools;
        DROP TABLE IF EXISTS results;
        CREATE TABLE schools (
            emis_number TEXT PRIMARY KEY,
            centre_number TEXT,
            centre_name TEXT
        );
        CREATE TABLE results (
            emis_number TEXT NOT NULL,
            year INTEGER NOT NULL,
            progressed INTEGER,
            total_wrote INTEGER,
            total_achieved INTEGER,
            percent_achieved REAL,
            report_year INTEGER NOT NULL,
            PRIMARY KEY (emis_number, year)
        );
        """
    )
    for record in all_records:
        connection.execute(
            """
            INSERT INTO results (emis_number, year, progressed, total_wrote, total_achieved, percent_achieved, report_year)
            VALUES (:emis_number, :year, :progressed, :total_wrote, :total_achieved, :percent_achieved, :report_year)
            ON CONFLICT (emis_number, year) DO UPDATE SET
                progressed=excluded.progressed, total_wrote=excluded.total_wrote,
                total_achieved=excluded.total_achieved, percent_achieved=excluded.percent_achieved,
                report_year=excluded.report_year
            """,
            record,
        )
        connection.execute(
            """
            INSERT INTO schools (emis_number, centre_number, centre_name)
            VALUES (:emis_number, :centre_number, :centre_name)
            ON CONFLICT (emis_number) DO UPDATE SET
                centre_number=excluded.centre_number, centre_name=excluded.centre_name
            """,
            record,
        )
    connection.commit()
    schools = connection.execute("SELECT COUNT(*) FROM schools").fetchone()[0]
    results = connection.execute("SELECT COUNT(*) FROM results").fetchone()[0]
    years = connection.execute("SELECT MIN(year), MAX(year) FROM results").fetchone()
    connection.close()
    print(f"Wrote {schools} schools, {results} results ({years[0]}-{years[1]}) to {args.db}")


if __name__ == "__main__":
    main()
