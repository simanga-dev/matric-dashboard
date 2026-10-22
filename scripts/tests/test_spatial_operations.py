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
import zipfile
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]

REPORT_CSV = Path(
    os.environ.get("CSV_PATH", ROOT / "2024_nsc_school_performance_report.csv")
)
# The raw DBE report is not committed (see .gitignore). Tests that parse the real
# file skip when it is absent; set CSV_PATH to point at a local copy.
requires_report = unittest.skipUnless(
    REPORT_CSV.is_file(),
    f"NSC performance report not found at {REPORT_CSV}; set CSV_PATH to run this test.",
)


def load_script(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


SEED = load_script("seed", "scripts/geoserver/seed_from_api.py")
LOCATIONS = load_script("locations", "scripts/import_school_locations.py")


def write_masterlist(path, headers, rows):
    """Write a minimal single-sheet xlsx using inline strings (no sharedStrings)."""
    from xml.sax.saxutils import escape

    def column(index):
        name = ""
        index += 1
        while index:
            index, remainder = divmod(index - 1, 26)
            name = chr(65 + remainder) + name
        return name

    def cell(reference, value):
        if isinstance(value, (int, float)):
            return f'<c r="{reference}"><v>{value}</v></c>'
        return f'<c r="{reference}" t="inlineStr"><is><t>{escape(str(value))}</t></is></c>'

    def row_xml(number, values):
        cells = "".join(cell(f"{column(i)}{number}", value) for i, value in enumerate(values))
        return f'<row r="{number}">{cells}</row>'

    sheet = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
        "<sheetData>" + row_xml(1, headers)
        + "".join(row_xml(i + 2, values) for i, values in enumerate(rows))
        + "</sheetData></worksheet>"
    )
    parts = {
        "[Content_Types].xml":
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
            '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
            '<Default Extension="xml" ContentType="application/xml"/>'
            '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
            '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
            "</Types>",
        "_rels/.rels":
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>'
            "</Relationships>",
        "xl/workbook.xml":
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
            'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
            '<sheets><sheet name="National" sheetId="1" r:id="rId1"/></sheets></workbook>',
        "xl/_rels/workbook.xml.rels":
            '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'
            "</Relationships>",
        "xl/worksheets/sheet1.xml": sheet,
    }
    with zipfile.ZipFile(path, "w") as archive:
        for name, content in parts.items():
            archive.writestr(name, content)


