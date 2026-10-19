"""Incomplete history must never overwrite known pass counts with zero."""

import importlib.util
import os
from pathlib import Path
import sqlite3
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts/import_nsc_history.py"
spec = importlib.util.spec_from_file_location("history", SCRIPT)
history = importlib.util.module_from_spec(spec)
spec.loader.exec_module(history)


class HistoryImportTests(unittest.TestCase):
    def test_recovers_unique_count_from_reported_rate(self):
        self.assertEqual(history.complete_result(10, None, 70.0), (7, 70.0))
        self.assertEqual(history.complete_result(13, None, 23.1), (3, 23.1))

    def test_unknown_ambiguous_and_inconsistent_counts_are_not_invented(self):
        self.assertIsNone(history.complete_result(10, None, None))
        self.assertIsNone(history.complete_result(10000, None, 70.0))
        self.assertIsNone(history.complete_result(3, None, 50.0))
        self.assertIsNone(history.complete_result(10, 11, 100.0))

    def test_preserves_real_zero_and_derives_missing_percentage(self):
        self.assertEqual(history.complete_result(10, 0, None), (0, 0.0))
        self.assertEqual(history.complete_result(10, 7, None), (7, 70.0))
        self.assertEqual(history.complete_result(0, None, None), (0, 0.0))

    def run_import(self, rows):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "history.db"
            with sqlite3.connect(path) as db:
                db.execute("CREATE TABLE results (emis_number TEXT, year INTEGER, progressed INTEGER, "
                           "total_wrote INTEGER, total_achieved INTEGER, percent_achieved REAL)")
                db.executemany("INSERT INTO results VALUES (?,?,?,?,?,?)", rows)
            return subprocess.run(
                ["python3", "-B", str(SCRIPT)],
                env=dict(os.environ, NSC_DB_PATH=str(path)),
                capture_output=True, text=True, check=False,
            )

    def test_emitted_sql_recovers_passes_and_skips_unrecoverable_rows(self):
        result = self.run_import([
            ("700400931", 2022, None, 10, None, 70.0),
            ("unknown", 2022, None, 10, None, None),
            ("zero", 2022, None, 10, 0, 0.0),
        ])
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("('700400931',2022,0,10,7,70.0)", result.stdout)
        self.assertIn("('zero',2022,0,10,0,0.0)", result.stdout)
        self.assertNotIn("'unknown'", result.stdout)
        self.assertIn("Skipping incomplete or invalid result: EMIS unknown", result.stderr)
        self.assertTrue(result.stdout.endswith("COMMIT;\n"))

    def test_all_incomplete_rows_refuse_import_without_emitting_sql(self):
        result = self.run_import([("unknown", 2022, None, 10, None, None)])
        self.assertEqual(result.returncode, 1)
        self.assertEqual(result.stdout, "")

    def test_subject_import_wrapper_is_executable(self):
        self.assertTrue(os.access(ROOT / "scripts/import_subject_results.sh", os.X_OK))
