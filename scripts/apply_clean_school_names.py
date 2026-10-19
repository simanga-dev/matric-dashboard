#!/usr/bin/env python3
"""Apply cleaned school names to SQLite and prepare the Postgres update.

Reads `data/nsc/school_names_clean.csv`, extends it with names from the DBE
National masterlist for schools that are not in the report, and updates
`data/nsc/matric.db` in one transaction after making a backup. Postgres SQL is
written to stdout and updates both `marks."Schools"."CentreName"` and
`geo.school_locations.name`.

Placeholder EMIS values are skipped. The source CSV and masterlist are never
modified.

SQL is written to stdout - pipe it into psql:
    python3 scripts/apply_clean_school_names.py | psql ...

Environment:
    CLEAN_CSV_PATH   default <repo root>/data/nsc/school_names_clean.csv
    NSC_DB_PATH      default <repo root>/data/nsc/matric.db
    DBE_DIR          default <home>/Documents/dbe-emis-downloads
    DBE_PRIMARY      default <DBE_DIR>/National.xlsx
    AUDIT_PATH       default <repo root>/data/nsc/school_name_updates.csv
    BACKUP_DIR       default <repo root>/data/nsc/backups
"""

import csv
import os
import sqlite3
import sys
import time

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import clean_school_names as names
import import_school_locations as locations

ROOT = os.path.normpath(os.path.join(SCRIPT_DIR, ".."))
DEFAULT_CLEAN_CSV = os.path.join(ROOT, "data", "nsc", "school_names_clean.csv")
DEFAULT_DB = os.path.join(ROOT, "data", "nsc", "matric.db")
DEFAULT_DBE_DIR = os.path.join(os.path.expanduser("~"), "Documents", "dbe-emis-downloads")
DEFAULT_PRIMARY = "National.xlsx"
DEFAULT_AUDIT = os.path.join(ROOT, "data", "nsc", "school_name_updates.csv")
DEFAULT_BACKUP_DIR = os.path.join(ROOT, "data", "nsc", "backups")

CLEAN_CSV_PATH = os.environ.get("CLEAN_CSV_PATH", DEFAULT_CLEAN_CSV)
DB_PATH = os.environ.get("NSC_DB_PATH", DEFAULT_DB)
DBE_DIR = os.environ.get("DBE_DIR", DEFAULT_DBE_DIR)
PRIMARY_PATH = os.environ.get("DBE_PRIMARY", os.path.join(DBE_DIR, DEFAULT_PRIMARY))
AUDIT_PATH = os.environ.get("AUDIT_PATH", DEFAULT_AUDIT)
BACKUP_DIR = os.environ.get("BACKUP_DIR", DEFAULT_BACKUP_DIR)


def q(value: object) -> str:
    """Quote a value as a SQL string literal."""
    return "'" + str(value).replace("'", "''") + "'"


