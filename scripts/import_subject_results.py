#!/usr/bin/env python3
"""Import the extracted NSC subject results into the marks schema (Postgres).

Reads data/nsc/matric.db (built by scripts/extract_subject_results.py) and
upserts one SchoolSubjectPerformances row per (EmisNumber, Subject, Year).
Schools are matched on EmisNumber through the centre -> EMIS mapping captured in
the SQLite `schools` table; when that mapping is missing, or the mapped EMIS
number is absent from the serving store, the school's CentreNumber is used as a
fallback. Centres that no longer exist in marks."Schools" (historic numbering)
are skipped.

Upserts are idempotent: re-running refreshes counts and percentages.

SQL is written to stdout - pipe it into psql:
    python3 scripts/import_subject_results.py | psql ...

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

# Rows per INSERT statement: large enough for speed, small enough for readable
# SQL and bounded statement size (752k rows => ~1500 statements).
BATCH_SIZE = 500


def q(value: object) -> str:
    """Quote a value as a SQL string literal."""
    return "'" + str(value).replace("'", "''") + "'"


def load_subjects(connection: sqlite3.Connection) -> list[tuple[str, str]]:
    return connection.execute(
        "SELECT code, name FROM subjects ORDER BY subject_id"
    ).fetchall()


def load_results(connection: sqlite3.Connection) -> list[tuple]:
    """Subject results joined to the centre -> EMIS mapping."""
    return connection.execute(
        """
        SELECT sr.centre_number,
               COALESCE(m.emis_number, ''),
               sub.code,
               sr.year,
               sr.total_wrote,
               sr.achieved,
               sr.percent_achieved
        FROM subject_results sr
        JOIN subjects sub ON sub.subject_id = sr.subject_id
        LEFT JOIN (
            SELECT centre_number, MIN(emis_number) AS emis_number
            FROM schools
            WHERE emis_number IS NOT NULL AND emis_number <> ''
            GROUP BY centre_number
        ) m ON m.centre_number = sr.centre_number
        ORDER BY sr.centre_number, sr.year, sub.code
        """
    ).fetchall()


def emit_subjects(out, subjects: list[tuple[str, str]]) -> None:
    values = ", ".join(
        f"(gen_random_uuid(),{q(code)},{q(name)},true,now(),false)"
        for code, name in subjects
    )
    out.write(
        'INSERT INTO marks."Subjects" '
        '("Id","Code","Name","IsGateway","CreatedAt","IsDeleted") VALUES\n'
        + values
        + "\n"
        'ON CONFLICT ("Code") DO UPDATE SET "Name"=EXCLUDED."Name",'
        '"IsGateway"=EXCLUDED."IsGateway","UpdatedAt"=now();\n'
    )


def emit_batch(out, batch: list[tuple]) -> None:
    values = []
    for centre, emis, subject, year, wrote, achieved, percent in batch:
        achieved_literal = "NULL::integer" if achieved is None else str(int(achieved))
        percent_literal = "NULL::numeric" if percent is None else repr(float(percent))
        values.append(
            f"({q(centre)},{q(emis)},{q(subject)},{int(year)},"
            f"{int(wrote or 0)},{achieved_literal},{percent_literal})"
        )
    out.write(
        "INSERT INTO subject_import_rows(centre, emis, subject, year, wrote, achieved, percent) VALUES\n"
        + ",\n".join(values) + ";\n"
    )


def emit_aggregated_results(out) -> None:
    """Resolve all centres before grouping, including collisions across batches."""
    out.write('''WITH centre_map AS (
    SELECT "CentreNumber" AS centre, MIN("Id"::text)::uuid AS school_id
    FROM marks."Schools"
    WHERE NOT "IsDeleted" AND "CentreNumber" IS NOT NULL AND "CentreNumber" <> ''
    GROUP BY "CentreNumber" HAVING COUNT(*) = 1
), resolved AS (
    SELECT COALESCE(s."Id", cm.school_id) AS school_id, sub."Id" AS subject_id, d.*
    FROM subject_import_rows d
    LEFT JOIN marks."Schools" s ON d.emis <> '' AND s."EmisNumber" = d.emis AND NOT s."IsDeleted"
    LEFT JOIN centre_map cm ON cm.centre = d.centre
    JOIN marks."Subjects" sub ON sub."Code" = d.subject AND NOT sub."IsDeleted"
    WHERE COALESCE(s."Id", cm.school_id) IS NOT NULL
)
INSERT INTO marks."SchoolSubjectPerformances"
    ("Id","SchoolId","SubjectId","Year","TotalWrote","Achieved","PercentAchieved","CreatedAt","IsDeleted")
SELECT gen_random_uuid(), school_id, subject_id, year, SUM(wrote),
    CASE WHEN COUNT(*) FILTER (WHERE wrote > 0 AND achieved IS NULL) = 0
         THEN SUM(achieved) ELSE NULL END,
    CASE WHEN SUM(wrote) > 0 AND COUNT(*) FILTER (WHERE wrote > 0 AND achieved IS NULL AND percent IS NULL) = 0
         THEN ROUND(100.0 * SUM(COALESCE(achieved::numeric, percent * wrote / 100.0)) / SUM(wrote), 1)
         ELSE NULL END,
    now(), false
FROM resolved
GROUP BY school_id, subject_id, year
ON CONFLICT ("SchoolId","SubjectId","Year") DO UPDATE SET
    "TotalWrote"=EXCLUDED."TotalWrote", "Achieved"=EXCLUDED."Achieved",
    "PercentAchieved"=EXCLUDED."PercentAchieved", "IsDeleted"=false, "UpdatedAt"=now();
''')


def main() -> None:
    connection = sqlite3.connect(DB_PATH)
    subjects = load_subjects(connection)
    rows = load_results(connection)
    connection.close()

    if not subjects or not rows:
        raise ValueError(f"{DB_PATH}: no subject results to import")

    mapped = sum(1 for row in rows if row[1])

    out = sys.stdout
    out.write("BEGIN;\n")
    emit_subjects(out, subjects)
    out.write("CREATE TEMP TABLE subject_import_rows (centre text, emis text, subject text, "
              "year integer, wrote integer, achieved integer, percent numeric) ON COMMIT DROP;\n")
    for start in range(0, len(rows), BATCH_SIZE):
        emit_batch(out, rows[start : start + BATCH_SIZE])
    emit_aggregated_results(out)
    out.write("COMMIT;\n")

    centres = {row[0] for row in rows}
    print(
        f"Prepared {len(rows)} subject results for {len(centres)} centres "
        f"({mapped} rows with an EMIS mapping, {len(rows) - mapped} by centre fallback)",
        file=sys.stderr,
    )


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(f"Import refused: {error}", file=sys.stderr)
        sys.exit(1)