class SchoolLocationImportTests(unittest.TestCase):
    HEADERS = ["NatEmis", "Province", "Official_Institution_Name", "GIS_Long", "GIS_Lat"]
    REPORT_FIELDS = ["province", "district_name", "emis_number", "centre_number",
                     "centre_name", "quintile", "total_wrote_2024", "total_achieved_2024"]

    def run_import(self, report_rows, masterlist_rows, fallback_rows=None, unresolved=False):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            report = directory / "report.csv"
            with report.open("w", newline="") as handle:
                writer = csv.DictWriter(handle, fieldnames=self.REPORT_FIELDS)
                writer.writeheader()
                writer.writerows(report_rows)
            primary = directory / "National.xlsx"
            write_masterlist(primary, self.HEADERS, masterlist_rows)
            env = dict(os.environ, CSV_PATH=str(report), DBE_DIR=str(directory),
                       DBE_PRIMARY=str(primary), DBE_FALLBACK=str(directory / "missing.xlsx"))
            env.pop("UNRESOLVED_PATH", None)
            if fallback_rows is not None:
                fallback = directory / "fallback.xlsx"
                write_masterlist(fallback, self.HEADERS, fallback_rows)
                env["DBE_FALLBACK"] = str(fallback)
            if unresolved:
                env["UNRESOLVED_PATH"] = str(directory / "unresolved.csv")
            result = subprocess.run(
                ["python3", "-B", str(ROOT / "scripts/import_school_locations.py")],
                env=env, capture_output=True, text=True, check=False,
            )
            unresolved_rows = []
            if unresolved and Path(env.get("UNRESOLVED_PATH", "")).exists():
                with open(env["UNRESOLVED_PATH"], newline="") as handle:
                    unresolved_rows = list(csv.DictReader(handle))
            return result, unresolved_rows

    def report(self, emis, name="School", province="GAUTENG", quintile="3",
               wrote="100", achieved="80"):
        return {"province": province, "district_name": "Metro", "emis_number": emis,
                "centre_number": "1", "centre_name": name, "quintile": quintile,
                "total_wrote_2024": wrote, "total_achieved_2024": achieved}

    def test_coerce_normalizes_swap_decimal_and_blank(self):
        self.assertEqual(LOCATIONS.coerce_coordinates("28.0473", "-26.2041"), (28.0473, -26.2041))
        self.assertEqual(LOCATIONS.coerce_coordinates("-31.157779", "29.97026"), (29.97026, -31.157779))
        self.assertEqual(LOCATIONS.coerce_coordinates("-2907075", "2368877"), (23.68877, -29.07075))
        self.assertIsNone(LOCATIONS.coerce_coordinates("", ""))
        self.assertIsNone(LOCATIONS.coerce_coordinates("0", "0"))

    def test_end_to_end_emits_real_rows_and_flags_samples_false(self):
        result, _ = self.run_import(
            [self.report("123", name="O'Brien"), self.report("456", province="EASTERN CAPE")],
            [["123", "GT", "O'Brien", "28.0473", "-26.2041"],
             ["456", "EC", "Swapped", "-31.157779", "29.97026"]],
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("ST_MakePoint(28.047300,-26.204100)", result.stdout)
        self.assertIn("ST_MakePoint(29.970260,-31.157779)", result.stdout)
        self.assertIn("O''Brien", result.stdout)
        self.assertIn(",false,ST_SetSRID", result.stdout)
        self.assertIn("is_sample=false", result.stdout)
        self.assertTrue(result.stdout.startswith("BEGIN;\n"))
        self.assertTrue(result.stdout.endswith("COMMIT;\n"))

    def test_fallback_fills_coordinates_missing_from_primary(self):
        result, _ = self.run_import(
            [self.report("999")],
            [["111", "GT", "Other", "28.0", "-26.0"]],
            fallback_rows=[["999", "GT", "Fallback", "28.05", "-26.21"]],
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("ST_MakePoint(28.050000,-26.210000)", result.stdout)

    def test_unresolved_schools_are_reported_and_optionally_written(self):
        result, unresolved = self.run_import(
            [self.report("123"), self.report("404", name="Missing")],
            [["123", "GT", "Found", "28.0", "-26.0"]],
            unresolved=True,
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("'404'", result.stdout)
        self.assertIn("1 schools left without coordinates", result.stderr)
        self.assertEqual([row["emis_number"] for row in unresolved], ["404"])
        self.assertIn("Wrote 1 schools", result.stderr)

    def test_placeholder_emis_emits_no_sql(self):
        result, _ = self.run_import(
            [self.report("0", name="Placeholder"), self.report("NA")],
            [["0", "GT", "Placeholder", "28.0", "-26.0"]],
        )
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stdout, "")

    def test_missing_primary_masterlist_refuses(self):
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / "report.csv"
            report.write_text("emis_number,province,centre_name\n123,GAUTENG,School\n")
            env = dict(os.environ, CSV_PATH=str(report),
                       DBE_PRIMARY=str(Path(directory) / "absent.xlsx"),
                       DBE_FALLBACK=str(Path(directory) / "absent2.xlsx"))
            result = subprocess.run(
                ["python3", "-B", str(ROOT / "scripts/import_school_locations.py")],
                env=env, capture_output=True, text=True, check=False,
            )
            self.assertEqual(result.returncode, 1)
            self.assertEqual(result.stdout, "")
            self.assertIn("not found", result.stderr)


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

    @requires_report
    def test_existing_quarantine_is_never_overwritten(self):
        with tempfile.TemporaryDirectory() as directory:
            existing = Path(directory) / "quarantine.csv"
            existing.write_text("retain this file\n")
            env = dict(os.environ, QUARANTINE_PATH=str(existing),
                       CSV_PATH=str(REPORT_CSV))
            result = subprocess.run(
                ["python3", "-B", str(ROOT / "scripts/import_schools_to_db.py")],
                env=env, capture_output=True, text=True, check=False,
            )
            self.assertEqual(result.returncode, 1)
            self.assertEqual(result.stdout, "")
            self.assertEqual(existing.read_text(), "retain this file\n")

    @requires_report
    def test_bundled_report_retains_all_unresolved_centres_without_database_access(self):
        with tempfile.TemporaryDirectory() as directory:
            rejected = Path(directory) / "quarantine.csv"
            env = dict(os.environ, QUARANTINE_PATH=str(rejected),
                       CSV_PATH=str(REPORT_CSV))
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

    def test_source_import_and_sample_seed_do_not_touch_editor_profiles(self):
        # The real PostGIS migration/view behavior is covered by backend tests.
        # This harness proves that the actual import/seed SQL only changes the
        # source table, leaving independently stored editor data untouched.
        self.db.executescript("""
            CREATE TABLE geo.school_spatial_profiles (
                school_id TEXT PRIMARY KEY, boundary TEXT, location_override TEXT,
                revision INTEGER, reason TEXT
            );
            INSERT INTO geo.school_spatial_profiles VALUES
                ('school-uuid','editor grounds','editor point',7,'Field correction');
        """)
        expected = self.db.execute("SELECT * FROM geo.school_spatial_profiles").fetchall()
        output = io.StringIO()
        with patch.object(LOCATIONS, "load_report", return_value=[{
            "emis_number": "001234567", "centre_name": "School", "province": "GAUTENG"
        }]), patch.object(LOCATIONS, "read_workbook", return_value={"001234567": (28.1, -26.1)}), \
                patch.object(LOCATIONS.os.path, "exists", return_value=False), \
                contextlib.redirect_stdout(output), contextlib.redirect_stderr(io.StringIO()):
            LOCATIONS.main()
        for _ in range(2):
            self.db.executescript(output.getvalue())
            self.db.executescript(self.seed_sql([
                {"id": "001234567", "name": "Sample overwrite", "province": "Gauteng"}
            ]))
            self.assertEqual(self.db.execute("SELECT * FROM geo.school_spatial_profiles").fetchall(), expected)
            self.assertEqual(self.db.execute(
                "SELECT geom,is_sample FROM geo.school_locations WHERE emis_number='001234567'"
            ).fetchone(), ("28.1,-26.1:4326", 0))

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
                self.assertEqual(services["frontend"]["environment"]["PUBLIC_MAP_IMAGERY_URL"],
                                 env.get("PUBLIC_MAP_IMAGERY_URL", ""))


@unittest.skipUnless(shutil.which("jq"), "jq required for bootstrap shell regression")
class BootstrapTests(unittest.TestCase):
    STUB = """#!/usr/bin/python3
import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
with open(os.environ['TEST_LOG'], 'a') as handle:
    handle.write(json.dumps([name, args]) + '\\n')
if name == 'docker' and 'psql' in args:
    sys.stdin.read()
    if '-tAc' in args:
        query = args[args.index('-tAc') + 1]
        print(os.environ.get('SPATIAL_VIEWS_PRESENT', 't') if 'to_regclass' in query
              else os.environ.get('HAS_GEOMETRY', 't'))
elif name == 'python3':
    print('BEGIN; COMMIT;')
elif name == 'curl':
    if '-w' in args:
        status = os.environ.get('GET_STATUS', '200')
        if 'PUT' in args:
            status = os.environ.get('BOUNDS_STATUS', '200')
        elif 'POST' in args:
            status = os.environ.get('PUBLISH_STATUS', '200') if any(arg.endswith('/featuretypes') for arg in args) else '201'
        print(status, end='')
    elif any('GetFeature' in arg for arg in args):
        print(os.environ.get('WFS_BODY', '{"features":[]}'))
    elif any('datastores/' in arg and arg.endswith('.json') for arg in args):
        entries = [{'@key':'host','$':'postgres'}]
        if os.environ.get('METADATA_TABLE'):
            entries.append({'@key':'Primary key metadata table','$':os.environ['METADATA_TABLE']})
        print(json.dumps({'dataStore':{'connectionParameters':{'entry':entries}}}))
"""

    def stub_env(self, directory, log, **overrides):
        for name in ("docker", "curl", "python3"):
            executable = directory / name
            executable.write_text(self.STUB)
            executable.chmod(0o755)
        env = dict(os.environ, PATH=f"{directory}:{os.environ['PATH']}", TEST_LOG=str(log),
                   PG_CONTAINER="fake-pg", GS_CONTAINER="fake-gs", PG_USER="fake",
                   PG_PASSWORD="fake", PG_DB="fake", PG_HOST="postgres",
                   GEOSERVER_PORT="8080", GEOSERVER_ADMIN_USER="fake",
                   GEOSERVER_ADMIN_PASSWORD="fake", SEED_FROM_API="1")
        env.update(overrides)
        return env

    def test_existing_layer_bounds_refresh_after_seed_on_repeat_runs(self):
        # No commands in this harness can contact Docker or an HTTP server.
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            env = self.stub_env(directory, log, DBE_DIR=str(directory / "no-dbe"),
                                GEOSERVER_LAYERS="school_locations", IMPORT_BOUNDARIES="0")
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
            self.assertIn("bounds refreshed", result.stdout)
            env["BOUNDS_STATUS"] = "500"
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Could not recalculate bounds", result.stderr)

    def test_publishes_and_refreshes_all_geo_layers(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            env = self.stub_env(directory, log, DBE_DIR=str(directory / "no-dbe"))
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertEqual(result.returncode, 0, result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            bounds_calls = [args for name, args in calls if name == "curl" and "PUT" in args
                            and any("featuretypes/" in arg for arg in args)]
            puts = [arg for args in bounds_calls for arg in args]
            self.assertEqual(len(bounds_calls), 6)
            for layer in ("school_locations", "school_locations_effective", "school_footprints",
                          "school_boundaries", "provinces", "districts"):
                self.assertTrue(any(f"featuretypes/{layer}?" in arg for arg in puts), layer)
            seeds = [i for i, (name, _) in enumerate(calls) if name == "python3"]
            first_put = next(i for i, (name, args) in enumerate(calls) if name == "curl" and "PUT" in args
                             and any("featuretypes/" in arg for arg in args))
            self.assertTrue(all(seed < first_put for seed in seeds))
            self.assertIn("boundaries imported", result.stdout)

    def test_missing_spatial_migration_fails_before_publication(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            env = self.stub_env(directory, log, SPATIAL_VIEWS_PRESENT="f")
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Apply the API spatial migration", result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            self.assertFalse(any(name == "curl" for name, _ in calls))

    def test_new_layers_have_initial_bounds_and_metadata_store_setting(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            env = self.stub_env(directory, log, GET_STATUS="404", IMPORT_BOUNDARIES="0",
                                DBE_DIR=str(directory / "no-dbe"))
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertEqual(result.returncode, 0, result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            store = next(args for name, args in calls if name == "curl" and "POST" in args
                         and any(arg.endswith("/datastores") for arg in args))
            entries = json.loads(store[store.index("-d") + 1])["dataStore"]["connectionParameters"]["entry"]
            self.assertIn({"@key": "Primary key metadata table", "$": "geo.gt_pk_metadata"}, entries)
            publications = [json.loads(args[args.index("-d") + 1])["featureType"]
                            for name, args in calls if name == "curl" and "POST" in args
                            and any(arg.endswith("/featuretypes") for arg in args)]
            self.assertEqual(len(publications), 6)
            for feature_type in publications:
                self.assertEqual(feature_type["nativeBoundingBox"]["crs"], "EPSG:4326")
            env["PUBLISH_STATUS"] = "500"
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Could not publish", result.stderr)

    def test_empty_spatial_views_publish_with_explicit_bounds_and_stable_keys(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            env = self.stub_env(directory, log, HAS_GEOMETRY="f", IMPORT_BOUNDARIES="0",
                                DBE_DIR=str(directory / "no-dbe"))
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertEqual(result.returncode, 0, result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            for layer in ("school_locations_effective", "school_footprints", "school_boundaries"):
                keys = [args for name, args in calls if name == "docker" and "-c" in args
                        and f"'geo','{layer}','id',1,'assigned'" in args[args.index("-c") + 1]]
                self.assertEqual(len(keys), 1)
                bounds = next(args for name, args in calls if name == "curl" and "PUT" in args
                              and any(f"featuretypes/{layer}?" in arg for arg in args))
                self.assertTrue(any(arg.endswith("?recalculate=") for arg in bounds))
                payload = json.loads(bounds[bounds.index("-d") + 1])["featureType"]
                self.assertEqual(payload["nativeBoundingBox"]["minx"], -180)
                self.assertEqual(payload["latLonBoundingBox"]["maxy"], 90)
            store = next(args for name, args in calls if name == "curl" and "PUT" in args
                         and any(arg.endswith("/datastores/schools") for arg in args))
            entries = json.loads(store[store.index("-d") + 1])["dataStore"]["connectionParameters"]["entry"]
            settings = {entry["@key"]: entry["$"] for entry in entries}
            self.assertEqual(settings["Primary key metadata table"], "geo.gt_pk_metadata")
            self.assertEqual(settings["host"], "postgres")

    def test_rejects_unsafe_schema_and_layer_identifiers(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            for overrides in ({"PG_SCHEMA": "geo;drop"}, {"GEOSERVER_LAYERS": "school_locations;drop"}):
                env = self.stub_env(directory, log, **overrides)
                result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                        env=env, capture_output=True, text=True, check=False)
                self.assertNotEqual(result.returncode, 0)
            self.assertFalse(log.exists())

    def test_does_not_overwrite_custom_store_key_metadata(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            env = self.stub_env(directory, log, METADATA_TABLE="custom.existing_keys")
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("uses another primary-key metadata table", result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            self.assertFalse(any(name == "curl" and "PUT" in args for name, args in calls))

    def test_wfs_json_exception_is_not_accepted_as_an_empty_layer(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            env = self.stub_env(directory, log, WFS_BODY='{"exceptions":[{"text":"Layer failed"}]}',
                                GEOSERVER_LAYERS="school_locations", IMPORT_BOUNDARIES="0",
                                DBE_DIR=str(directory / "no-dbe"))
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("Expected a WFS feature collection", result.stderr)

    def test_real_coordinate_import_runs_before_bounds_refresh_when_masterlist_present(self):
        with tempfile.TemporaryDirectory() as directory:
            directory = Path(directory)
            log = directory / "calls.jsonl"
            dbe = directory / "dbe"
            dbe.mkdir()
            (dbe / "National.xlsx").write_bytes(b"placeholder")
            env = self.stub_env(directory, log, DBE_DIR=str(dbe), IMPORT_LOCATIONS="1",
                                GEOSERVER_LAYERS="school_locations", IMPORT_BOUNDARIES="0")
            result = subprocess.run(["bash", str(ROOT / "scripts/geoserver/bootstrap.sh")],
                                    env=env, capture_output=True, text=True, check=False)
            self.assertEqual(result.returncode, 0, result.stderr)
            calls = [json.loads(line) for line in log.read_text().splitlines()]
            imports = [i for i, (name, args) in enumerate(calls)
                       if name == "python3" and any("import_school_locations.py" in arg for arg in args)]
            puts = [i for i, (name, args) in enumerate(calls) if name == "curl" and "PUT" in args]
            self.assertEqual(len(imports), 1)
            self.assertEqual(len(puts), 1)
            self.assertLess(imports[0], puts[0])
            self.assertIn("real coordinates imported", result.stdout)



if __name__ == "__main__":
    unittest.main()
