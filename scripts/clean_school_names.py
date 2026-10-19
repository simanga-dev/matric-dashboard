#!/usr/bin/env python3
"""Build a cleaned school-name list from the NSC report and DBE masterlist.

The NSC report uses abbreviated names such as "S S S", "C/S", and "COMP".
This script matches each report row to the DBE EMIS masterlist by EMIS number,
takes the masterlist's Official_Institution_Name, and expands any school-type
codes that remain. The source CSV is never modified.

Output is a CSV with one row per report row. Schools without a real EMIS or
without a masterlist match keep their cleaned report name and carry a
match_status/review_note flag.

Environment:
    CSV_PATH       default <repo root>/2024_nsc_school_performance_report.csv
    DBE_DIR        default <home>/Documents/dbe-emis-downloads
    DBE_PRIMARY    default <DBE_DIR>/National.xlsx
    OUTPUT_PATH    default <repo root>/data/nsc/school_names_clean.csv
"""

import csv
import os
import re
import sys
from xml.etree import ElementTree
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
DEFAULT_OUTPUT = os.path.normpath(
    os.path.join(SCRIPT_DIR, "..", "data", "nsc", "school_names_clean.csv")
)

CSV_PATH = os.environ.get("CSV_PATH", DEFAULT_CSV)
DBE_DIR = os.environ.get("DBE_DIR", DEFAULT_DBE_DIR)
PRIMARY_PATH = os.environ.get("DBE_PRIMARY", os.path.join(DBE_DIR, DEFAULT_PRIMARY))
OUTPUT_PATH = os.environ.get("OUTPUT_PATH", DEFAULT_OUTPUT)

FIELDS = (
    "emis_number",
    "centre_number",
    "province",
    "district_name",
    "report_school_name",
    "official_school_name",
    "clean_school_name",
    "phase",
    "name_source",
    "match_status",
    "name_changed",
    "review_note",
)

# Ambiguous tails whose meaning depends on the recorded school phase.
PHASE_TAILS = {
    "S/S": "SECONDARY SCHOOL",
    "C/S": "COMBINED SCHOOL",
    "P/S": "PRIMARY SCHOOL",
    "I/S": "INTERMEDIATE SCHOOL",
    "CS/S": "SECONDARY SCHOOL",
}

# Independent-school codes carry both a phase and sector marker.
INDEPENDENT_TAILS = {
    "CI/S": "COMBINED INDEPENDENT SCHOOL",
    "SI/S": "SECONDARY INDEPENDENT SCHOOL",
    "PI/S": "PRIMARY INDEPENDENT SCHOOL",
    "IP/S": "PRIMARY INDEPENDENT SCHOOL",
    "II/S": "INTERMEDIATE INDEPENDENT SCHOOL",
    "ISS": "INDEPENDENT SECONDARY SCHOOL",
    "IS/S": "INDEPENDENT SECONDARY SCHOOL",
}

# Other common South African school-name codes.
FIXED_TAILS = {
    "AS/S": "AGRICULTURAL SECONDARY SCHOOL",
    "A/S": "AGRICULTURAL SECONDARY SCHOOL",
    "ASS": "AGRICULTURAL SECONDARY SCHOOL",
    "CF/S": "COMBINED FARM SCHOOL",
    "T/S": "TECHNICAL SCHOOL",
    "H/S": "HIGH SCHOOL",
    "HS": "HIGH SCHOOL",
    "JS": "JUNIOR SECONDARY SCHOOL",
    "PS": "PRIMARY SCHOOL",
    "SS": "SECONDARY SCHOOL",
    "S.S.": "SECONDARY SCHOOL",
    "S.S": "SECONDARY SCHOOL",
    "SEC": "SECONDARY SCHOOL",
    "SPEC": "SPECIAL NEEDS EDUCATION SCHOOL",
    "CEN": "CENTRE",
}

