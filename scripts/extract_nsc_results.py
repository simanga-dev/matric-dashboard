#!/usr/bin/env python3
"""Extract per-school NSC results from the DBE School Performance Report PDFs.

Two report generations share the same wide-table layout (one table per province)
but differ in columns:

* 2020-2025 reports print, for each of three consecutive years,
  Progressed / Total Wrote / Total Achieved / % Achieved.
* 2012-2019 reports cover 2010-2019 and print only Total Wrote / Total Achieved /
  % Achieved for the older years, gaining a Progressed column from 2015 onward.
  The metric order varies between report years.

Column positions are read from `pdftotext -bbox` so blank cells do not shift the
remaining values. Earlier reports are extracted first and newer ones last; the
upsert keeps the newest non-null value per (emis_number, year), so existing
2018-2025 data is never clobbered by an older report.

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
from html import unescape

# Reports 2020-2025 print Progressed / Wrote / Achieved / % per year, three years.
ACHIEVED_METRICS = ("progressed", "total_wrote", "total_achieved", "percent_achieved")
ACHIEVED_REPORTS = range(2020, 2026)

# Legacy (2012-2019) metric orders.
_LEGACY_A = ("wrote", "achieved", "percent")
_LEGACY_B = ("progressed", "wrote", "achieved", "percent")
_LEGACY_C = ("wrote", "progressed", "achieved", "percent")  # 2015's own column order


def _sequence(specs: list[tuple[int, tuple[str, ...]]]) -> tuple[tuple[int, str], ...]:
    """Flatten [(year, metrics), ...] into printed left-to-right (year, metric)."""
    return tuple((year, metric) for year, metrics in specs for metric in metrics)


# Per report year: the (calendar year, metric) sequence, in printed order.
LEGACY_LAYOUTS = {
    2012: _sequence([(2010, _LEGACY_A), (2011, _LEGACY_A), (2012, _LEGACY_A)]),
    2013: _sequence([(2011, _LEGACY_A), (2012, _LEGACY_A), (2013, _LEGACY_A)]),
    2014: _sequence([(2012, _LEGACY_A), (2013, _LEGACY_A), (2014, _LEGACY_A)]),
    2015: _sequence([(2013, _LEGACY_A), (2014, _LEGACY_A), (2015, _LEGACY_C)]),
    2016: _sequence([(2014, _LEGACY_A), (2015, _LEGACY_B), (2016, _LEGACY_B)]),
    2017: _sequence([(2015, _LEGACY_B), (2016, _LEGACY_B), (2017, _LEGACY_B)]),
    2018: _sequence([(2016, _LEGACY_B), (2017, _LEGACY_B), (2018, _LEGACY_B)]),
    2019: _sequence([(2017, _LEGACY_B), (2018, _LEGACY_B), (2019, _LEGACY_B)]),
}
LEGACY_REPORTS = sorted(LEGACY_LAYOUTS)
# Where legacy reports live; the first match wins, so prefer the primary pdf dir.
LEGACY_REPORT_DIRS = (
    "data/reports/National Senior Certificate Examination Reports",
)

NUMBER = re.compile(r"^\d+(?:[.,]\d+)?$")
EMIS = re.compile(r"^\d{9}$")
CENTRE = re.compile(r"^\d{7}$")
YEAR = re.compile(r"^(?:19|20)\d\d$")

# Column right-edges within this distance are treated as the same column.
COLUMN_TOLERANCE = 4.0
# Legacy rows/columns are matched by proximity with looser tolerances.
ROW_TOLERANCE = 2.0
GROUP_TOLERANCE = 6.0
CELL_TOLERANCE = 12.0

# pdftotext -bbox emits simple elements; a regex tolerates the malformed markup
# (entities, stray control characters) some legacy reports produce.
WORD = re.compile(
    r'<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">(.*?)</word>',
    re.DOTALL,
)


def read_pages(pdf_path: str) -> list[list[dict]]:
    """Return per-page word boxes (text, x/y bounds) via pdftotext -bbox."""
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
            text = "".join(character for character in unescape(text) if character >= " ").strip()
            if not text:
                continue
            words.append(
                {
                    "text": text,
                    "xmin": float(xmin),
                    "xmax": float(xmax),
                    "ymin": float(ymin),
                    "ymax": float(ymax),
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


def legacy_group_rows(words: list[dict]) -> list[dict]:
    """Group words into visual rows retaining each row's y for name lookup."""
    rows: list[dict] = []
    for word in sorted(words, key=lambda w: (w["ymin"], w["xmin"])):
        if rows and abs(word["ymin"] - rows[-1]["y"]) <= ROW_TOLERANCE:
            rows[-1]["words"].append(word)
        else:
            rows.append({"y": word["ymin"], "words": [word]})
    return rows


