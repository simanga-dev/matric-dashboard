#!/usr/bin/env bash
#
# MatricDashboard - GeoServer bootstrap
#
# Seeds the PostGIS sample layer and configures GeoServer to publish it:
#   1. applies scripts/postgis/init.sql to the running PostGIS container
#   2. creates the workspace, PostGIS data store, and feature type via REST
#   3. verifies the layer responds to a WFS GetFeature request
#
# Works against both the Aspire dev stack and the docker-compose stack by
# auto-detecting the PostGIS/GeoServer containers, database credentials, the
# GeoServer published port, and the network host name. Every value can be
# overridden with an environment variable.
#
# Idempotent: existing workspace/store/layer are kept, bounds are refreshed.
#
# Usage:
#   ./scripts/geoserver/bootstrap.sh
#
# Environment overrides:
#   GEOSERVER_URL              default auto-detected (http://localhost:<port>/geoserver)
#   GEOSERVER_ADMIN_USER       default from container env, else "admin"
#   GEOSERVER_ADMIN_PASSWORD   default from container env, else from .env
#   GEOSERVER_WORKSPACE        default matric
#   GEOSERVER_DATASTORE        default schools
#   GEOSERVER_LAYERS           default "school_locations school_locations_effective school_footprints school_boundaries provinces districts"
#   PG_CONTAINER               default auto-detected running postgis container
#   PG_HOST                    default auto-detected network alias (compose: postgres, Aspire: db)
#   PG_PORT                    default 5432
#   PG_USER / PG_DB / PG_PASSWORD  default auto-detected from the container env
#   PG_SCHEMA                  default geo

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="${ENV_FILE:-$REPO_ROOT/.env}"

read_env() {
    [ -f "$ENV_FILE" ] || return 0
    grep -E "^$1=" "$ENV_FILE" | head -1 | cut -d= -f2- || true
}

find_container() {
    # $1 = image substring; returns the container id of the first match.
    docker ps --format '{{.ID}} {{.Image}}' | grep -i "$1" | head -1 | awk '{print $1}'
}

container_env() {
    docker exec "$1" printenv "$2" 2>/dev/null | tr -d '\r' || true
}

published_host_port() {
    # $1 = container, $2 = internal port; returns the host port.
    docker port "$1" "$2" 2>/dev/null | head -1 | sed 's/.*://' || true
}

network_alias() {
    # Shortest non-hex network alias, e.g. "postgres" (compose) or "db" (Aspire).
    docker inspect -f \
        '{{range $net, $conf := .NetworkSettings.Networks}}{{range $conf.Aliases}}{{println .}}{{end}}{{end}}' \
        "$1" 2>/dev/null \
        | grep -vE '^[0-9a-f]{16,}$' \
        | awk 'NF { print length, $0 }' \
        | sort -n | head -1 | cut -d' ' -f2- || true
}

command -v jq >/dev/null || { echo "jq is required" >&2; exit 1; }
command -v curl >/dev/null || { echo "curl is required" >&2; exit 1; }

WS="${GEOSERVER_WORKSPACE:-matric}"
DS="${GEOSERVER_DATASTORE:-schools}"
LAYERS="${GEOSERVER_LAYERS:-school_locations school_locations_effective school_footprints school_boundaries provinces districts}"
PG_SCHEMA="${PG_SCHEMA:-geo}"
PG_PORT="${PG_PORT:-5432}"

# Identifiers are interpolated into maintenance SQL below, never user SQL.
if [[ ! "$PG_SCHEMA" =~ ^[a-z_][a-z0-9_]*$ ]]; then
    echo "PG_SCHEMA must be a lowercase PostgreSQL identifier" >&2
    exit 1
fi
for LAYER in $LAYERS; do
    if [[ ! "$LAYER" =~ ^[a-z_][a-z0-9_]*$ ]]; then
        echo "Invalid layer identifier: $LAYER" >&2
        exit 1
    fi