# Ordered so longer codes win over their suffixes (CS/S before S/S).
TAIL_PATTERNS = (
    r"CS/S",
    r"AS/S",
    r"CF/S",
    r"CI/S",
    r"SI/S",
    r"PI/S",
    r"II/S",
    r"IP/S",
    r"IS/S",
    r"A/S",
    r"S/S",
    r"C/S",
    r"P/S",
    r"I/S",
    r"T/S",
    r"H/S",
    r"S\.S\.?",
    r"SS",
    r"JS",
    r"PS",
    r"HS",
    r"ASS",
    r"ISS",
    r"SEC\.?",
    r"PRIM\.?",
    r"COMB\.?",
    r"TECH\.?",
    r"SPEC\.?",
    r"CEN\.?",
    r"SEK\.?",
    r"HOËR",
    r"HOER",
    r"HIGH",
    r"SECONDARY",
    r"PRIMARY",
    r"COMBINED",
    r"FARM",
)

RESIDUAL_RE = re.compile(
    r"(?<![\w/])(?:"
    r"CS/S|AS/S|CF/S|CI/S|SI/S|PI/S|II/S|IP/S|IS/S|A/S|S/S|C/S|P/S|I/S|T/S|H/S|"
    r"ASS|ISS|SS|JS|PS|HS|SEC|PRIM|COMB|COMP|TECH|COMM|SPEC"
    r")\.?$",
    re.IGNORECASE,
)

WORD_REPLACEMENTS = (
    (re.compile(r"\bSEC\.?(?=\s|$|[).,])", re.IGNORECASE), "SECONDARY"),
    (re.compile(r"\bPRIM\.?(?=\s|$|[).,])", re.IGNORECASE), "PRIMARY"),
    (re.compile(r"\bCOMB\.?(?=\s|$|[).,])", re.IGNORECASE), "COMBINED"),
    (re.compile(r"\bCOMP(?=\s|$|[).,])", re.IGNORECASE), "COMPREHENSIVE"),
    (re.compile(r"\bTECH(?=\s|$|[).,])", re.IGNORECASE), "TECHNICAL"),
    (re.compile(r"\bCOMM(?=\s|$|[).,])", re.IGNORECASE), "COMMERCIAL"),
    (re.compile(r"\bJUN\.?(?=\s|$|[).,])", re.IGNORECASE), "JUNIOR"),
    (re.compile(r"\bSPEC(?=\s|$|[).,])", re.IGNORECASE), "SPECIAL NEEDS EDUCATION SCHOOL"),
    (re.compile(r"\bSCHO(?=\s|$|[).,])", re.IGNORECASE), "SCHOOL"),
)

# Official names that already contain a school-type noun do not need a phase suffix.
SCHOOL_NOUN_RE = re.compile(
    r"\b(?:SCHOOL|SCHOOLS|SKOOL|HOËRSKOOL|HOëRSKOOL|AKADEMIE|ACADEMY|COLLEGE|"
    r"GYMNASIUM|GIMNASIUM|LANDBOUSKOOL|MEISIESKOOL|SEUNSKOOL|PRIVAATSKOOL|"
    r"PARKSKOOL|BRANDSKOOL|SENTRUM|CENTRE|CENTER|INSTITUTE|INSTITUUT|INSTITUTION|"
    r"CONVENT|SEMINARY|YESHIVA|CAMPUS|HTS|H/SCHOOL|H/SKOOL|KIDDIELAND)\b",
    re.IGNORECASE,
)


def collapse(value: object) -> str:
    """Trim a value and reduce every whitespace run to one space."""
    return " ".join(str(value or "").replace("\u00a0", " ").split())


def phase_suffix(phase: object) -> str:
    """Return a full school-type suffix for a DBE Phase_PED value."""
    value = collapse(phase).upper()
    if value in (
        "SECONDARY SCHOOL",
        "COMBINED SCHOOL",
        "PRIMARY SCHOOL",
        "INTERMEDIATE SCHOOL",
        "SPECIAL NEEDS EDUCATION SCHOOL",
    ):
        return value
    return ""