def legacy_metric(text: str, percent_xs: list[float], x0: float) -> str | None:
    lowered = text.lower().strip()
    if lowered.startswith("%"):
        return "percent"
    if lowered == "wrote":
        return "wrote"
    if "progressed" in lowered:
        return "progressed"
    if lowered == "achieved":
        return (
            "percent"
            if any(abs(x0 - px) <= GROUP_TOLERANCE for px in percent_xs)
            else "achieved"
        )
    return None


def legacy_columns(rows: list[dict]) -> list[tuple[float, str]] | None:
    """Return sorted [(centre_x, metric)] header anchors for the page, or None."""
    year_rows = [(row, sum(1 for w in row["words"] if YEAR.match(w["text"]))) for row in rows]
    year_rows = [(row, count) for row, count in year_rows if count >= 3]
    if not year_rows:
        return None
    year_row, _ = min(year_rows, key=lambda rc: rc[0]["y"])
    data_y = min(
        (row["y"] for row in rows if any(EMIS.match(w["text"]) for w in row["words"])),
        default=None,
    )
    if data_y is None:
        return None
    band = [
        w
        for row in rows
        if year_row["y"] - GROUP_TOLERANCE <= row["y"] <= data_y
        for w in row["words"]
    ]
    # The individual-results tables carry a Quintile column; appendices for
    # special/part-time candidates share the metric layout but drop it, and their
    # same-EMIS rows would otherwise overwrite the mainstream results.
    if not any(w["text"].lower() == "quintile" for w in band):
        return None
    percent_xs = [w["xmin"] for w in band if w["text"] == "%"]
    groups: list[dict] = []
    for word in band:
        metric = legacy_metric(word["text"], percent_xs, word["xmin"])
        if metric is None:
            continue
        for group in groups:
            if abs(group["x0"] - word["xmin"]) <= GROUP_TOLERANCE:
                group["x1"] = max(group["x1"], word["xmax"])
                group["kinds"].add(metric)
                break
        else:
            groups.append({"x0": word["xmin"], "x1": word["xmax"], "kinds": {metric}})
    groups.sort(key=lambda g: g["x0"])
    result = []
    for group in groups:
        for preferred in ("percent", "progressed", "wrote", "achieved"):
            if preferred in group["kinds"]:
                result.append(((group["x0"] + group["x1"]) / 2, preferred))
                break
    return result


def to_int(raw: str | None) -> int | None:
    if raw is None:
        return None
    cleaned = raw.replace(" ", "").replace(",", "").replace(".", "")
    return int(cleaned) if cleaned.isdigit() else None


def to_float(raw: str | None) -> float | None:
    if raw is None:
        return None
    try:
        return float(raw.replace(" ", "").replace(",", "."))
    except ValueError:
        return None


def in_dinaledi(word: dict, ranges: list[tuple[float, float]]) -> bool:
    return any(
        word["xmin"] >= x0 - 3 and word["xmax"] <= x1 + 3 for x0, x1 in ranges
    )


