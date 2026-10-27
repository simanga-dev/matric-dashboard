#!/usr/bin/env python3
"""Emit SQL that imports school contact, ownership, and enrolment data.

Reads the DBE EMIS masterlist (xlsx) named by DBE_PRIMARY and updates the
profile columns on `marks."Schools"` plus one `marks."SchoolEnrolments"` row per
school for the masterlist's data year. Only schools that already exist in
`marks."Schools"` (matched by EMIS number) are touched, so this enriches the NSC
roster without inventing schools.

The masterlist is dirty, so values are normalized before use:

* `OwnerLand` / `OwnerBuild` have 45 and 84 distinct spellings. Values collapse
  to Government, Private, Independent, Unknown, or NotProvided, and the original
  string is kept in the `*Raw` column.
* `Telephone` is a bare 9-digit number with the trunk zero dropped
  (`475770029` is `047 577 0029`), and uses `99` / `0` as "no value" sentinels.
* `Learners2025` / `Educators2025` become the enrolment row for `DataYear`.

SQL is written to stdout - pipe it into psql:
    python3 scripts/import_school_profiles.py | psql ...

Environment:
    CSV_PATH     default <repo root>/2024_nsc_school_performance_report.csv
    DBE_DIR      default <home>/Documents/dbe-emis-downloads
    DBE_PRIMARY  default <DBE_DIR>/National.xlsx
"""

import os
import re
import sys
import xml.etree.ElementTree as ElementTree
from zipfile import ZipFile

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import import_school_locations as locations

DEFAULT_CSV = os.path.normpath(
    os.path.join(SCRIPT_DIR, "..", "2024_nsc_school_performance_report.csv")
)
DEFAULT_DBE_DIR = os.path.join(os.path.expanduser("~"), "Documents", "dbe-emis-downloads")
DEFAULT_PRIMARY = "National.xlsx"

CSV_PATH = os.environ.get("CSV_PATH", DEFAULT_CSV)
DBE_DIR = os.environ.get("DBE_DIR", DEFAULT_DBE_DIR)
PRIMARY_PATH = os.environ.get("DBE_PRIMARY", os.path.join(DBE_DIR, DEFAULT_PRIMARY))

# Values the masterlist uses for "no data". "99" is a placeholder across columns.
SENTINELS = {"", "99", "-", "N/A", "NA", ".", "TO BE UPDATED"}

# Land and building ownership collapse to a single enum. Unrecognised values are
# free-text names of private owners ("MJB PROPERTIES", "MS. NTULI", "AFM CHURCH"),
# so they map to Private.
OWNERSHIP = {
    "GOVERNMENT": "Government",
    "GOVT": "Government",
    "GOVT EDUCATION": "Government",
    "GOVT OTHER": "Government",
    "PUBLIC": "Government",
    "STATE": "Government",
    "DOE NORTHERN CAPE": "Government",
    "MUNICIPALITY": "Government",
    "INDEPENDENT": "Independent",
    "UNKNOWN": "Unknown",
    "OTHER": "Unknown",
    "PRIVATE": "Private",
    "PRIVATE COMPANY": "Private",
    "COMPANY": "Private",
    "FARM": "Private",
    "CHURCH": "Private",
    "MINE": "Private",
    "FACTORY": "Private",
    "HOSPITAL": "Private",
    "TRUST LAND": "Private",
    "TRUST": "Private",
    "TRIBAL": "Private",
}

SETTLEMENT = {"URBAN": "Urban", "RURAL": "Rural", "UNKNOWN": "Unknown"}
SECTOR = {"PUBLIC": "Public", "INDEPENDENT": "Independent"}
PHASE = {
    "PRIMARY SCHOOL": "Primary",
    "SECONDARY SCHOOL": "Secondary",
    "COMBINED SCHOOL": "Combined",
    "INTERMEDIATE SCHOOL": "Intermediate",
    "SPECIAL NEEDS EDUCATION SCHOOL": "SpecialNeeds",
    "SPECIAL NEEDS EDUCATION CENTRE": "SpecialNeeds",
    "SCHOOL OF SKILLS": "Skills",
}

