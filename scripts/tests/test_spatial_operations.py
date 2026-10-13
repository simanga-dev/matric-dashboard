"""Non-destructive import, sample upsert, and bootstrap regressions.

Run: python3 -B -m unittest discover -s scripts/tests -v
All SQL runs in in-memory SQLite; bootstrap uses fake Docker/curl executables.
"""

import contextlib
import csv
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import sqlite3
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]


def load_script(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


SEED = load_script("seed", "scripts/geoserver/seed_from_api.py")


class ImportTests(unittest.TestCase):
    def run_import(self, rows, quarantine=False):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source.csv"
            rejected = Path(directory) / "rejected.csv"
            fields = ["emis_number", "centre_number", "centre_name", "province",
                      "total_wrote_2024", "total_achieved_2024"]
            with source.open("w", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=fields)
                writer.writeheader()
                writer.writerows(rows)
            env = dict(os.environ, CSV_PATH=str(source))
            env.pop("QUARANTINE_PATH", None)
            if quarantine:
                env["QUARANTINE_PATH"] = str(rejected)
            result = subprocess.run(
                ["python3", "-B", str(ROOT / "scripts/import_schools_to_db.py")],
                env=env, capture_output=True, text=True, check=False,
            )
            quarantined = []
            if rejected.exists():
                with rejected.open(newline="") as handle:
                    quarantined = list(csv.DictReader(handle))
            return result, quarantined

    def test_all_44_zero_centres_rejected_before_sql(self):
        rows = [{"emis_number": "0", "centre_number": str(i), "centre_name": f"Centre {i}"}
                for i in range(44)]
        result, _ = self.run_import(rows)
        self.assertEqual(result.returncode, 1)
        self.assertEqual(result.stdout, "")
        self.assertIn("44 rows", result.stderr)
        self.assertIn("Centre 43", result.stderr)

    def test_quarantine_retains_every_row_and_preserves_real_identifier(self):
        placeholders = [{"emis_number": "0", "centre_number": str(i), "centre_name": f"Centre {i}"}
                        for i in range(44)]
        real = {"emis_number": "001234567", "centre_name": "O'Brien",
                "total_wrote_2024": "10", "total_achieved_2024": "9"}
        result, rejected = self.run_import(placeholders + [real], quarantine=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(rejected), 44)
        self.assertEqual([row["centre_number"] for row in rejected], [str(i) for i in range(44)])
        self.assertIn("'001234567'", result.stdout)
        self.assertIn("O''Brien", result.stdout)
        self.assertNotIn("'Centre 43'", result.stdout)
        self.assertIn("Quarantined 44", result.stderr)

    def test_conflicting_real_emis_never_uses_last_row(self):
        result, _ = self.run_import([
            {"emis_number": "123", "centre_name": "First"},
            {"emis_number": "123", "centre_name": "Second"},
        ])
        self.assertEqual(result.returncode, 1)
        self.assertEqual(result.stdout, "")
        self.assertIn("conflicting records", result.stderr)

    def test_empty_and_no_performance_inputs_emit_no_empty_values(self):
        result, _ = self.run_import([])
        self.assertEqual(result.returncode, 0)
        self.assertEqual(result.stdout, "")
        result, _ = self.run_import([{"emis_number": "123"}])
        self.assertEqual(result.returncode, 0)
        self.assertNotIn('INSERT INTO marks."SchoolPerformances"', result.stdout)
        self.assertTrue(result.stdout.endswith("COMMIT;\n"))

    def test_missing_and_zero_variants_are_rejected(self):
        for emis in ("", "000", "0.0", "NA", "N/A", "-"):
            with self.subTest(emis=emis):
                result, _ = self.run_import([{"emis_number": emis}])
                self.assertEqual(result.returncode, 1)
                self.assertEqual(result.stdout, "")

    def test_existing_quarantine_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            existing = Path(directory) / "quarantine.csv"
            existing.write_text("retain this file\n")
            env = dict(os.environ, QUARANTINE_PATH=str(existing),
                       CSV_PATH=str(ROOT / "2024_nsc_school_performance_report.csv"))
            result = subprocess.run(
                ["python3", "-B", str(ROOT / "scripts/import_schools_to_db.py")],
                env=env, capture_output=True, text=True, check=False,
            )
            self.assertEqual(result.returncode, 1)
            self.assertEqual(result.stdout, "")
            self.assertEqual(existing.read_text(), "retain this file\n")

    def test_bundled_report_retains_all_unresolved_centres_without_database_access(self):
        with tempfile.TemporaryDirectory() as directory:
            rejected = Path(directory) / "quarantine.csv"
            env = dict(os.environ, QUARANTINE_PATH=str(rejected),
                       CSV_PATH=str(ROOT / "2024_nsc_school_performance_report.csv"))
            result = subprocess.run(
                ["python3", "-B", str(ROOT / "scripts/import_schools_to_db.py")],
                env=env, capture_output=True, text=True, check=False,
            )
            self.assertEqual(result.returncode, 0, result.stderr)
            with rejected.open(newline="") as handle:
                rows = list(csv.DictReader(handle))
            self.assertEqual(len(rows), 50)
            self.assertEqual(sum(row["emis_number"].strip() == "0" for row in rows), 45)
            self.assertEqual(sum(not row["emis_number"].strip() for row in rows), 5)
            self.assertEqual(len({row["centre_number"].strip() for row in rows}), 50)
            self.assertIn("Quarantined 50", result.stderr)


class SampleSeedTests(unittest.TestCase):
    def setUp(self):
        self.db = sqlite3.connect(":memory:")
        self.addCleanup(self.db.close)
        self.db.execute("ATTACH DATABASE ':memory:' AS geo")
        self.db.executescript("""
            CREATE TABLE geo.school_locations (
                emis_number TEXT PRIMARY KEY, name TEXT, province TEXT, district TEXT,
                quintile INTEGER, wrote INTEGER, passed INTEGER, is_sample BOOLEAN, geom TEXT
            );
        """)
        self.db.create_function("ST_MakePoint", 2, lambda lon, lat: f"{lon},{lat}")
        self.db.create_function("ST_SetSRID", 2, lambda point, srid: f"{point}:{srid}")

    def seed_sql(self, schools):
        out = io.StringIO()
        with patch.object(SEED, "fetch_all", return_value=schools), contextlib.redirect_stdout(out):
            SEED.main()
        return out.getvalue()

    def test_api_seed_preserves_real_rows_across_repeat_runs(self):
        self.db.execute("INSERT INTO geo.school_locations VALUES ('123','Real','Gauteng','Real district',5,20,18,false,'real point')")
        self.db.commit()
        sql = self.seed_sql([
            {"id": "123", "name": "Sample overwrite", "province": "Gauteng"},
            {"id": "456", "name": "O'Brien", "province": "Western Cape"},
        ])
        for _ in range(2):
            self.db.executescript(sql)
            self.assertEqual(self.db.execute("SELECT name,geom,is_sample FROM geo.school_locations WHERE emis_number='123'").fetchone(),
                             ("Real", "real point", 0))
            self.assertEqual(self.db.execute("SELECT COUNT(*) FROM geo.school_locations").fetchone()[0], 2)
            self.assertEqual(self.db.execute("SELECT name,is_sample FROM geo.school_locations WHERE emis_number='456'").fetchone(),
                             ("O'Brien", 1))

    def test_static_seed_preserves_real_geometry(self):
        self.db.execute("INSERT INTO geo.school_locations (emis_number,name,is_sample,geom) VALUES ('SAMPLE-WC-001','Real',false,'real point')")
        self.db.commit()
        sql = (ROOT / "scripts/postgis/init.sql").read_text().split("INSERT INTO geo.school_locations", 1)[1]
        for _ in range(2):
            self.db.executescript("INSERT INTO geo.school_locations" + sql)
            self.assertEqual(self.db.execute("SELECT name,geom,is_sample FROM geo.school_locations WHERE emis_number='SAMPLE-WC-001'").fetchone(),
                             ("Real", "real point", 0))

    def test_empty_api_does_not_delete_existing_samples(self):
        self.assertEqual(self.seed_sql([]), "")
        self.assertEqual(self.seed_sql([{"id": ""}]), "")

    def test_failed_sample_insert_can_roll_back_sample_deletion(self):
        self.db.execute("INSERT INTO geo.school_locations (emis_number,name,is_sample,geom) VALUES ('old','Retain',true,'old point')")
        self.db.commit()
        sql = self.seed_sql([{"id": "new", "name": "Replacement"}])
        # Fail after deletion, as psql ON_ERROR_STOP would disconnect before COMMIT.
        sql = sql.replace("INSERT INTO geo.school_locations", "INSERT INTO geo.missing_table")
        with self.assertRaises(sqlite3.OperationalError):
            self.db.executescript(sql)
        self.db.rollback()
        self.assertEqual(self.db.execute("SELECT emis_number FROM geo.school_locations").fetchall(), [("old",)])


@unittest.skipUnless(shutil.which("docker"), "Docker CLI required for Compose config regression")
class ComposeTests(unittest.TestCase):
    def test_version_volume_layout_and_maintenance_binding(self):
        env = dict(os.environ)
        for key in ("POSTGIS_IMAGE", "POSTGRES_VOLUME", "POSTGRES_VOLUME_TARGET", "GEOSERVER_PORT"):
            env.pop(key, None)
        for key in ("POSTGRES_PASSWORD", "GEOSERVER_ADMIN_PASSWORD", "MEILI_MASTER_KEY",
                    "MINIO_ROOT_PASSWORD", "JWT_SECRET", "EXT_AUTH_ENCRYPTION_KEY"):
            env[key] = "validation-only"
        cases = [({}, "postgis/postgis:17-3.5-alpine", "postgres-data", "/var/lib/postgresql/data"),
                 ({"POSTGIS_IMAGE": "postgis/postgis:18-3.6-alpine",
                   "POSTGRES_VOLUME": "postgres-data-pg18", "POSTGRES_VOLUME_TARGET": "/var/lib/postgresql"},
                  "postgis/postgis:18-3.6-alpine", "postgres-data-pg18", "/var/lib/postgresql")]
        for overrides, image, volume, target in cases:
            with self.subTest(image=image):
                result = subprocess.run(
                    ["docker", "compose", "--env-file", "/dev/null", "-f", str(ROOT / "docker-compose.yml"),
                     "config", "--format", "json"],
                    env=dict(env, **overrides), capture_output=True, text=True, check=False,
                )
                self.assertEqual(result.returncode, 0, result.stderr)
                services = json.loads(result.stdout)["services"]
                self.assertEqual(services["postgres"]["image"], image)
                data_mount = services["postgres"]["volumes"][0]
                self.assertEqual((data_mount["source"], data_mount["target"]), (volume, target))
                self.assertEqual(services["geoserver"]["ports"][0]["host_ip"], "127.0.0.1")


@unittest.skipUnless(shutil.which("jq"), "jq required for bootstrap shell regression")
class BootstrapTests(unittest.TestCase):
    def test_existing_layer_bounds_refresh_after_seed_on_repeat_runs(self):
        # No commands in this harness can contact Docker or an HTTP server.
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            stub = """#!/usr/bin/python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
with open(os.environ['TEST_LOG'], 'a') as handle:
    handle.write(json.dumps([name, args]) + '\\n')
if name == 'docker' and 'psql' in args:
    sys.stdin.read()
elif name == 'python3':
    print('BEGIN; COMMIT;')
elif name == 'curl':
    if '-w' in args:
        print(os.environ.get('BOUNDS_STATUS', '200') if 'PUT' in args else '200', end='')
    elif any('GetFeature' in arg for arg in args):
        print('{"features":[]}')
"""
            for name in ("docker", "curl", "python3"):
                executable = directory / name
                executable.write_text(stub)
                executable.chmod(0o755)
            env = dict(os.environ, PATH=f"{directory}:{os.environ['PATH']}", TEST_LOG=str(log),
                       PG_CONTAINER="fake-pg", GS_CONTAINER="fake-gs", PG_USER="fake",
                       PG_PASSWORD="fake", PG_DB="fake", PG_HOST="postgres",
                       GEOSERVER_PORT="8080", GEOSERVER_ADMIN_USER="fake",
                       GEOSERVER_ADMIN_PASSWORD="fake", SEED_FROM_API="1")
            for _ in range(2):
                result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                        env=env, capture_output=True, text=True, check=False)
                self.assertEqual(result.returncode, 0, result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            puts = [i for i, (name, args) in enumerate(calls) if name == "curl" and "PUT" in args]
            seeds = [i for i, (name, _) in enumerate(calls) if name == "python3"]
            self.assertEqual(len(puts), 2)
            self.assertEqual(len(seeds), 2)
            for seed, put in zip(seeds, puts):
                self.assertLess(seed, put)
                self.assertTrue(any("recalculate=nativebbox,latlonbbox" in arg for arg in calls[put][1]))
            env["SEED_FROM_API"] = "0"
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertIn("layer bounds refreshed", result.stdout)
            env["BOUNDS_STATUS"] = "500"
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Could not recalculate bounds", result.stderr)


if __name__ == "__main__":
    unittest.main()
