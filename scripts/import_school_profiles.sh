#!/usr/bin/env bash
#
# MatricDashboard - import school contact details and enrolment counts.
#
# Updates `marks."Schools"` with email, telephone, ownership, Section 21,
# no-fee, urban/rural, sector, and phase values from the DBE EMIS masterlist,
# and upserts one `marks."SchoolEnrolments"` row per school for the masterlist's
# data year. Only schools already in `marks."Schools"` are touched. Idempotent:
# re-running upserts by EMIS number and (SchoolId, Year).
#
# Usage:
#   ./scripts/import_school_profiles.sh
#   DBE_DIR=/path/to/dbe-emis-downloads ./scripts/import_school_profiles.sh
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

echo "Importing school profiles into $PG_CONTAINER (db=$PG_DB, user=$PG_USER) ..."
python3 "$SCRIPT_DIR/import_school_profiles.py" \
    | docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
        psql -v ON_ERROR_STOP=1 -q -U "$PG_USER" -d "$PG_DB"

counts="$(docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" psql -U "$PG_USER" -d "$PG_DB" -tAc \
    'SELECT count(*) FILTER (WHERE "MasterlistDataYear" IS NOT NULL), count(*) FROM marks."Schools"' \
    2>/dev/null)"
echo "Done. with_profile,total_schools = $counts"
