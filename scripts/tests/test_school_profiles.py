"""School profile and enrolment import regressions.

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


PROFILES = load_script("profiles", "scripts/import_school_profiles.py")

HEADERS = [
    "NatEmis",
    "DataYear",
    "Sector",
    "Phase_PED",
    "OwnerLand",
    "OwnerBuild",
    "Email",
    "Telephone",
    "Section21",
    "NoFeeSchool",
    "Urban_Rural",
    "Learners2025",
    "Educators2025",
    "Quintile",
]


def masterlist_row(**overrides):
    row = {
        "NatEmis": "200500039",
        "DataYear": "2025",
        "Sector": "PUBLIC",
        "Phase_PED": "SECONDARY SCHOOL",
        "OwnerLand": "GOVERNMENT",
        "OwnerBuild": "Govt Education",
        "Email": "Admin@Example.CO.ZA",
        "Telephone": "475770029",
        "Section21": "Yes",
        "NoFeeSchool": "No Fee",
        "Urban_Rural": "Urban",
        "Learners2025": "1933",
        "Educators2025": "49",
        "Quintile": "Q3",
    }
    row.update(overrides)
    return row


class TelephoneNormalizationTests(unittest.TestCase):
    def test_adds_country_code_to_trunkless_nine_digit_numbers(self):
        self.assertEqual(PROFILES.normalize_telephone("475770029"), "+27475770029")

    def test_replaces_trunk_zero(self):
        self.assertEqual(PROFILES.normalize_telephone("0475770029"), "+27475770029")

    def test_keeps_existing_country_code(self):
        self.assertEqual(PROFILES.normalize_telephone("+27 47 577 0029"), "+27475770029")
        self.assertEqual(PROFILES.normalize_telephone("0027475770029"), "+27475770029")

    def test_drops_sentinels_and_unusable_lengths(self):
        for value in ("", None, "99", "0", "66482932", "1111111111", "530539941831"):
            self.assertIsNone(PROFILES.normalize_telephone(value), value)


class EmailNormalizationTests(unittest.TestCase):
    def test_lowercases_and_trims(self):
        self.assertEqual(PROFILES.normalize_email("  Admin@Example.CO.ZA "), "admin@example.co.za")

    def test_drops_blanks_sentinels_and_non_addresses(self):
        for value in ("", None, "99", "not-an-address"):
            self.assertIsNone(PROFILES.normalize_email(value), value)


class OwnershipNormalizationTests(unittest.TestCase):
    def test_collapses_government_spellings(self):
        for raw in ("GOVERNMENT", "Govt", "PUBLIC", "State", "Govt Education", "Municipality"):
            normalized, kept = PROFILES.normalize_ownership(raw)
            self.assertEqual(normalized, "Government", raw)
            self.assertEqual(kept, raw)

    def test_keeps_independent_distinct(self):
        self.assertEqual(PROFILES.normalize_ownership("INDEPENDENT")[0], "Independent")

    def test_maps_unknown_and_free_text(self):
        self.assertEqual(PROFILES.normalize_ownership("UNKNOWN")[0], "Unknown")
        self.assertEqual(PROFILES.normalize_ownership("MJB PROPERTIES")[0], "Private")

    def test_missing_values_have_no_normalized_ownership(self):
        self.assertEqual(PROFILES.normalize_ownership("99"), (None, "99"))
        self.assertEqual(PROFILES.normalize_ownership(""), (None, None))
        self.assertEqual(PROFILES.normalize_ownership(None), (None, None))


class OtherColumnNormalizationTests(unittest.TestCase):
    def test_yes_no_columns(self):
        self.assertIs(PROFILES.normalize_bool("Yes"), True)
        self.assertIs(PROFILES.normalize_bool("N"), False)
        self.assertIsNone(PROFILES.normalize_bool("99"))

    def test_no_fee_column_mixes_two_vocabularies(self):
        self.assertIs(PROFILES.normalize_no_fee("No Fee"), True)
        self.assertIs(PROFILES.normalize_no_fee("YES"), True)
        self.assertIs(PROFILES.normalize_no_fee("Fee Charging"), False)
        self.assertIs(PROFILES.normalize_no_fee("NO"), False)
        self.assertIsNone(PROFILES.normalize_no_fee("99"))

    def test_settlement(self):
        self.assertEqual(PROFILES.normalize_settlement("URBAN"), "Urban")
        self.assertEqual(PROFILES.normalize_settlement("Rural"), "Rural")
        self.assertEqual(PROFILES.normalize_settlement("TO BE UPDATED"), None)
        self.assertEqual(PROFILES.normalize_settlement("Elsewhere"), "Unknown")

    def test_sector_and_phase(self):
        self.assertEqual(PROFILES.normalize_sector("PUBLIC"), "Public")
        self.assertEqual(PROFILES.normalize_sector("INDEPENDENT"), "Independent")
        self.assertEqual(PROFILES.normalize_phase("COMBINED SCHOOL"), "Combined")
        self.assertEqual(PROFILES.normalize_phase("SCHOOL OF SKILLS"), "Skills")
        self.assertEqual(PROFILES.normalize_phase("SOMETHING NEW"), "Other")
        self.assertEqual(PROFILES.normalize_phase("99"), None)

    def test_quintile_keeps_raw_code(self):
        self.assertEqual(PROFILES.normalize_quintile("Q1"), (1, "Q1"))
        self.assertEqual(PROFILES.normalize_quintile("q5"), (5, "q5"))
        self.assertEqual(PROFILES.normalize_quintile("N/A"), (None, "N/A"))
        self.assertEqual(PROFILES.normalize_quintile("99"), (None, "99"))
        self.assertEqual(PROFILES.normalize_quintile(""), (None, None))

    def test_counts(self):
        self.assertEqual(PROFILES.parse_count("1933"), 1933)
        self.assertEqual(PROFILES.parse_count(""), None)
        self.assertEqual(PROFILES.parse_count("99"), None)
        self.assertEqual(PROFILES.parse_count("n/a"), None)


class ProfileRowTests(unittest.TestCase):
    def test_build_profile_normalizes_the_whole_row(self):
        profile = PROFILES.build_profile(masterlist_row())
        self.assertEqual(profile["email"], "admin@example.co.za")
        self.assertEqual(profile["telephone"], "+27475770029")
        self.assertEqual(profile["land_ownership"], "Government")
        self.assertEqual(profile["building_ownership"], "Government")
        self.assertEqual(profile["building_raw"], "Govt Education")
        self.assertIs(profile["section21"], True)
        self.assertIs(profile["no_fee"], True)
        self.assertEqual(profile["urban_rural"], "Urban")
        self.assertEqual(profile["sector"], "Public")
        self.assertEqual(profile["phase"], "Secondary")
        self.assertEqual(profile["quintile"], 3)
        self.assertEqual(profile["quintile_code"], "Q3")
        self.assertEqual(profile["data_year"], 2025)
        self.assertEqual(profile["learners"], 1933)
        self.assertEqual(profile["educators"], 49)

    def test_build_sql_updates_schools_and_upserts_enrolments(self):
        sql = PROFILES.build_sql([("200500039", PROFILES.build_profile(masterlist_row()))])
        self.assertIn('UPDATE marks."Schools"', sql)
        self.assertIn('"MasterlistDataYear" = v.data_year', sql)
        self.assertIn('INSERT INTO marks."SchoolEnrolments"', sql)
        self.assertIn('ON CONFLICT ("SchoolId","Year") DO UPDATE', sql)
        self.assertIn("'200500039'::text", sql)
        self.assertIn("1933::integer", sql)
        self.assertIn('"Quintile" = COALESCE(v.quintile, s."Quintile")', sql)
        self.assertTrue(sql.startswith("BEGIN;"))

    def test_build_sql_skips_enrolment_when_counts_are_missing(self):
        row = masterlist_row(Learners2025="", Educators2025="")
        sql = PROFILES.build_sql([("200500039", PROFILES.build_profile(row))])
        self.assertIn('UPDATE marks."Schools"', sql)
        self.assertNotIn('INSERT INTO marks."SchoolEnrolments"', sql)

    def test_build_sql_quotes_apostrophes(self):
        row = masterlist_row(OwnerLand="NOAH'S ARK CHILDREN'S HOME TRUST")
        sql = PROFILES.build_sql([("200500039", PROFILES.build_profile(row))])
        self.assertIn("NOAH''S ARK CHILDREN''S HOME TRUST", sql)


class MasterlistReaderTests(unittest.TestCase):
    def test_reads_rows_keyed_by_emis(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "National.xlsx"
            write_masterlist(
                path,
                HEADERS,
                [
                    [
                        "200500039", "2025", "PUBLIC", "SECONDARY SCHOOL", "GOVERNMENT",
                        "Govt", "admin@example.co.za", "475770029", "Yes", "No Fee",
                        "Urban", "1933", "49", "Q2",
                    ],
                    [
                        "0", "2025", "PUBLIC", "PRIMARY SCHOOL", "GOVERNMENT",
                        "Govt", "b@example.co.za", "475770030", "No", "No Fee",
                        "Rural", "10", "1", "N/A",
                    ],
                ],
            )
            records = PROFILES.read_masterlist(str(path))

        self.assertEqual(list(records), ["200500039"])
        self.assertEqual(records["200500039"]["OwnerLand"], "GOVERNMENT")

    def test_rejects_workbook_without_ownership_columns(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "National.xlsx"
            write_masterlist(
                path,
                ["NatEmis", "DataYear", "Sector"],
                [["200500039", "2025", "PUBLIC"]],
            )
            with self.assertRaises(ValueError):
                PROFILES.read_masterlist(str(path))


if __name__ == "__main__":
    unittest.main()
