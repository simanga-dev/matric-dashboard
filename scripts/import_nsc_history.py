#!/usr/bin/env python3
"""Import the extracted NSC results history into the marks schema (Postgres).

Reads data/nsc/matric.db (built by scripts/extract_nsc_results.py) and
upserts one SchoolPerformances row per (EmisNumber, Year). Schools are matched
on EmisNumber; results for schools absent from marks."Schools" are skipped and
counted, because the extracted history carries no province/district/quintile.
Missing pass counts are recovered only when the reported rounded percentage
identifies exactly one count. Other incomplete rows are skipped with a diagnostic
so they cannot overwrite known results in the serving store.

SQL is written to stdout - pipe it into psql:
    python3 scripts/import_nsc_history.py | psql ...

Environment:
    NSC_DB_PATH  default <repo>/data/nsc/matric.db
"""

import os
import sqlite3
import sys
from decimal import Decimal, ROUND_HALF_UP

DEFAULT_DB = os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "data", "nsc", "matric.db"
)
DB_PATH = os.environ.get("NSC_DB_PATH", DEFAULT_DB)


def q(value: object) -> str:
    """Quote a value as a SQL string literal."""
    return "'" + str(value).replace("'", "''") + "'"


def complete_result(wrote: int, achieved: int | None, percent: float | None) -> tuple[int, float] | None:
    """Recover missing metrics only when the counts are unambiguous."""
    if wrote < 0 or (percent is not None and not 0 <= percent <= 100):
        return None
    if achieved is None:
        if wrote == 0:
            achieved = 0
        elif percent is None:
            return None
        else:
            rate = Decimal(str(percent))
            estimate = int((rate * wrote / 100).to_integral_value(rounding=ROUND_HALF_UP))
            candidates = [
                count for count in range(max(0, estimate - 1), min(wrote, estimate + 1) + 1)
                if (Decimal(count) * 100 / wrote).quantize(Decimal("0.1"), rounding=ROUND_HALF_UP) == rate
            ]
            if len(candidates) != 1:
                return None
            achieved = candidates[0]
    if not 0 <= achieved <= wrote:
        return None
    if percent is None:
        percent = round(achieved / wrote * 100, 1) if wrote else 0.0
    return achieved, percent


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

    imported = []
    values = []
    for emis, year, progressed, wrote, achieved, percent in rows:
        result = complete_result(wrote, achieved, percent)
        if result is None:
            print(f"Skipping incomplete or invalid result: EMIS {emis}, year {year}", file=sys.stderr)
            continue
        achieved, percent = result
        imported.append((emis, year))
        values.append(
            f"({q(emis)},{int(year)},{int(progressed or 0)},{int(wrote)},"
            f"{achieved},{float(percent)})"
        )

    if not values:
        raise ValueError(f"{DB_PATH}: no complete results to import")

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

    years = sorted({year for _, year in imported})
    number_literals = {emis for emis, _ in imported}
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
