"""Regression coverage for the production map service wiring and provisioning."""

import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import unittest
from unittest.mock import patch
from urllib.error import HTTPError


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("production_geoserver", ROOT / "scripts/geoserver/production.py")
PRODUCTION = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PRODUCTION)

ENV = {
    "GEOSERVER_BASE_URL": "http://geoserver:8080/geoserver",
    "GEOSERVER_ADMIN_USER": "admin",
    "GEOSERVER_ADMIN_PASSWORD": "test-admin-password",
    "GEOSERVER_DB_HOST": "main-db",
    "GEOSERVER_DB_NAME": "postgres",
    "GEOSERVER_DB_USER": "map_reader",
    "GEOSERVER_DB_PASSWORD": "test-db-password",
}


class ProvisioningTests(unittest.TestCase):
    @patch.dict(os.environ, ENV, clear=True)
    def test_new_catalog_uses_main_database_and_publishes_all_layers(self):
        with patch.object(PRODUCTION, "request", return_value=None) as request:
            PRODUCTION.configure()
        calls = request.call_args_list
        store_call = next(c for c in calls if c.args[:2] == ("POST", "workspaces/matric/datastores"))
        parameters = {e["@key"]: e["$"] for e in store_call.args[2]["dataStore"]["connectionParameters"]["entry"]}
        self.assertEqual(parameters["host"], "main-db")
        self.assertEqual(parameters["database"], "postgres")
        self.assertEqual(parameters["schema"], "geo")
        self.assertEqual(parameters["SSL mode"], "require")
        self.assertEqual(parameters["Primary key metadata table"], "geo.gt_pk_metadata")
        layers = [c.args[2]["featureType"]["name"] for c in calls
                  if c.args[0] == "POST" and c.args[1].endswith("/featuretypes")]
        self.assertEqual(layers, list(PRODUCTION.LAYERS))

    @patch.dict(os.environ, dict(ENV, GEOSERVER_DB_SSLMODE="disable"), clear=True)
    def test_existing_catalog_updates_connection_without_duplicate_layers(self):
        with patch.object(PRODUCTION, "request", return_value=b"{}") as request:
            PRODUCTION.configure()
        self.assertFalse(any(c.args[0] == "POST" for c in request.call_args_list))
        update = next(c for c in request.call_args_list
                      if c.args[:2] == ("PUT", "workspaces/matric/datastores/schools"))
        parameters = {e["@key"]: e["$"] for e in update.args[2]["dataStore"]["connectionParameters"]["entry"]}
        self.assertEqual(parameters["SSL mode"], "disable")

    @patch.dict(os.environ, ENV, clear=True)
    def test_configuration_failure_does_not_continue_publishing(self):
        with patch.object(PRODUCTION, "request", side_effect=RuntimeError("Unavailable")) as request:
            with self.assertRaises(RuntimeError):
                PRODUCTION.configure()
        self.assertEqual(request.call_count, 1)

    @patch.dict(os.environ, ENV, clear=True)
    def test_only_get_404_is_treated_as_missing(self):
        error = HTTPError("http://geoserver", 404, "missing", {}, None)
        with patch.object(PRODUCTION, "urlopen", side_effect=error):
            self.assertIsNone(PRODUCTION.request("GET", "workspaces/matric.json"))
            with self.assertRaisesRegex(RuntimeError, "HTTP 404"):
                PRODUCTION.request("POST", "workspaces", {})


@unittest.skipUnless(shutil.which("docker"), "Docker CLI required")
class ComposeTests(unittest.TestCase):
    def test_private_geoserver_and_frontend_readiness_chain(self):
        env = dict(os.environ, **ENV)
        for key in ("DATABASE_CONNECTION_STRING", "JWT_SECRET", "EXT_AUTH_ENCRYPTION_KEY",
                    "FILE_STORAGE_ENDPOINT", "FILE_STORAGE_ACCESS_KEY", "FILE_STORAGE_SECRET_KEY",
                    "MEILI_URL", "MEILI_MASTER_KEY", "TURNSTILE_SECRET_KEY", "TURNSTILE_SITE_KEY",
                    "TRUSTED_PROXY_NETWORK", "FRONTEND_URL"):
            env[key] = "validation-only"
        result = subprocess.run(
            ["docker", "compose", "--env-file", "/dev/null", "-f",
             str(ROOT / "docker-compose.production.yml"), "config", "--format", "json"],
            env=env, capture_output=True, text=True, check=True,
        )
        config = json.loads(result.stdout)
        services = config["services"]
        self.assertNotIn("db", services)
        self.assertNotIn("ports", services["geoserver"])
        self.assertIn("external-services", services["geoserver"]["networks"])
        self.assertEqual(services["api"]["environment"]["GeoServer__BaseUrl"], "http://geoserver:8080/geoserver")
        self.assertEqual(services["frontend"]["depends_on"]["geoserver-init"]["condition"],
                         "service_completed_successfully")
        self.assertEqual(services["geoserver-init"]["depends_on"]["api"]["condition"], "service_healthy")
        self.assertEqual(services["geoserver-init"]["depends_on"]["geoserver"]["condition"], "service_healthy")
        self.assertEqual(services["geoserver"]["volumes"][0]["source"], "geoserver-data")


if __name__ == "__main__":
    unittest.main()