def normalize_school_name(value: object, phase: object = None) -> str:
    """Expand school-type codes and collapse whitespace in a school name.

    Initials and personal names are left alone. Only a code at the end of the
    name is expanded, plus a short list of words such as COMP and TECH.
    """
    text = collapse(value)
    if not text:
        return ""

    # The DBE workbook stores a few Afrikaans characters as halfwidth katakana.
    text = text.replace("\uff8a", "Ê").replace("\uff8b", "Ë")
    text = re.sub(r"HOëR", "HOËR", text, flags=re.IGNORECASE)
    text = re.sub(r"\s*&\s*", " & ", text)
    for pattern, replacement in WORD_REPLACEMENTS:
        text = pattern.sub(replacement, text)

    # "S S SCHOOL" and "S S S" appear in the source report.
    text = re.sub(r"\bS\s+S\s+S\s+SCHOOL$", "SECONDARY SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\bS\s+S\s+SCHOOL$", "SECONDARY SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\bS\s+S\s+S$", "SECONDARY SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\bS\s+S$", "SECONDARY SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\bJ\s+S\s+SCHOOL$", "JUNIOR SECONDARY SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\bJ\s+S$", "JUNIOR SECONDARY SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\bSS\s+SCHOOL$", "SECONDARY SCHOOL", text, flags=re.IGNORECASE)

    for code in TAIL_PATTERNS:
        match = re.search(rf"(?<![\w/]){code}\s*$", text, flags=re.IGNORECASE)
        if not match:
            continue
        key = code.replace("\\", "").replace(".?", "")
        replacement = None
        if key in PHASE_TAILS:
            replacement = phase_suffix(phase) or PHASE_TAILS[key]
        elif key in INDEPENDENT_TAILS:
            suffix = phase_suffix(phase)
            if key == "II/S" and suffix:
                replacement = suffix + " INDEPENDENT SCHOOL"
            else:
                replacement = INDEPENDENT_TAILS[key]
        elif key in FIXED_TAILS:
            replacement = FIXED_TAILS[key]
        else:
            replacement = {
                "SEC": "SECONDARY SCHOOL",
                "PRIM": "PRIMARY SCHOOL",
                "COMB": "COMBINED SCHOOL",
                "TECH": "TECHNICAL SCHOOL",
                "CEN": "CENTRE",
                "SEK": "SECONDARY SCHOOL",
                "HOËR": "HIGH SCHOOL",
                "HOER": "HIGH SCHOOL",
                "HIGH": "HIGH SCHOOL",
                "SECONDARY": "SECONDARY SCHOOL",
                "PRIMARY": "PRIMARY SCHOOL",
                "COMBINED": "COMBINED SCHOOL",
                "FARM": "FARM SCHOOL",
            }.get(key.upper())
        if replacement:
            text = (text[:match.start()].rstrip(" .,-/&") + " " + replacement).strip()
            break

    # A trailing single S or H is a school-type code only when it is its own word.
    text = re.sub(r"(?<=\s)S\.?$", "SECONDARY SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"(?<=\s)H\.?$", "HIGH SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\s+HIGH\s+SECONDARY\s+SCHOOL$", " HIGH SCHOOL", text, flags=re.IGNORECASE)
    text = re.sub(r"\s+", " ", text).strip(" .")

    # Some official names are only a name with no school type. Add the phase
    # suffix so the cleaned list reads as a full school name.
    if phase and text and not SCHOOL_NOUN_RE.search(text):
        suffix = phase_suffix(phase)
        if text.upper().endswith(("TECHNICAL", "INTERMEDIATE")):
            suffix = "SCHOOL"
        elif text.upper().endswith("SPECIAL"):
            suffix = "NEEDS EDUCATION SCHOOL"
        if suffix:
            insertion = re.search(r"\s*\([^)]*\)$", text)
            if insertion:
                text = text[: insertion.start()] + " " + suffix + text[insertion.start():]
            else:
                text += " " + suffix

    return text


def residual_codes(value: str) -> list[str]:
    """Return school-type codes that are still abbreviated after cleaning."""
    return sorted({match.group(0).upper() for match in RESIDUAL_RE.finditer(value)})


def read_masterlist_names(path: str) -> dict[str, dict[str, str]]:
    """Read NatEmis, Official_Institution_Name, and Phase_PED from a masterlist."""
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
                name = collapse(record.get("Official_Institution_Name"))
                if emis and name:
                    records[emis] = {
                        "name": name,
                        "phase": collapse(record.get("Phase_PED")),
                    }
                row.clear()
        if "NatEmis" not in headers.values() or "Official_Institution_Name" not in headers.values():
            raise ValueError(f"{path}: no NatEmis/Official_Institution_Name columns found")
    return records


def load_report(path: str) -> list[dict]:
    """Read every report row without changing the source file."""
    with open(path, newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fields = reader.fieldnames or []
        for required in ("emis_number", "centre_number", "centre_name", "province"):
            if required not in fields:
                raise ValueError(f"{path}: missing required column {required}")
        return list(reader)


def build_rows(
    report: list[dict], masterlist: dict[str, dict[str, str]]
) -> list[dict[str, str]]:
    """Combine report rows with cleaned masterlist names."""
    rows: list[dict[str, str]] = []
    for source in report:
        emis = collapse(source.get("emis_number"))
        report_name = collapse(source.get("centre_name"))
        placeholder = locations._is_placeholder_emis(emis)
        record = None if placeholder else masterlist.get(emis)

        if record:
            official = record["name"]
            phase = record.get("phase", "")
            clean = normalize_school_name(official, phase)
            status = "matched"
            name_source = "DBE National masterlist"
        else:
            official = ""
            phase = ""
            clean = normalize_school_name(report_name)
            status = "placeholder_emis" if placeholder else "unmatched"
            name_source = "NSC report"

        notes: list[str] = []
        if status == "placeholder_emis":
            notes.append("Placeholder EMIS; no masterlist match")
        elif status == "unmatched":
            notes.append("No match in DBE National masterlist")
        leftovers = residual_codes(clean)
        if leftovers:
            notes.append("Check residual abbreviation: " + ", ".join(leftovers))

        rows.append(
            {
                "emis_number": emis,
                "centre_number": collapse(source.get("centre_number")),
                "province": collapse(source.get("province")),
                "district_name": collapse(source.get("district_name")),
                "report_school_name": report_name,
                "official_school_name": official,
                "clean_school_name": clean,
                "phase": phase,
                "name_source": name_source,
                "match_status": status,
                "name_changed": "yes" if clean != report_name else "no",
                "review_note": "; ".join(notes),
            }
        )
    return rows


def write_csv(path: str, rows: list[dict[str, str]]) -> None:
    """Write the cleaned rows to a new CSV, creating parent directories."""
    parent = os.path.dirname(os.path.abspath(path))
    os.makedirs(parent, exist_ok=True)
    with open(path, "w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=FIELDS)
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    report = load_report(CSV_PATH)
    masterlist = read_masterlist_names(PRIMARY_PATH)
    rows = build_rows(report, masterlist)
    if not rows:
        print("No report rows; no CSV written", file=sys.stderr)
        return

    write_csv(OUTPUT_PATH, rows)

    valid = sum(1 for row in rows if row["match_status"] != "placeholder_emis")
    matched = sum(1 for row in rows if row["match_status"] == "matched")
    unmatched = sum(1 for row in rows if row["match_status"] == "unmatched")
    placeholders = len(rows) - valid
    residual = sum(1 for row in rows if "residual abbreviation" in row["review_note"])

    print(f"Wrote {len(rows)} rows to {OUTPUT_PATH}", file=sys.stderr)
    if valid:
        share = matched / valid * 100
        print(
            f"Matched {matched} of {valid} rows with a real EMIS ({share:.1f}%)",
            file=sys.stderr,
        )
    print(f"{unmatched} rows with a real EMIS have no masterlist match", file=sys.stderr)
    print(f"{placeholders} rows have a placeholder EMIS and keep the report name", file=sys.stderr)
    if residual:
        print(f"{residual} cleaned names still contain a school-type code", file=sys.stderr)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(f"School name export refused: {error}", file=sys.stderr)
        sys.exit(1)
