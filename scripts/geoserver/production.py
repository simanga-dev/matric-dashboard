"""Configure the production GeoServer store without seeding or changing database data."""

import base64
import json
import os
from urllib.error import HTTPError
from urllib.request import Request, urlopen


LAYERS = (
    "school_locations", "school_locations_effective", "school_footprints",
    "school_boundaries", "provinces", "districts",
)


def request(method, path, payload=None):
    """Call the private GeoServer REST API, failing without logging credentials."""
    credentials = f"{os.environ['GEOSERVER_ADMIN_USER']}:{os.environ['GEOSERVER_ADMIN_PASSWORD']}"
    token = base64.b64encode(credentials.encode()).decode()
    req = Request(
        os.environ["GEOSERVER_BASE_URL"].rstrip("/") + "/rest/" + path,
        data=json.dumps(payload).encode() if payload is not None else None,
        headers={"Authorization": "Basic " + token, "Content-Type": "application/json"},
        method=method,
    )
    try:
        with urlopen(req, timeout=120) as response:
            return response.read()
    except HTTPError as error:
        error.close()
        if method == "GET" and error.code == 404:
            return None
        raise RuntimeError(f"GeoServer {method} {path} failed: HTTP {error.code}") from None


def configure():
    """Create or update the shared PostGIS store and publish the application's layers."""
    if request("GET", "workspaces/matric.json") is None:
        request("POST", "workspaces", {"workspace": {"name": "matric"}})

    parameters = {
        "dbtype": "postgis",
        "host": os.environ["GEOSERVER_DB_HOST"],
        "port": os.environ.get("GEOSERVER_DB_PORT", "5432"),
        "database": os.environ["GEOSERVER_DB_NAME"],
        "schema": "geo",
        "user": os.environ["GEOSERVER_DB_USER"],
        "passwd": os.environ["GEOSERVER_DB_PASSWORD"],
        "SSL mode": os.environ.get("GEOSERVER_DB_SSLMODE", "require"),
        "Expose primary keys": "true",
        "Primary key metadata table": "geo.gt_pk_metadata",
    }
    store = {"dataStore": {
        "name": "schools", "enabled": True,
        "connectionParameters": {"entry": [
            {"@key": key, "$": value} for key, value in parameters.items()
        ]},
    }}
    path = "workspaces/matric/datastores/schools"
    if request("GET", path + ".json") is None:
        request("POST", "workspaces/matric/datastores", store)
    else:
        request("PUT", path, store)

    for layer in LAYERS:
        feature_path = f"{path}/featuretypes/{layer}"
        if request("GET", feature_path + ".json") is None:
            bounds = {"minx": -180, "miny": -90, "maxx": 180, "maxy": 90, "crs": "EPSG:4326"}
            request("POST", path + "/featuretypes", {"featureType": {
                "name": layer, "nativeName": layer, "srs": "EPSG:4326",
                "nativeBoundingBox": bounds, "latLonBoundingBox": bounds,
            }})
        request("PUT", feature_path + "?recalculate=nativebbox,latlonbbox",
                {"featureType": {"name": layer}})
        print(f"Configured matric:{layer}", flush=True)


if __name__ == "__main__":
    configure()