done

# ── 1. Locate containers ─────────────────────────────────────────────────────
PG_CONTAINER="${PG_CONTAINER:-$(find_container 'postgis')}"
if [ -z "$PG_CONTAINER" ]; then
    echo "No running PostGIS container found." >&2
    echo "Start the stack first (Aspire: 'just dev'; compose: 'just geo-up')." >&2
    exit 1
fi

GS_CONTAINER="${GS_CONTAINER:-$(find_container 'geoserver')}"
if [ -z "$GS_CONTAINER" ]; then
    echo "No running GeoServer container found." >&2
    echo "Start the stack first (Aspire: 'just dev'; compose: 'just geo-up')." >&2
    exit 1
fi

# ── 2. Resolve database + GeoServer connection details ───────────────────────
PG_USER="${PG_USER:-$(container_env "$PG_CONTAINER" POSTGRES_USER)}"
PG_USER="${PG_USER:-postgres}"
PG_PASSWORD="${PG_PASSWORD:-$(container_env "$PG_CONTAINER" POSTGRES_PASSWORD)}"
PG_HOST="${PG_HOST:-$(network_alias "$PG_CONTAINER")}"
PG_HOST="${PG_HOST:-postgres}"

if [ -z "${PG_DB:-}" ]; then
    PG_DB="$(docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" psql -U "$PG_USER" -d postgres -tAc \
        "SELECT datname FROM pg_database WHERE datistemplate=false AND datname <> 'postgres' ORDER BY datname LIMIT 1" \
        2>/dev/null | tr -d '[:space:]')"
fi
PG_DB="${PG_DB:-${POSTGRES_DB:-}}"

GS_PORT="${GEOSERVER_PORT:-$(published_host_port "$GS_CONTAINER" 8080)}"
GS_URL="${GEOSERVER_URL:-http://localhost:${GS_PORT:-8080}/geoserver}"
GS_USER="${GEOSERVER_ADMIN_USER:-$(container_env "$GS_CONTAINER" GEOSERVER_ADMIN_USER)}"
GS_USER="${GS_USER:-admin}"
GS_PASS="${GEOSERVER_ADMIN_PASSWORD:-$(container_env "$GS_CONTAINER" GEOSERVER_ADMIN_PASSWORD)}"
GS_PASS="${GS_PASS:-$(read_env GEOSERVER_ADMIN_PASSWORD)}"
GS_PASS="${GS_PASS:-geoserver}"

echo "Detected:"
echo "  PostGIS container : $PG_CONTAINER ($PG_HOST:$PG_PORT, db=$PG_DB, user=$PG_USER)"
echo "  GeoServer         : $GS_CONTAINER ($GS_URL, user=$GS_USER)"

# ── 3. Seed PostGIS ──────────────────────────────────────────────────────────
echo "Seeding PostGIS ..."
docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
    psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" \
    < "$SCRIPT_DIR/../postgis/init.sql" >/dev/null
echo "  sample layer seeded"

# The API migration owns correction tables/views. Never create a second copy of
# those definitions here, and fail clearly if deployment skipped the migration.
SPATIAL_VIEWS=""
for LAYER in $LAYERS; do
    case "$LAYER" in
        school_locations_effective|school_footprints|school_boundaries)
            exists="$(docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" \
                psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -tAc \
                "SELECT to_regclass('$PG_SCHEMA.$LAYER') IS NOT NULL;")"
            if [ "$(echo "$exists" | tr -d '[:space:]')" != "t" ]; then
                echo "Missing $PG_SCHEMA.$LAYER. Apply the API spatial migration before GeoServer bootstrap." >&2
                exit 1
            fi
            SPATIAL_VIEWS="$SPATIAL_VIEWS $LAYER"
            ;;
    esac
done