def extract_legacy(pdf_path: str, report_year: int, sequence: tuple[tuple[int, str], ...]) -> list[dict]:
    """Pull school rows from a 2012-2019 report laid out per LEGACY_LAYOUTS."""
    records: dict[tuple[str, int], dict] = {}
    pages = read_pages(pdf_path)
    for words in pages:
        rows = legacy_group_rows(words)
        columns = legacy_columns(rows)
        if columns is None or len(columns) != len(sequence):
            continue
        centres = [column[0] for column in columns]
        first_x = centres[0]
        first_edge = centres[0] - 12
        dinaledi = [(w["xmin"], w["xmax"]) for w in words if w["text"].lower() == "dinaledi"]
        for row in rows:
            emis_w = next((w for w in row["words"] if EMIS.match(w["text"])), None)
            centre_w = next((w for w in row["words"] if CENTRE.match(w["text"])), None)
            if emis_w is None or centre_w is None:
                continue
            numbers = [
                w
                for w in row["words"]
                if NUMBER.match(w["text"])
                and w["xmin"] >= first_edge
                and w is not emis_w
                and w is not centre_w
                and not in_dinaledi(w, dinaledi)
            ]
            mapped: dict[int, str] = {}
            for word in numbers:
                center = (word["xmin"] + word["xmax"]) / 2
                best = min(range(len(centres)), key=lambda i: abs(centres[i] - center))
                if abs(centres[best] - center) <= CELL_TOLERANCE:
                    mapped[best] = word["text"]
            if not mapped:
                continue
            # The name often sits on the line above the numbers, so collect name
            # words from a small y-window around the row's numbers.
            name_words = []
            for other in rows:
                if abs(other["y"] - row["y"]) > 14:
                    continue
                for word in other["words"]:
                    if (
                        word["xmin"] >= centre_w["xmax"] - 1
                        and word["xmax"] <= first_x - 18
                        and not NUMBER.match(word["text"])
                        and not in_dinaledi(word, dinaledi)
                    ):
                        name_words.append((other["y"], word["xmin"], word["text"]))
            name_words.sort()
            name = " ".join(text for _, _, text in name_words).strip()
            for index, (year, metric) in enumerate(sequence):
                raw = mapped.get(index)
                key = (emis_w["text"], year)
                record = records.get(key)
                if record is None:
                    record = {
                        "emis_number": emis_w["text"],
                        "centre_number": centre_w["text"],
                        "centre_name": name,
                        "report_year": report_year,
                        "year": year,
                        "progressed": None,
                        "total_wrote": None,
                        "total_achieved": None,
                        "percent_achieved": None,
                    }
                    records[key] = record
                elif name and not record["centre_name"]:
                    record["centre_name"] = name
                if raw is None:
                    continue
                if metric == "progressed":
                    record["progressed"] = to_int(raw)
                elif metric == "wrote":
                    record["total_wrote"] = to_int(raw)
                elif metric == "achieved":
                    record["total_achieved"] = to_int(raw)
                elif metric == "percent":
                    record["percent_achieved"] = to_float(raw)
    out = []
    for record in records.values():
        if record["percent_achieved"] is None and record["total_wrote"] and record["total_achieved"] is not None:
            record["percent_achieved"] = round(
                record["total_achieved"] / record["total_wrote"] * 100, 1
            )
        if all(
            record[k] is None
            for k in ("progressed", "total_wrote", "total_achieved", "percent_achieved")
        ):
            continue
        out.append(record)
    return out


def find_legacy_report(report_year: int, pdf_dir: str) -> str | None:
    """Locate a legacy report, preferring pdf_dir over the bundled reports dir.

    Matching is by filename: it must mention the report year and be a school
    performance report, so sibling diagnostic/technical reports for the same year
    are not mistaken for it. The `{year}_...` naming used in pdf_dir wins.
    """
    year_pattern = re.compile(rf"(?<!\d){report_year}(?!\d)")
    for directory in (pdf_dir, *LEGACY_REPORT_DIRS):
        if not os.path.isdir(directory):
            continue
        candidates = [
            path
            for path in sorted(glob.glob(os.path.join(directory, "*.pdf")))
            if year_pattern.search(os.path.basename(path))
            and "performance report" in os.path.basename(path).lower().replace("_", " ")
        ]
        if not candidates:
            continue
        for path in candidates:
            if os.path.basename(path).startswith(f"{report_year}_"):
                return path
        return candidates[0]
    return None


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--pdf-dir", default="data/nsc/pdfs")
    parser.add_argument("--db", default="data/nsc/matric.db")
    args = parser.parse_args()

    # Legacy reports first, then modern: the upsert below keeps the last writer's
    # non-null value, so newer reports win for any (emis_number, year) overlap.
    all_records: list[dict] = []
    for report_year in LEGACY_REPORTS:
        path = find_legacy_report(report_year, args.pdf_dir)
        if not path:
            print(f"skip legacy {report_year}: no PDF", file=sys.stderr)
            continue
        records = extract_legacy(path, report_year, LEGACY_LAYOUTS[report_year])
        print(f"{report_year}: {len(records)} school-year rows (legacy)", file=sys.stderr)
        all_records.extend(records)

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
                progressed=COALESCE(excluded.progressed, results.progressed),
                total_wrote=COALESCE(excluded.total_wrote, results.total_wrote),
                total_achieved=COALESCE(excluded.total_achieved, results.total_achieved),
                percent_achieved=COALESCE(excluded.percent_achieved, results.percent_achieved),
                report_year=excluded.report_year
            """,
            record,
        )
        connection.execute(
            """
            INSERT INTO schools (emis_number, centre_number, centre_name)
            VALUES (:emis_number, :centre_number, :centre_name)
            ON CONFLICT (emis_number) DO UPDATE SET
                centre_number=COALESCE(NULLIF(excluded.centre_number, ''), schools.centre_number),
                centre_name=COALESCE(NULLIF(excluded.centre_name, ''), schools.centre_name)
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