MAX_EMAIL = 320
MAX_OWNERSHIP_RAW = 100


def collapse(value: object) -> str:
    """Trim and collapse internal whitespace in a cell value."""
    return re.sub(r"\s+", " ", str(value or "").strip())


def _is_sentinel(text: str) -> bool:
    return text.upper() in SENTINELS


def normalize_email(value: object) -> str | None:
    """Return a usable email address, or None when the cell has no address."""
    text = collapse(value)
    if _is_sentinel(text) or "@" not in text:
        return None
    # Cells are occasionally padded past the RFC limit; drop rather than truncate.
    return text.lower() if len(text) <= MAX_EMAIL else None


def normalize_telephone(value: object) -> str | None:
    """Return a South African number in E.164 form, or None when unusable.

    The masterlist stores most numbers as 9 digits with the trunk zero dropped.
    10-digit numbers keep the zero, and a few already carry the 27 country code.
    Anything that does not fit those shapes is left out rather than guessed.
    """
    digits = re.sub(r"\D", "", str(value or ""))
    if digits in ("", "0", "99"):
        return None
    if len(digits) == 9:
        return "+27" + digits
    if len(digits) == 10 and digits.startswith("0"):
        return "+27" + digits[1:]
    if len(digits) == 11 and digits.startswith("27"):
        return "+" + digits
    if len(digits) == 13 and digits.startswith("0027"):
        return "+" + digits[2:]
    return None


def normalize_ownership(value: object) -> tuple[str | None, str | None]:
    """Return (normalized ownership, raw value) for a masterlist ownership cell."""
    text = collapse(value)
    if not text or text.upper() == "99":
        return None, text or None
    normalized = OWNERSHIP.get(text.upper())
    if normalized is None:
        normalized = "Private"
    return normalized, text[:MAX_OWNERSHIP_RAW]


def normalize_bool(value: object) -> bool | None:
    """Return a yes/no cell as a bool, or None when it carries no answer."""
    text = collapse(value).upper()
    if text in ("YES", "Y", "TRUE", "1"):
        return True
    if text in ("NO", "N", "FALSE", "0"):
        return False
    return None


def normalize_no_fee(value: object) -> bool | None:
    """Return whether the school charges no fees, or None when the cell has no answer.

    The column mixes two vocabularies: "No Fee" / "Fee Charging" and plain yes/no.
    """
    text = collapse(value).upper()
    if text in ("NO FEE", "NOFEE", "YES", "Y", "TRUE", "1"):
        return True
    if text in ("FEE CHARGING", "FEECHARGING", "NO", "N", "FALSE", "0"):
        return False
    return None


def normalize_settlement(value: object) -> str | None:
    """Return the urban/rural enum name, or None when the cell has no answer."""
    text = collapse(value).upper()
    if _is_sentinel(text):
        return None
    return SETTLEMENT.get(text, "Unknown")


def normalize_sector(value: object) -> str | None:
    """Return the sector enum name, or None when the cell has no answer."""
    text = collapse(value).upper()
    if _is_sentinel(text):
        return None
    return SECTOR.get(text, "Unknown")


def normalize_phase(value: object) -> str | None:
    """Return the phase enum name, or None when the cell has no answer."""
    text = collapse(value).upper()
    if _is_sentinel(text):
        return None
    return PHASE.get(text, "Other")


def normalize_quintile(value: object) -> tuple[int | None, str | None]:
    """Return (numeric quintile, raw code) for a masterlist quintile cell.

    The raw code is kept so "N/A" (not applicable) stays distinguishable from
    "99" (not captured) and from a published Q1-Q5.
    """
    raw = collapse(value)
    if not raw:
        return None, None
    text = raw.upper()
    if text.startswith("Q") and text[1:].isdigit():
        number = int(text[1:])
        return (number if 1 <= number <= 5 else None), raw[:10]
    return None, raw[:10]


