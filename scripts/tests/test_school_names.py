"""School-name cleaning regressions.

Run: python3 -B -m unittest discover -s scripts/tests -v
"""

import importlib.util
import tempfile
import unittest
from pathlib import Path

from test_spatial_operations import write_masterlist

ROOT = Path(__file__).resolve().parents[2]


def load_script(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


NAMES = load_script("names", "scripts/clean_school_names.py")


class SchoolNameNormalizationTests(unittest.TestCase):
    def test_expands_secondary_tails(self):
        self.assertEqual(
            NAMES.normalize_school_name("BHOVULENGWE S S S"),
            "BHOVULENGWE SECONDARY SCHOOL",
        )
        self.assertEqual(
            NAMES.normalize_school_name("TSITSANA COMP S S SCHOOL"),
            "TSITSANA COMPREHENSIVE SECONDARY SCHOOL",
        )
        self.assertEqual(
            NAMES.normalize_school_name("BOKAMOSO SI/S"),
            "BOKAMOSO SECONDARY INDEPENDENT SCHOOL",
        )

    def test_uses_phase_for_ambiguous_tails(self):
        self.assertEqual(
            NAMES.normalize_school_name("RHEEDERPARK C/S", "COMBINED SCHOOL"),
            "RHEEDERPARK COMBINED SCHOOL",
        )
        self.assertEqual(
            NAMES.normalize_school_name("SOMEPLACE C/S", "INTERMEDIATE SCHOOL"),
            "SOMEPLACE INTERMEDIATE SCHOOL",
        )

    def test_keeps_initials_and_personal_names(self):
        self.assertEqual(
            NAMES.normalize_school_name("S.K. MAKHANYA HIGH SCHOOL"),
            "S.K. MAKHANYA HIGH SCHOOL",
        )
        self.assertEqual(
            NAMES.normalize_school_name("ST. ANDREW'S SEK."),
            "ST. ANDREW'S SECONDARY SCHOOL",
        )

    def test_appends_phase_when_official_name_has_no_school_type(self):
        self.assertEqual(
            NAMES.normalize_school_name("LEKGARIETSE", "SECONDARY SCHOOL"),
            "LEKGARIETSE SECONDARY SCHOOL",
        )
        self.assertEqual(
            NAMES.normalize_school_name("BOSCHKLOOF INTERMEDIATE", "COMBINED SCHOOL"),
            "BOSCHKLOOF INTERMEDIATE SCHOOL",
        )

    def test_repairs_known_masterlist_character_glitches(self):
        self.assertEqual(
            NAMES.normalize_school_name("BELLVILLE HOﾋR TEGNIESE SKOOL"),
            "BELLVILLE HOËR TEGNIESE SKOOL",
        )
        self.assertEqual(
            NAMES.normalize_school_name("ASHTON SEKONDﾊRE SKOOL"),
            "ASHTON SEKONDÊRE SKOOL",
        )


class SchoolNameBuildTests(unittest.TestCase):
    HEADERS = ("NatEmis", "Official_Institution_Name", "Phase_PED")

    def test_matches_masterlist_and_flags_missing_rows(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "National.xlsx"
            write_masterlist(
                path,
                self.HEADERS,
                [
                    ["200500315", "BHOVULENGWE  SECONDARY SCHOOL", "SECONDARY SCHOOL"],
                    ["200500316", "NAMPO AS/S", "SECONDARY SCHOOL"],
                ],
            )
            master = NAMES.read_masterlist_names(str(path))

        report = [
            {
                "emis_number": "200500315 ",
                "centre_number": "4241040 ",
                "centre_name": " BHOVULENGWE S S S ",
                "province": "EASTERN CAPE",
                "district_name": "ALFRED NZO EAST",
            },
            {
                "emis_number": "200500316",
                "centre_number": "4241041",
                "centre_name": "NAMPO ASS",
                "province": "FREE STATE",
                "district_name": "FEZILE DABI",
            },
            {
                "emis_number": "999999999",
                "centre_number": "5000001",
                "centre_name": "MQULELA JS",
                "province": "KWAZULU-NATAL",
                "district_name": "UMGUNGUNDLOVU",
            },
            {
                "emis_number": "0",
                "centre_number": "8888001",
                "centre_name": " DESIGNATED CENTRE EXAMPLE ",
                "province": "GAUTENG",
                "district_name": "JOHANNESBURG",
            },
        ]
        rows = NAMES.build_rows(report, master)

        self.assertEqual([row["match_status"] for row in rows], [
            "matched", "matched", "unmatched", "placeholder_emis",
        ])
        self.assertEqual(rows[0]["clean_school_name"], "BHOVULENGWE SECONDARY SCHOOL")
        self.assertEqual(rows[1]["clean_school_name"], "NAMPO AGRICULTURAL SECONDARY SCHOOL")
        self.assertEqual(rows[2]["clean_school_name"], "MQULELA JUNIOR SECONDARY SCHOOL")
        self.assertEqual(rows[3]["clean_school_name"], "DESIGNATED CENTRE EXAMPLE")
        self.assertEqual(rows[0]["emis_number"], "200500315")
        self.assertEqual(rows[0]["name_source"], "DBE National masterlist")
        self.assertEqual(rows[2]["name_source"], "NSC report")
        self.assertIn("No match", rows[2]["review_note"])
        self.assertIn("Placeholder", rows[3]["review_note"])


if __name__ == "__main__":
    unittest.main()
