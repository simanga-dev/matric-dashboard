#!/usr/bin/env bash
#
# MatricDashboard - apply cleaned school names to SQLite and Postgres.
#
# Updates data/nsc/matric.db in place (after a backup), then applies the same
# names to marks."Schools"."CentreName" and geo.school_locations.name by EMIS.
# Placeholder EMIS values are skipped. Re-running is safe.
#
# Usage:
#   ./scripts/apply_clean_school_names.sh
#   PG_CONTAINER=db-name ./scripts/apply_clean_school_names.sh
#
# Auto-detects the running PostGIS/PostgreSQL container, credentials, and
# database. Override with PG_CONTAINER / PG_USER / PG_DB / PG_PASSWORD.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

find_container() {
    docker ps --format '{{.ID}} {{.Image}}' | grep -i "$1" | head -1 | awk '{print $1}'
}

container_env() {
    docker exec "$1" printenv "$2" 2>/dev/null | tr -d '\r' || true
}

PG_CONTAINER="${PG_CONTAINER:-$(find_container 'postgis\|postgres')}"
if [ -z "$PG_CONTAINER" ]; then
    echo "No running PostgreSQL/PostGIS container found." >&2
    exit 1
fi

PG_USER="${PG_USER:-$(container_env "$PG_CONTAINER" POSTGRES_USER)}"
PG_USER="${PG_USER:-postgres}"
PG_PASSWORD="${PG_PASSWORD:-$(container_env "$PG_CONTAINER" POSTGRES_PASSWORD)}"

if [ -z "${PG_DB:-}" ]; then
    PG_DB="$(docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" psql -U "$PG_USER" -d postgres -tAc \
        "SELECT datname FROM pg_database WHERE datistemplate=false AND datname <> 'postgres' ORDER BY datname LIMIT 1" \
        2>/dev/null | tr -d '[:space:]')"
fi
PG_DB="${PG_DB:-postgres}"

echo "Applying cleaned school names to SQLite and $PG_CONTAINER (db=$PG_DB, user=$PG_USER) ..."
python3 "$SCRIPT_DIR/apply_clean_school_names.py" \
    | docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
        psql -v ON_ERROR_STOP=1 -q -U "$PG_USER" -d "$PG_DB"

counts="$(docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -tAc \
    'SELECT (SELECT count(*) FROM marks."Schools"), (SELECT count(*) FROM geo.school_locations)' \
    2>/dev/null)"
echo "Done. schools,locations = $counts"
