#!/usr/bin/env python3
"""Emit SQL that populates the geo boundary layers (provinces and districts).

Provinces are read from a GeoJSON file sourced from the Municipal Demarcation
Board (via geoBoundaries, CC BY 3.0 IGO). Districts are derived inside PostGIS:
a Voronoi diagram is built over the mapped school points, the cells are labelled
by their nearest school's DBE district, dissolved, and clipped to the province.
That produces boundaries that match the education-district names the app already
uses, instead of the 52 municipal district municipalities.

SQL is written to stdout - pipe it into psql:
    python3 scripts/import_boundaries.py | psql ...

Environment:
    PROVINCES_GEOJSON  default <repo>/scripts/postgis/data/za-provinces.geojson
    PROVINCES_URL      override the download used when the file is missing
"""

import json
import os
import sys
import urllib.request

DEFAULT_GEOJSON = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "postgis",
    "data",
    "za-provinces.geojson",
)
GEOJSON_PATH = os.environ.get("PROVINCES_GEOJSON", DEFAULT_GEOJSON)
GEOJSON_URL = os.environ.get(
    "PROVINCES_URL",
    "https://github.com/wmgeolab/geoBoundaries/raw/9469f09/releaseData/gbOpen/ZAF/"
    "ADM1/geoBoundaries-ZAF-ADM1_simplified.geojson",
)

# Voronoi cells are labelled by their nearest school; districts are dissolved and
# clipped to the province polygon. Bump the margin if outer provinces look thin.
ENVELOPE_MARGIN = 0.5


def load_provinces() -> dict:
    """Read the province GeoJSON, downloading it once when it is not on disk."""
    if not os.path.exists(GEOJSON_PATH):
        os.makedirs(os.path.dirname(GEOJSON_PATH), exist_ok=True)
        print(f"Downloading province boundaries to {GEOJSON_PATH}", file=sys.stderr)
        with urllib.request.urlopen(GEOJSON_URL, timeout=120) as response:
            with open(GEOJSON_PATH, "wb") as handle:
                handle.write(response.read())
    with open(GEOJSON_PATH, encoding="utf-8") as handle:
        return json.load(handle)


def q(value: object) -> str:
    """Quote a value as a SQL string literal."""
    return "'" + str(value).replace("'", "''") + "'"


def province_values(feature: dict) -> str:
    properties = feature["properties"]
    geometry = json.dumps(feature["geometry"], separators=(",", ":"))
    # The demarcation GeoJSON has ring self-intersections; validate and keep only
    # polygonal parts so the geometry can be unioned and clipped later.
    return (
        f"({q(properties['code'])},{q(properties['name'])},"
        f"ST_Multi(ST_CollectionExtract(ST_MakeValid("
        f"ST_SetSRID(ST_GeomFromGeoJSON({q(geometry)}),4326)),3)))"
    )


DISTRICT_SQL = f"""
DELETE FROM geo.districts;
WITH points AS (
    SELECT ST_Collect(geom) AS g
    FROM geo.school_locations
    WHERE NOT is_sample AND district IS NOT NULL AND district <> ''
),
env AS (
    SELECT ST_Expand(ST_Extent(geom)::geometry, {ENVELOPE_MARGIN}) AS e
    FROM geo.provinces
),
cells AS (
    SELECT ST_SnapToGrid((ST_Dump(ST_VoronoiPolygons(points.g, 0, env.e))).geom, 0.000001) AS cell
    FROM points, env
),
labelled AS (
    SELECT c.cell AS cell, s.province AS province, s.district AS name
    FROM cells c
    CROSS JOIN LATERAL (
        SELECT province, district, geom
        FROM geo.school_locations
        WHERE NOT is_sample AND district IS NOT NULL AND district <> ''
        ORDER BY c.cell <-> geom
        LIMIT 1
    ) s
),
dissolved AS (
    SELECT province, name, ST_MakeValid(ST_Union(cell)) AS poly
    FROM labelled
    GROUP BY province, name
)
INSERT INTO geo.districts (province, name, geom)
SELECT d.province, d.name,
       ST_Multi(ST_Intersection(d.poly, ST_MakeValid(p.geom)))
FROM dissolved d
JOIN geo.provinces p ON p.name = d.province
WHERE NOT ST_IsEmpty(ST_Intersection(d.poly, ST_MakeValid(p.geom)));
"""


def main() -> None:
    provinces = load_provinces()
    features = provinces.get("features") or []
    if not features:
        raise ValueError(f"{GEOJSON_PATH}: no features")

    out = sys.stdout
    # Provinces first and committed, so district derivation has province
    # polygons to clip against even if that step needs to be re-run.
    out.write("BEGIN;\n")
    out.write(
        "INSERT INTO geo.provinces (code,name,geom) VALUES\n"
        + ",\n".join(province_values(feature) for feature in features)
        + "\nON CONFLICT (code) DO UPDATE SET name=EXCLUDED.name, geom=EXCLUDED.geom;\n"
    )
    out.write("COMMIT;\n")
    out.write("BEGIN;\n")
    out.write(DISTRICT_SQL)
    out.write("COMMIT;\n")
    print(f"Prepared {len(features)} provinces and district boundaries", file=sys.stderr)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(f"Import refused: {error}", file=sys.stderr)
        sys.exit(1)
