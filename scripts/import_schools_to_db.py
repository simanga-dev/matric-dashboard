#!/usr/bin/env python3
"""Emit SQL that imports the NSC school performance CSV into Postgres (marks schema).

Reads the Department of Basic Education NSC school performance report and upserts
every school plus its 2022-2024 performance rows into `marks."Schools"` and
`marks."SchoolPerformances"`.

SQL is written to stdout - pipe it into psql:
    python3 scripts/import_schools_to_db.py | psql ...

Environment:
    CSV_PATH   default <repo root>/2024_nsc_school_performance_report.csv
    QUARANTINE_PATH  optional NEW CSV file for rows with missing/placeholder EMIS
"""

import csv
import os
import sys

DEFAULT_CSV = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..",
    "2024_nsc_school_performance_report.csv",
)
CSV_PATH = os.environ.get("CSV_PATH", DEFAULT_CSV)
YEARS = (2022, 2023, 2024)


def q(value: object) -> str:
    """Quote a value as a SQL string literal."""
    return "'" + str(value).replace("'", "''") + "'"


def to_int(value: object) -> int | None:
    text = str(value or "").strip()
    if text in ("", "NA", "N/A", "-"):
        return None
    try:
        return int(float(text))
    except ValueError:
        return None


def to_float(value: object) -> float | None:
    text = str(value or "").strip()
    if text in ("", "NA", "N/A", "-"):
        return None
    try:
        return float(text)
    except ValueError:
        return None


def main() -> None:
    # Validate all identities before emitting any SQL.
    schools: dict[str, tuple] = {}
    performances: dict[tuple[str, int], tuple] = {}
    seen: dict[str, dict] = {}
    rejected: list[dict] = []
    errors: list[str] = []

    with open(CSV_PATH, newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fields = reader.fieldnames or []
        if "emis_number" not in fields:
            raise ValueError("CSV is missing required column emis_number")
        for line, row in enumerate(reader, start=2):
            emis = (row.get("emis_number") or "").strip()
            if not emis or emis.upper() in ("NA", "N/A", "-") or to_float(emis) == 0:
                print(
                    f"CSV row {line}: rejected placeholder EMIS {emis!r}, "
                    f"centre={row.get('centre_number')!r}, name={row.get('centre_name')!r}",
                    file=sys.stderr,
                )
                rejected.append(row)
                continue
            if emis in seen:
                if row != seen[emis]:
                    errors.append(f"CSV row {line}: conflicting records for EMIS {emis!r}")
                else:
                    print(f"CSV row {line}: identical duplicate EMIS {emis!r}", file=sys.stderr)
                continue
            seen[emis] = row

            schools[emis] = (
                emis,
                (row.get("province") or "").strip(),
                (row.get("district_name") or "").strip(),
                (row.get("centre_number") or "").strip(),
                (row.get("centre_name") or "").strip(),
                to_int(row.get("quintile")) or 0,
            )

            for year in YEARS:
                wrote = to_int(row.get(f"total_wrote_{year}"))
                achieved = to_int(row.get(f"total_achieved_{year}"))
                if wrote is None or achieved is None:
                    continue
                performances[(emis, year)] = (
                    emis,
                    year,
                    to_int(row.get(f"progressed_number_{year}")) or 0,
                    wrote,
                    achieved,
                    to_float(row.get(f"percent_achieved_{year}")) or 0.0,
                )

    quarantine_path = os.environ.get("QUARANTINE_PATH")
    if rejected:
        if quarantine_path:
            # Exclusive creation prevents overwriting a previous quarantine/source.
            with open(quarantine_path, "x", newline="", encoding="utf-8") as handle:
                writer = csv.DictWriter(handle, fieldnames=fields)
                writer.writeheader()
                writer.writerows(rejected)
            print(f"Quarantined {len(rejected)} rows in {quarantine_path}", file=sys.stderr)
        else:
            errors.append(
                f"{len(rejected)} rows have missing/placeholder EMIS. Correct the source "
                "or set QUARANTINE_PATH to a new CSV file to retain them for review."
            )
    if errors:
        raise ValueError("\n".join(errors))
    if not schools:
        print("No valid schools; no SQL emitted", file=sys.stderr)
        return

    print(f"Importing {len(schools)} schools and {len(performances)} performance rows", file=sys.stderr)
    out = sys.stdout
    out.write("BEGIN;\n")

    school_values = ",\n".join(
        f"(gen_random_uuid(),{q(province)},{q(district)},{q(emis)},{q(centre_number)},"
        f"{q(centre_name)},{quintile},now(),false)"
        for emis, province, district, centre_number, centre_name, quintile in schools.values()
    )
    out.write(
        'INSERT INTO marks."Schools" '
        '("Id","Province","DistrictName","EmisNumber","CentreNumber","CentreName","Quintile","CreatedAt","IsDeleted")\n'
        "VALUES\n" + school_values + "\n"
        'ON CONFLICT ("EmisNumber") DO UPDATE SET '
        '"Province"=EXCLUDED."Province","DistrictName"=EXCLUDED."DistrictName",'
        '"CentreNumber"=EXCLUDED."CentreNumber","CentreName"=EXCLUDED."CentreName",'
        '"Quintile"=EXCLUDED."Quintile","UpdatedAt"=now();\n'
    )

    performance_values = ",\n".join(
        f"({q(emis)},{year},{progressed},{wrote},{achieved},{percent})"
        for emis, year, progressed, wrote, achieved, percent in performances.values()
    )
    if not performances:
        out.write("COMMIT;\n")
        return
    out.write(
        "WITH data(emis, year, progressed, wrote, achieved, percent) AS (VALUES\n"
        + performance_values
        + "\n)\n"
        'INSERT INTO marks."SchoolPerformances" '
        '("Id","SchoolId","Year","ProgressedNumber","TotalWrote","TotalAchieved","PercentAchieved","CreatedAt","IsDeleted")\n'
        'SELECT gen_random_uuid(), s."Id", d.year, d.progressed, d.wrote, d.achieved, d.percent, now(), false\n'
        'FROM data d JOIN marks."Schools" s ON s."EmisNumber" = d.emis\n'
        'ON CONFLICT ("SchoolId","Year") DO UPDATE SET '
        '"ProgressedNumber"=EXCLUDED."ProgressedNumber","TotalWrote"=EXCLUDED."TotalWrote",'
        '"TotalAchieved"=EXCLUDED."TotalAchieved","PercentAchieved"=EXCLUDED."PercentAchieved",'
        '"UpdatedAt"=now();\n'
    )

    out.write("COMMIT;\n")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(f"Import refused: {error}", file=sys.stderr)
        sys.exit(1)