def load_clean_names(path: str) -> dict[str, str]:
    """Read cleaned names from the export, skipping placeholder EMIS values."""
    with open(path, newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fields = reader.fieldnames or []
        for required in ("emis_number", "clean_school_name"):
            if required not in fields:
                raise ValueError(f"{path}: missing required column {required}")
        cleaned: dict[str, str] = {}
        for row in reader:
            emis = names.collapse(row.get("emis_number"))
            name = names.collapse(row.get("clean_school_name"))
            if locations._is_placeholder_emis(emis) or not name:
                continue
            cleaned[emis] = name
    return cleaned


def load_master_names(path: str) -> dict[str, str]:
    """Read and normalize every usable official name from the masterlist."""
    cleaned: dict[str, str] = {}
    for emis, record in names.read_masterlist_names(path).items():
        if locations._is_placeholder_emis(emis):
            continue
        clean = names.normalize_school_name(record["name"], record.get("phase"))
        if clean:
            cleaned[emis] = clean
    return cleaned


def build_mapping(clean_csv: str, masterlist: str) -> dict[str, tuple[str, str]]:
    """Return EMIS -> (clean name, source), preferring the cleaned report export."""
    mapping = {
        emis: (name, "cleaned report export")
        for emis, name in load_clean_names(clean_csv).items()
    }
    for emis, name in load_master_names(masterlist).items():
        mapping.setdefault(emis, (name, "DBE National masterlist"))
    return mapping


def read_sqlite_schools(path: str) -> dict[str, str]:
    """Read current SQLite school names."""
    with sqlite3.connect(path) as connection:
        return {
            str(emis): name or ""
            for emis, name in connection.execute(
                "SELECT emis_number, centre_name FROM schools"
            )
        }


def backup_sqlite(path: str, backup_dir: str) -> str:
    """Copy the SQLite database with the backup API before changing it."""
    os.makedirs(backup_dir, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    backup_path = os.path.join(backup_dir, f"{os.path.basename(path)}.{stamp}.bak")
    with sqlite3.connect(path) as source, sqlite3.connect(backup_path) as target:
        source.backup(target)
    return backup_path


def sqlite_updates(
    schools: dict[str, str], mapping: dict[str, tuple[str, str]]
) -> list[tuple[str, str, str, str]]:
    """Return (EMIS, old name, new name, source) for changed SQLite rows."""
    updates: list[tuple[str, str, str, str]] = []
    for emis, (clean, source) in mapping.items():
        old = schools.get(emis)
        if old is not None and old != clean:
            updates.append((emis, old, clean, source))
    return updates


def apply_sqlite(path: str, updates: list[tuple[str, str, str, str]]) -> int:
    """Apply SQLite name changes in one transaction and return the row count."""
    if not updates:
        return 0
    with sqlite3.connect(path) as connection:
        before = connection.total_changes
        connection.executemany(
            "UPDATE schools SET centre_name = ? WHERE emis_number = ?",
            [(new, emis) for emis, _, new, _ in updates],
        )
        connection.commit()
        return connection.total_changes - before


def write_audit(path: str, updates: list[tuple[str, str, str, str]]) -> None:
    """Write changed SQLite names so the update can be reviewed or reversed."""
    parent = os.path.dirname(os.path.abspath(path))
    os.makedirs(parent, exist_ok=True)
    with open(path, "w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.writer(handle)
        writer.writerow(["database", "emis_number", "old_name", "new_name", "source"])
        for emis, old, new, source in updates:
            writer.writerow(["sqlite", emis, old, new, source])


def postgres_sql(mapping: dict[str, tuple[str, str]]) -> str:
    """Build a transactional Postgres update for both name-bearing tables."""
    if not mapping:
        return ""
    values = ",\n".join(
        f"({q(emis)},{q(name)})" for emis, (name, _) in sorted(mapping.items())
    )
    return (
        "BEGIN;\n"
        "CREATE TEMP TABLE clean_school_names (emis text PRIMARY KEY, name text NOT NULL)\n"
        "ON COMMIT DROP;\n"
        "INSERT INTO clean_school_names (emis, name) VALUES\n"
        + values
        + ";\n"
        'UPDATE marks."Schools" s\n'
        'SET "CentreName" = d.name, "UpdatedAt" = now()\n'
        "FROM clean_school_names d\n"
        'WHERE s."EmisNumber" = d.emis\n'
        '  AND s."CentreName" IS DISTINCT FROM d.name;\n'
        "UPDATE geo.school_locations g\n"
        "SET name = d.name\n"
        "FROM clean_school_names d\n"
        "WHERE g.emis_number = d.emis\n"
        "  AND g.name IS DISTINCT FROM d.name;\n"
        "COMMIT;\n"
    )


def main() -> None:
    if not os.path.exists(DB_PATH):
        raise ValueError(f"SQLite database not found at {DB_PATH}")
    schools = read_sqlite_schools(DB_PATH)
    clean_emis = set(load_clean_names(CLEAN_CSV_PATH))
    target_emis = set(schools) | clean_emis
    mapping = {
        emis: value
        for emis, value in build_mapping(CLEAN_CSV_PATH, PRIMARY_PATH).items()
        if emis in target_emis
    }
    if not mapping:
        raise ValueError("No cleaned school names found")

    updates = sqlite_updates(schools, mapping)
    covered = sum(1 for emis in schools if emis in mapping)

    backup_path = backup_sqlite(DB_PATH, BACKUP_DIR)
    changed = apply_sqlite(DB_PATH, updates)
    if updates:
        write_audit(AUDIT_PATH, updates)

    print(
        f"SQLite: updated {changed} of {covered} covered schools "
        f"({len(schools)} total); backup {backup_path}",
        file=sys.stderr,
    )
    print(
        f"Audit: {AUDIT_PATH}" if updates else "Audit: no SQLite changes, existing file left unchanged",
        file=sys.stderr,
    )
    print(
        f"Postgres SQL: {len(mapping)} names ready for marks.\"Schools\" and "
        "geo.school_locations",
        file=sys.stderr,
    )
    sql = postgres_sql(mapping)
    if sql:
        sys.stdout.write(sql)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError, sqlite3.Error) as error:
        print(f"School name update refused: {error}", file=sys.stderr)
        sys.exit(1)
