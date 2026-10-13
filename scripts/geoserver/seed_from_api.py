#!/usr/bin/env python3
"""Emit SQL that seeds geo.school_locations with real schools from the API.

Coordinates are approximate province centroids plus a deterministic per-school
jitter - NOT real school positions. The purpose is to exercise the tree-to-map
selection highlight (which matches on EMIS number) before the real EMIS
coordinate import lands.

Usage (SQL is written to stdout, pipe it into psql):
    python3 scripts/geoserver/seed_from_api.py > /tmp/seed.sql

Environment:
    API_URL         default http://localhost:5179
    SEED_PAGE_SIZE  default 1000
"""

import json
import os
import sys
import urllib.request

API_URL = os.environ.get("API_URL", "http://localhost:5179").rstrip("/")
PAGE_SIZE = int(os.environ.get("SEED_PAGE_SIZE", "1000"))

# Approximate provincial centres (lon, lat) used as placeholders.
CENTROIDS = {
    "EASTERN CAPE": (28.78, -31.59),
    "FREE STATE": (26.16, -29.09),
    "GAUTENG": (28.05, -26.20),
    "KWAZULU-NATAL": (31.02, -29.86),
    "LIMPOPO": (29.47, -23.90),
    "MPUMALANGA": (30.97, -25.48),
    "NORTHERN CAPE": (24.75, -28.73),
    "NORTH WEST": (25.64, -25.87),
    "WESTERN CAPE": (18.42, -33.92),
}


def esc(value: object) -> str:
    """Escape a value for a single-quoted SQL string literal."""
    return str(value).replace("'", "''")


def fetch_all() -> list[dict]:
    """Fetch every school from the paginated API."""
    schools: list[dict] = []
    page = 1
    while True:
        url = f"{API_URL}/api/v1/dashboard/schools?pageNumber={page}&pageSize={PAGE_SIZE}"
        with urllib.request.urlopen(url, timeout=60) as response:
            data = json.load(response)
        items = data.get("items") or []
        schools.extend(items)
        total = data.get("totalCount", len(schools))
        if not items or len(schools) >= total:
            break
        page += 1
    return schools


def main() -> None:
    schools = fetch_all()
    if not schools:
        print("-- no schools returned by the API", file=sys.stderr)
        return

    values: list[str] = []
    for school in schools:
        emis = str(school.get("id", "")).strip()
        if not emis:
            continue

        province = str(school.get("province", ""))
        centre_lon, centre_lat = CENTROIDS.get(province.upper(), (24.7, -29.0))

        try:
            seed = int(emis)
        except ValueError:
            seed = sum(ord(char) for char in emis)

        lon = centre_lon + ((seed % 100) - 50) / 100.0
        lat = centre_lat + (((seed // 100) % 100) - 50) / 100.0

        values.append(
            f"('{esc(emis)}','{esc(school.get('name', ''))}','{esc(province)}',"
            f"'{esc(school.get('circuit', ''))}',"
            f"{int(school.get('totalWrote') or 0)},{int(school.get('totalPassed') or 0)},"
            f"true,ST_SetSRID(ST_MakePoint({lon:.4f},{lat:.4f}),4326))"
        )

    if not values:
        print("No usable school identifiers; existing locations retained", file=sys.stderr)
        return

    # Replace samples atomically; a failure must not leave the layer empty.
    print("BEGIN;")
    print("DELETE FROM geo.school_locations WHERE is_sample;")
    print(
        "INSERT INTO geo.school_locations "
        "(emis_number,name,province,district,wrote,passed,is_sample,geom) VALUES\n"
        + ",\n".join(values)
        + "\nON CONFLICT (emis_number) DO UPDATE SET "
        "name=EXCLUDED.name, province=EXCLUDED.province, district=EXCLUDED.district, "
        "wrote=EXCLUDED.wrote, passed=EXCLUDED.passed, geom=EXCLUDED.geom "
        "WHERE geo.school_locations.is_sample;"
    )
    print("COMMIT;")


if __name__ == "__main__":
    main()