# GeoServer cannot infer a primary key for a PostgreSQL view. Register the
# school UUID as an assigned key so feature IDs remain stable between reads.
if [ -n "$SPATIAL_VIEWS" ]; then
    docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
        psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" <<SQL >/dev/null
CREATE TABLE IF NOT EXISTS $PG_SCHEMA.gt_pk_metadata (
    table_schema varchar(32) NOT NULL,
    table_name varchar(64) NOT NULL,
    pk_column varchar(64) NOT NULL,
    pk_column_idx integer NOT NULL,
    pk_policy varchar(32),
    pk_sequence varchar(64),
    UNIQUE (table_schema, table_name, pk_column)
);
SQL
    for LAYER in $SPATIAL_VIEWS; do
        docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" \
            psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -c \
            "INSERT INTO $PG_SCHEMA.gt_pk_metadata (table_schema,table_name,pk_column,pk_column_idx,pk_policy)
             VALUES ('$PG_SCHEMA','$LAYER','id',1,'assigned')
             ON CONFLICT (table_schema,table_name,pk_column) DO UPDATE
             SET pk_column_idx=EXCLUDED.pk_column_idx,pk_policy=EXCLUDED.pk_policy;" >/dev/null
    done
fi

# ── 4. Wait for GeoServer REST ───────────────────────────────────────────────
echo "Waiting for GeoServer at $GS_URL ..."
ready=""
for _ in $(seq 1 80); do
    if curl -sf -u "$GS_USER:$GS_PASS" "$GS_URL/rest/about/version.json" >/dev/null 2>&1; then
        ready="yes"
        break
    fi
    sleep 3
done
[ -n "$ready" ] || { echo "GeoServer not reachable at $GS_URL" >&2; exit 1; }
echo "  GeoServer is up"

rest_status() {
    curl -s -o /dev/null -w '%{http_code}' -u "$GS_USER:$GS_PASS" "$1"
}

rest_post() {
    curl -s -o /dev/null -w '%{http_code}' -u "$GS_USER:$GS_PASS" \
        -H 'Content-Type: application/json' -X POST "$1" -d "$2"
}

# ── 5. Workspace ─────────────────────────────────────────────────────────────
if [ "$(rest_status "$GS_URL/rest/workspaces/$WS")" = "404" ]; then
    payload="$(jq -n --arg ws "$WS" '{workspace:{name:$ws}}')"
    echo "Creating workspace '$WS': HTTP $(rest_post "$GS_URL/rest/workspaces" "$payload")"
else
    echo "Workspace '$WS' already exists"
fi

# ── 6. PostGIS data store ────────────────────────────────────────────────────
if [ "$(rest_status "$GS_URL/rest/workspaces/$WS/datastores/$DS")" = "404" ]; then
    payload="$(jq -n \
        --arg name "$DS" --arg host "$PG_HOST" --arg port "$PG_PORT" \
        --arg db "$PG_DB" --arg schema "$PG_SCHEMA" \
        --arg user "$PG_USER" --arg pass "$PG_PASSWORD" --arg spatial "$SPATIAL_VIEWS" \
        '{dataStore:{name:$name,connectionParameters:{entry:[
            {"@key":"dbtype","$":"postgis"},
            {"@key":"host","$":$host},
            {"@key":"port","$":$port},
            {"@key":"database","$":$db},
            {"@key":"schema","$":$schema},
            {"@key":"user","$":$user},
             {"@key":"passwd","$":$pass},
             {"@key":"Expose primary keys","$":"true"}
        ] + (if $spatial != "" then
             [{"@key":"Primary key metadata table","$":($schema + ".gt_pk_metadata")}]
             else [] end)}}}')"
    echo "Creating data store '$DS': HTTP $(rest_post "$GS_URL/rest/workspaces/$WS/datastores" "$payload")"
