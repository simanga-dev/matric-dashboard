"""Bootstrap script safety and ASP.NET Identity V3 hash format tests."""

import base64
import hashlib
import importlib.util
from pathlib import Path
import struct
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("create_admin", Path(__file__).resolve().parents[1] / "create_admin.py")
ADMIN = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ADMIN)


class CreateAdminTests(unittest.TestCase):
    def test_identity_v3_sha512_hash(self):
        raw = base64.b64decode(ADMIN.password_hash("TestPassword123!"))
        self.assertEqual(raw[0], 1)
        self.assertEqual(struct.unpack(">III", raw[1:13]), (2, 100_000, 16))
        self.assertEqual(len(raw), 61)
        self.assertEqual(raw[29:], hashlib.pbkdf2_hmac("sha512", b"TestPassword123!", raw[13:29], 100_000, 32))

    def test_hashes_have_random_salts(self):
        self.assertNotEqual(ADMIN.password_hash("TestPassword123!"), ADMIN.password_hash("TestPassword123!"))

    def test_sql_creates_only_admin_and_never_includes_plaintext_password(self):
        sql = ADMIN.build_sql("owner@example.com", "TestPassword123!")
        self.assertNotIn("TestPassword123!", sql)
        self.assertIn("'OWNER@EXAMPLE.COM'", sql)
        self.assertIn('"Name" = \'Admin\'', sql)
        self.assertNotIn("Superuser", sql)
        self.assertNotIn("UPDATE", sql)
        self.assertIn("Account already exists", sql)
        self.assertIn("LOCK TABLE", sql)

    def test_rejects_weak_password_and_unsafe_email(self):
        for email, password in [("owner@example.com", "weak"), ("bad@example.com'; DROP TABLE x;", "TestPassword123!"),
                                ("bad$bootstrap$@example.com", "TestPassword123!")]:
            with self.subTest(email=email), self.assertRaises(ValueError):
                ADMIN.build_sql(email, password)

    def test_refuses_ambiguous_container_selection(self):
        with patch.object(ADMIN.subprocess, "check_output", return_value='{"Names":"a","Image":"postgres:18"}\n{"Names":"b","Image":"postgis/postgis:18-3.6"}'):
            with self.assertRaises(ValueError):
                ADMIN.find_container()

    def test_auto_detects_current_postgis_container(self):
        with patch.object(ADMIN.subprocess, "check_output", return_value='{"Names":"db-task","Image":"postgis/postgis:18-3.6"}'):
            self.assertEqual(ADMIN.find_container(), "db-task")


if __name__ == "__main__":
    unittest.main()
