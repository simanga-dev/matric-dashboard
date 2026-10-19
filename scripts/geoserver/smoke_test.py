"""Opt-in publication smoke test using disposable PostGIS/GeoServer containers.

Run explicitly: python3 scripts/geoserver/smoke_test.py
Requires Docker, curl and jq. Does not touch the application's database,
GeoServer configuration, credentials or volumes. Removes its own resources.
"""

import base64
import json
import os
import subprocess
import time
import urllib.request
from pathlib import Path
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[2]
tag = f"opencode-spatial-{uuid4().hex[:12]}"
network, pg, gs = tag + "-net", tag + "-pg", tag + "-gs"
password = "isolated-smoke-test-only"


def run(*args, input=None):
    return subprocess.run(
        args, input=input, text=True, check=True, capture_output=True
    ).stdout.strip()


def sql(text):
    return run(
        "docker",
        "exec",
        "-i",
        pg,
        "psql",
        "-v",
        "ON_ERROR_STOP=1",
        "-U",
        "smoke",
        "-d",
        "smoke",
        input=text,
    )


def ready(check, label, seconds=180):
    deadline = time.monotonic() + seconds
    last_error = None
    while time.monotonic() < deadline:
        try:
            if check():
                return
        except (OSError, subprocess.CalledProcessError) as error:
            last_error = str(error)
        time.sleep(1)
    raise RuntimeError(f"{label} did not become ready: {last_error}")


def main():
    try:
        run("docker", "network", "create", network)
        run(
            "docker",
            "run",
            "-d",
            "--name",
            pg,
            "--network",
            network,
            "-e",
            "POSTGRES_USER=smoke",
            "-e",
            "POSTGRES_DB=smoke",
            "-e",
            f"POSTGRES_PASSWORD={password}",
            "postgis/postgis:18-3.6-alpine",
        )
        ready(
            lambda: run(
                "docker",
                "exec",
                pg,
                "pg_isready",
                "-h",
                "127.0.0.1",
                "-U",
                "smoke",
                "-d",
                "smoke",
            ),
            "PostGIS",
        )
        sql((ROOT / "scripts/postgis/init.sql").read_text())
        # Fixture views exercise publication and UUID handling. Application
        # migration definitions are tested separately by the backend suite.
        sql("""CREATE TABLE geo.smoke_grounds (id uuid PRIMARY KEY, geom geometry(MultiPolygon,4326));
CREATE VIEW geo.school_locations_effective AS
SELECT md5(emis_number)::uuid AS id, emis_number,name,province,district,quintile,wrote,passed,is_sample,
       geom::geometry(Point,4326) AS geom FROM geo.school_locations;
CREATE VIEW geo.school_boundaries AS
SELECT s.id,s.emis_number,s.name,s.province,s.district,g.geom::geometry(MultiPolygon,4326) AS geom
FROM geo.school_locations_effective s JOIN geo.smoke_grounds g ON g.id=s.id;
""")
        run(
            "docker",
            "run",
            "-d",
            "--name",
            gs,
            "--network",
            network,
            "-p",
            "127.0.0.1::8080",
            "-e",
            "SKIP_DEMO_DATA=true",
            "-e",
            "GEOSERVER_ADMIN_USER=admin",
            "-e",
            f"GEOSERVER_ADMIN_PASSWORD={password}",
            "-e",
            "EXTRA_JAVA_OPTS=-Xms128m -Xmx512m",
            "docker.osgeo.org/geoserver:2.26.2",
        )
        port = run("docker", "port", gs, "8080").split(":")[-1]
        base = f"http://127.0.0.1:{port}/geoserver"
        authorization = (
            "Basic " + base64.b64encode(f"admin:{password}".encode()).decode()
        )
        ready(
            lambda: (
                urllib.request.urlopen(
                    urllib.request.Request(
                        base + "/rest/about/version.json",
                        headers={"Authorization": authorization},
                    ),
                    timeout=5,
                ).status
                == 200
            ),
            "GeoServer",
        )
        env = dict(
            os.environ,
            ENV_FILE="/dev/null",
            PG_CONTAINER=pg,
            GS_CONTAINER=gs,
            PG_USER="smoke",
            PG_PASSWORD=password,
            PG_DB="smoke",
            PG_HOST=pg,
            PG_PORT="5432",
            PG_SCHEMA="geo",
            GEOSERVER_URL=base,
            GEOSERVER_ADMIN_USER="admin",
            GEOSERVER_ADMIN_PASSWORD=password,
            SEED_FROM_API="0",
            GEOSERVER_WORKSPACE="matric",
            GEOSERVER_DATASTORE="schools",
            IMPORT_LOCATIONS="0",
            IMPORT_BOUNDARIES="0",
            GEOSERVER_LAYERS="school_locations school_locations_effective school_boundaries",
        )
        result = subprocess.run(
            ["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
            env=env,
            text=True,
            capture_output=True,
            check=False,
        )
        print(result.stdout)
        if result.returncode:
            raise RuntimeError(result.stderr)

        def features(layer):
            with urllib.request.urlopen(
                base
                + "/matric/ows?service=WFS&version=1.0.0&request=GetFeature"
                + f"&typeName=matric:{layer}&outputFormat=application/json",
                timeout=30,
            ) as response:
                return json.load(response)["features"]

        points = features("school_locations_effective")
        assert len(points) == 12, points
        assert features("school_boundaries") == []
        assert [f["id"] for f in points] == [
            f["id"] for f in features("school_locations_effective")
        ]
        assert len({f["id"] for f in points}) == 12
        assert all(f["properties"].get("id") for f in points), points[0]
        sql("""INSERT INTO geo.smoke_grounds SELECT id,
       ST_Multi(ST_Buffer(geom,0.001))::geometry(MultiPolygon,4326)
       FROM geo.school_locations_effective LIMIT 1;""")
        grounds = features("school_boundaries")
        assert len(grounds) == 1 and grounds[0]["geometry"]["type"] == "MultiPolygon"
        result = subprocess.run(
            ["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
            env=env,
            text=True,
            capture_output=True,
            check=False,
        )
        if result.returncode:
            raise RuntimeError(result.stdout + result.stderr)
        assert [f["id"] for f in points] == [
            f["id"] for f in features("school_locations_effective")
        ]
        assert grounds[0]["id"] == features("school_boundaries")[0]["id"]
        print(
            "PASS: empty-view publication, exposed UUID keys, stable FIDs, live new grounds, and repeat bootstrap."
        )
    except Exception:
        logs = subprocess.run(
            ["docker", "logs", "--tail", "80", gs],
            capture_output=True,
            text=True,
            check=False,
        )
        print(logs.stdout)
        print(logs.stderr)
        raise
    finally:
        for container in (gs, pg):
            subprocess.run(
                ["docker", "rm", "-f", "-v", container],
                capture_output=True,
                check=False,
            )
        subprocess.run(
            ["docker", "network", "rm", network], capture_output=True, check=False
        )


if __name__ == "__main__":
    main()