else
    echo "Data store '$DS' already exists"
    if [ -n "$SPATIAL_VIEWS" ]; then
        # Preserve all existing store settings, including its credentials.
        store="$(curl -sf -u "$GS_USER:$GS_PASS" \
            "$GS_URL/rest/workspaces/$WS/datastores/$DS.json")"
        metadata_table="$(jq -r '[.dataStore.connectionParameters.entry[]
            | select(."@key" == "Primary key metadata table") | ."$"][0] // ""' <<< "$store")"
        case "$metadata_table" in
            ""|gt_pk_metadata|"$PG_SCHEMA.gt_pk_metadata") ;;
            *) echo "Store '$DS' uses another primary-key metadata table. Preserve its existing keys in $PG_SCHEMA.gt_pk_metadata and explicitly configure the store before continuing." >&2; exit 1 ;;
        esac
        payload="$(jq --arg table "$PG_SCHEMA.gt_pk_metadata" '
            .dataStore.connectionParameters.entry |=
                (map(select(."@key" != "Primary key metadata table" and ."@key" != "Expose primary keys"))
                 + [{"@key":"Primary key metadata table","$":$table},
                    {"@key":"Expose primary keys","$":"true"}])
            | {dataStore: .dataStore}' <<< "$store")"
        code="$(curl -sS -o /dev/null -w '%{http_code}' -u "$GS_USER:$GS_PASS" \
            -H 'Content-Type: application/json' -X PUT \
            "$GS_URL/rest/workspaces/$WS/datastores/$DS" -d "$payload")"
        case "$code" in
            2??) echo "  view primary-key metadata configured" ;;
            *) echo "Could not configure view primary keys: HTTP $code" >&2; exit 1 ;;
        esac
    fi
fi

# ── 7. Feature types ─────────────────────────────────────────────────────────
# Public tables and views in the geo schema that the map reads over WFS.
for LAYER in $LAYERS; do
    if [ "$(rest_status "$GS_URL/rest/workspaces/$WS/datastores/$DS/featuretypes/$LAYER")" = "404" ]; then
        # Grounds may be empty until the first editor save. Explicit initial
        # bounds allow GeoServer to publish an empty view without guessing.
        payload="$(jq -n --arg name "$LAYER" \
            '{featureType:{name:$name,nativeName:$name,srs:"EPSG:4326",
              nativeBoundingBox:{minx:-180,miny:-90,maxx:180,maxy:90,crs:"EPSG:4326"},
              latLonBoundingBox:{minx:-180,miny:-90,maxx:180,maxy:90,crs:"EPSG:4326"}}}')"
        code="$(rest_post \
            "$GS_URL/rest/workspaces/$WS/datastores/$DS/featuretypes" \
            "$payload")"
        echo "Publishing layer '$WS:$LAYER': HTTP $code"
        case "$code" in
            2??) ;;
            *) echo "Could not publish $WS:$LAYER: HTTP $code" >&2; exit 1 ;;
        esac
    else
        echo "Layer '$WS:$LAYER' already published"
    fi
done

# ── 8. Seed real schools from the API (approximate coordinates) ──────────────
# Replaces the static sample points with the actual schools the app indexes so
# the tree-to-map selection highlight (keyed on EMIS number) has data to match.
# Set SEED_FROM_API=0 to skip.
API_URL="${API_URL:-http://localhost:5179}"
if [ "${SEED_FROM_API:-1}" = "1" ]; then
    if curl -sf "$API_URL/api/v1/dashboard/schools?pageNumber=1&pageSize=1" >/dev/null 2>&1; then
        echo "Seeding schools from API ($API_URL) ..."
        python3 "$SCRIPT_DIR/seed_from_api.py" \
            | docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
                psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" >/dev/null
        echo "  schools seeded (approximate coordinates)"
    else
        echo "API not reachable at $API_URL - keeping static sample points"
    fi
fi

