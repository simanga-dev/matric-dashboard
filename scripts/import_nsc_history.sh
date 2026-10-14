#!/usr/bin/env bash
#
# MatricDashboard - import extracted NSC results history into the marks schema.
#
# Reads data/nsc/matric.db (from scripts/extract_nsc_results.py) and upserts
# one SchoolPerformances row per (EmisNumber, Year), extending history to
# 2018-2025. Idempotent: re-running upserts by (SchoolId, Year).
#
# Usage:
#   ./scripts/import_nsc_history.sh
#   NSC_DB_PATH=/path/to/matric.db ./scripts/import_nsc_history.sh
#
# Auto-detects the running PostGIS/PostgreSQL container, credentials and
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

echo "Importing NSC results history into $PG_CONTAINER (db=$PG_DB, user=$PG_USER) ..."
python3 "$SCRIPT_DIR/import_nsc_history.py" \
    | docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
        psql -v ON_ERROR_STOP=1 -q -U "$PG_USER" -d "$PG_DB"

counts="$(docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -tAc \
    'SELECT count(*), count(DISTINCT "Year") FROM marks."SchoolPerformances"' 2>/dev/null)"
echo "Done. performances,years = $counts"