def parse_count(value: object) -> int | None:
    """Return a non-negative whole number, or None when the cell has no figure."""
    text = collapse(value)
    if _is_sentinel(text):
        return None
    try:
        number = int(float(text))
    except ValueError:
        return None
    return number if number >= 0 else None


def read_masterlist(path: str) -> dict[str, dict[str, str]]:
    """Read the masterlist into {EMIS: row}, keyed by the NatEmis column."""
    if not os.path.exists(path):
        raise ValueError(
            f"DBE masterlist not found at {path}. Set DBE_DIR or DBE_PRIMARY."
        )
    records: dict[str, dict[str, str]] = {}
    with ZipFile(path) as archive:
        shared: list[str] = []
        if "xl/sharedStrings.xml" in archive.namelist():
            root = ElementTree.fromstring(archive.read("xl/sharedStrings.xml"))
            shared = ["".join(item.itertext()) for item in root]
        headers: dict[str, str] = {}
        with archive.open(locations._first_sheet_path(archive)) as sheet:
            for _, row in ElementTree.iterparse(sheet, events=("end",)):
                if row.tag != locations._ROW:
                    continue
                cells = {
                    "".join(char for char in cell.get("r", "") if char.isalpha()):
                    locations._cell_text(cell, shared)
                    for cell in row.findall(locations._CELL)
                }
                if not headers:
                    headers = cells
                    continue
                record = {headers.get(key, key): value for key, value in cells.items()}
                emis = collapse(record.get("NatEmis"))
                if emis and not locations._is_placeholder_emis(emis):
                    records[emis] = record
                row.clear()
        required = {"NatEmis", "DataYear", "OwnerLand", "OwnerBuild"}
        missing = required - set(headers.values())
        if missing:
            raise ValueError(f"{path}: missing columns {', '.join(sorted(missing))}")
    return records


def build_profile(record: dict[str, str]) -> dict:
    """Normalize one masterlist row into the values written to the database."""
    land_ownership, land_raw = normalize_ownership(record.get("OwnerLand"))
    building_ownership, building_raw = normalize_ownership(record.get("OwnerBuild"))
    quintile, quintile_code = normalize_quintile(record.get("Quintile"))
    data_year = parse_count(record.get("DataYear"))
    return {
        "email": normalize_email(record.get("Email")),
        "telephone": normalize_telephone(record.get("Telephone")),
        "land_ownership": land_ownership,
        "land_raw": land_raw,
        "building_ownership": building_ownership,
        "building_raw": building_raw,
        "section21": normalize_bool(record.get("Section21")),
        "no_fee": normalize_no_fee(record.get("NoFeeSchool")),
        "urban_rural": normalize_settlement(record.get("Urban_Rural")),
        "sector": normalize_sector(record.get("Sector")),
        "phase": normalize_phase(record.get("Phase_PED")),
        "quintile": quintile,
        "quintile_code": quintile_code,
        "data_year": data_year,
        "learners": parse_count(record.get("Learners2025")),
        "educators": parse_count(record.get("Educators2025")),
    }


def _text(value: object) -> str:
    if value is None:
        return "NULL::text"
    return "'" + str(value).replace("'", "''") + "'::text"


def _int(value: object) -> str:
    return "NULL::integer" if value is None else f"{int(value)}::integer"


def _bool(value: object) -> str:
    if value is None:
        return "NULL::boolean"
    return "true::boolean" if value else "false::boolean"