# ── 8b. Import real school coordinates from the DBE EMIS masterlists ─────────
# Overrides the approximate API/init points with measured coordinates keyed on
# EMIS number. Runs only when a masterlist is present; set IMPORT_LOCATIONS=0 to
# skip. See scripts/import_school_locations.py.
DBE_DIR="${DBE_DIR:-$HOME/Documents/dbe-emis-downloads}"
DBE_PRIMARY="${DBE_PRIMARY:-$DBE_DIR/National.xlsx}"
if [ "${IMPORT_LOCATIONS:-1}" = "1" ] && [ -f "$DBE_PRIMARY" ]; then
    echo "Importing real school coordinates ($DBE_PRIMARY) ..."
    DBE_DIR="$DBE_DIR" DBE_PRIMARY="$DBE_PRIMARY" python3 "$SCRIPT_DIR/../import_school_locations.py" \
        | docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
            psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" >/dev/null
    echo "  real coordinates imported"
else
    echo "Skipping real coordinate import (set IMPORT_LOCATIONS=1 with DBE_PRIMARY to enable)"
fi

# ── 8c. Import province and district boundaries ──────────────────────────────
# Provinces come from the demarcation GeoJSON; districts are derived from school
# points. Set IMPORT_BOUNDARIES=0 to skip. See scripts/import_boundaries.py.
if [ "${IMPORT_BOUNDARIES:-1}" = "1" ]; then
    echo "Importing province and district boundaries ..."
    python3 "$SCRIPT_DIR/../import_boundaries.py" \
        | docker exec -e PGPASSWORD="$PG_PASSWORD" -i "$PG_CONTAINER" \
            psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" >/dev/null
    echo "  boundaries imported"
fi

# Refresh after the final seed on every run, including already-published layers.
echo "Recalculating layer bounds ..."
for LAYER in $LAYERS; do
    payload="$(jq -n --arg name "$LAYER" '{featureType:{name:$name}}')"
    recalculate="nativebbox,latlonbbox"
    case "$LAYER" in
        school_locations_effective|school_footprints|school_boundaries)
            has_geometry="$(docker exec -e PGPASSWORD="$PG_PASSWORD" "$PG_CONTAINER" \
                psql -v ON_ERROR_STOP=1 -U "$PG_USER" -d "$PG_DB" -tAc \
                "SELECT EXISTS (SELECT 1 FROM $PG_SCHEMA.$LAYER WHERE geom IS NOT NULL);")"
            if [ "$(echo "$has_geometry" | tr -d '[:space:]')" = "f" ]; then
                payload="$(jq -n --arg name "$LAYER" '{featureType:{name:$name,
                    nativeBoundingBox:{minx:-180,miny:-90,maxx:180,maxy:90,crs:"EPSG:4326"},
                    latLonBoundingBox:{minx:-180,miny:-90,maxx:180,maxy:90,crs:"EPSG:4326"}}}')"
                recalculate=""
            fi
            ;;
    esac
    code="$(curl -sS -o /dev/null -w '%{http_code}' -u "$GS_USER:$GS_PASS" \
        -H 'Content-Type: application/json' -X PUT \
        "$GS_URL/rest/workspaces/$WS/datastores/$DS/featuretypes/$LAYER?recalculate=$recalculate" \
        -d "$payload")"
    case "$code" in
        2??) echo "  $LAYER bounds refreshed" ;;
        *) echo "Could not recalculate bounds for $WS:$LAYER: HTTP $code" >&2; exit 1 ;;
    esac
done

# ── 9. Verify ────────────────────────────────────────────────────────────────
echo "Verifying WFS GetFeature ..."
for LAYER in $LAYERS; do
    feature_count="$(curl -sf \
        "$GS_URL/$WS/ows?service=WFS&version=1.0.0&request=GetFeature&typeName=$WS:$LAYER&outputFormat=application/json" \
        | jq -e '.features | if type == "array" then length else error("Expected a WFS feature collection") end')"
    echo "  $WS:$LAYER returned $feature_count feature(s)"
done

echo
echo "Done. Layers ready in workspace '$WS': $LAYERS"
