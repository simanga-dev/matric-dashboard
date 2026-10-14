#!/usr/bin/env python3
"""Import the extracted NSC results history into the marks schema (Postgres).

Reads data/nsc/matric.db (built by scripts/extract_nsc_results.py) and
upserts one SchoolPerformances row per (EmisNumber, Year). Schools are matched
on EmisNumber; results for schools absent from marks."Schools" are skipped and
counted, because the extracted history carries no province/district/quintile.

SQL is written to stdout - pipe it into psql:
    python3 scripts/import_nsc_history.py | psql ...

Environment:
    NSC_DB_PATH  default <repo>/data/nsc/matric.db
"""

import os
import sqlite3
import sys

DEFAULT_DB = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "data", "nsc", "matric.db"
)
DB_PATH = os.environ.get("NSC_DB_PATH", DEFAULT_DB)


def q(value: object) -> str:
    """Quote a value as a SQL string literal."""
    return "'" + str(value).replace("'", "''") + "'"


def main() -> None:
    connection = sqlite3.connect(DB_PATH)
    rows = connection.execute(
        """
        SELECT emis_number, year, progressed, total_wrote, total_achieved, percent_achieved
        FROM results
        WHERE total_wrote IS NOT NULL
        ORDER BY emis_number, year
        """
    ).fetchall()
    connection.close()

    if not rows:
        raise ValueError(f"{DB_PATH}: no results to import")

    number_literals = {e for e, _, _, _, _, _ in rows}
    values = []
    for emis, year, progressed, wrote, achieved, percent in rows:
        values.append(
            f"({q(emis)},{int(year)},{int(progressed or 0)},{int(wrote)},"
            f"{int(achieved or 0)},{float(percent or 0)})"
        )

    out = sys.stdout
    out.write("BEGIN;\n")
    out.write(
        "WITH data(emis, year, progressed, wrote, achieved, percent) AS (VALUES\n"
        + ",\n".join(values)
        + "\n)\n"
        'INSERT INTO marks."SchoolPerformances" '
        '("Id","SchoolId","Year","ProgressedNumber","TotalWrote","TotalAchieved",'
        '"PercentAchieved","CreatedAt","IsDeleted")\n'
        'SELECT gen_random_uuid(), s."Id", d.year, d.progressed, d.wrote, d.achieved, '
        'd.percent, now(), false\n'
        'FROM data d JOIN marks."Schools" s ON s."EmisNumber" = d.emis\n'
        'ON CONFLICT ("SchoolId","Year") DO UPDATE SET '
        '"ProgressedNumber"=EXCLUDED."ProgressedNumber","TotalWrote"=EXCLUDED."TotalWrote",'
        '"TotalAchieved"=EXCLUDED."TotalAchieved","PercentAchieved"=EXCLUDED."PercentAchieved",'
        '"IsDeleted"=false,"UpdatedAt"=now();\n'
    )
    out.write("COMMIT;\n")

    years = sorted({row[1] for row in rows})
    print(
        f"Prepared {len(values)} results for {len(number_literals)} schools "
        f"({years[0]}-{years[-1]})",
        file=sys.stderr,
    )


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(f"Import refused: {error}", file=sys.stderr)
        sys.exit(1)