def build_sql(profiles: list[tuple[str, dict]]) -> str:
    """Render the profile update and enrolment upsert as one transaction."""
    if not profiles:
        return ""

    profile_rows = ",\n".join(
        "(" + ",".join(
            (
                _text(emis),
                _text(p["email"]),
                _text(p["telephone"]),
                _text(p["land_ownership"]),
                _text(p["land_raw"]),
                _text(p["building_ownership"]),
                _text(p["building_raw"]),
                _bool(p["section21"]),
                _bool(p["no_fee"]),
                _text(p["urban_rural"]),
                _text(p["sector"]),
                _text(p["phase"]),
                _text(p["quintile_code"]),
                _int(p["quintile"]),
                _int(p["data_year"]),
            )
        ) + ")"
        for emis, p in profiles
    )

    enrolment_rows = ",\n".join(
        "("
        + ",".join((_text(emis), _int(p["data_year"]), _int(p["learners"]), _int(p["educators"])))
        + ")"
        for emis, p in profiles
        if p["data_year"] is not None
        and (p["learners"] is not None or p["educators"] is not None)
    )

    out = ["BEGIN;\n"]
    out.append(
        'UPDATE marks."Schools" AS s SET\n'
        '    "Email" = v.email,\n'
        '    "Telephone" = v.telephone,\n'
        '    "LandOwnership" = v.land_ownership,\n'
        '    "LandOwnershipRaw" = v.land_raw,\n'
        '    "BuildingOwnership" = v.building_ownership,\n'
        '    "BuildingOwnershipRaw" = v.building_raw,\n'
        '    "IsSection21" = v.section21,\n'
        '    "IsNoFeeSchool" = v.no_fee,\n'
        '    "UrbanRural" = v.urban_rural,\n'
        '    "Sector" = v.sector,\n'
        '    "Phase" = v.phase,\n'
        '    "Quintile" = COALESCE(v.quintile, s."Quintile"),\n'
        '    "QuintileCode" = v.quintile_code,\n'
        '    "MasterlistDataYear" = v.data_year,\n'
        '    "UpdatedAt" = now()\n'
        "FROM (VALUES\n" + profile_rows + "\n"
        ") AS v(emis, email, telephone, land_ownership, land_raw, building_ownership,"
        " building_raw, section21, no_fee, urban_rural, sector, phase, quintile_code,"
        " quintile, data_year)\n"
        'WHERE s."EmisNumber" = v.emis AND s."IsDeleted" = false;\n'
    )
    if enrolment_rows:
        out.append(
            'INSERT INTO marks."SchoolEnrolments" '
            '("Id","SchoolId","Year","Learners","Educators","CreatedAt","IsDeleted")\n'
            'SELECT gen_random_uuid(), s."Id", v.year, v.learners, v.educators, now(), false\n'
            "FROM (VALUES\n" + enrolment_rows + "\n"
            ") AS v(emis, year, learners, educators)\n"
            'JOIN marks."Schools" s ON s."EmisNumber" = v.emis AND s."IsDeleted" = false\n'
            'ON CONFLICT ("SchoolId","Year") DO UPDATE SET\n'
            '    "Learners" = EXCLUDED."Learners",\n'
            '    "Educators" = EXCLUDED."Educators",\n'
            '    "UpdatedAt" = now();\n'
        )
    out.append("COMMIT;\n")
    return "".join(out)


def main() -> None:
    report = locations.load_report(CSV_PATH)
    in_scope = {(row.get("emis_number") or "").strip() for row in report}
    masterlist = read_masterlist(PRIMARY_PATH)

    profiles = [
        (emis, build_profile(record))
        for emis, record in masterlist.items()
        if emis in in_scope
    ]
    if not profiles:
        print("No masterlist rows match the NSC report; no SQL emitted", file=sys.stderr)
        return

    years = sorted({p["data_year"] for _, p in profiles if p["data_year"] is not None})
    emails = sum(1 for _, p in profiles if p["email"])
    telephones = sum(1 for _, p in profiles if p["telephone"])
    enrolments = sum(
        1 for _, p in profiles if p["learners"] is not None or p["educators"] is not None
    )
    print(
        f"Matched {len(profiles)} of {len(in_scope)} report schools to the masterlist "
        f"({len(masterlist)} masterlist rows, data year {years or 'unknown'}); "
        f"{emails} emails, {telephones} telephones, {enrolments} enrolment rows",
        file=sys.stderr,
    )
    sys.stdout.write(build_sql(profiles))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(f"Import refused: {error}", file=sys.stderr)
        sys.exit(1)
