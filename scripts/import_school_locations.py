#!/usr/bin/env python3
"""Emit SQL that imports real school coordinates into the geo.school_locations layer.

Coordinates come from the DBE EMIS school masterlists (xlsx) named by DBE_PRIMARY
and DBE_FALLBACK, matched to the NSC performance report by EMIS number. Two
masterlist quirks are normalized before use: some provinces store the latitude in
the longitude column, and some store coordinates with the decimal point dropped
(e.g. -2907075 for -29.07075). Emitted rows are authoritative, so is_sample is set
false and the GeoServer layer stops showing province-centroid jitter.

SQL is written to stdout - pipe it into psql:
    python3 scripts/import_school_locations.py | psql ...

Environment:
    CSV_PATH        default <repo root>/2024_nsc_school_performance_report.csv
    DBE_DIR         default <home>/Documents/dbe-emis-downloads
    DBE_PRIMARY     default <DBE_DIR>/National.xlsx
    DBE_FALLBACK    default <DBE_DIR>/2018 Masterlist Ordinary schools National Masterlist.xlsx
    UNRESOLVED_PATH optional NEW CSV file listing schools left without coordinates
"""

import csv
import os
import sys
import xml.etree.ElementTree as ElementTree
from zipfile import ZipFile

DEFAULT_CSV = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "..",
    "2024_nsc_school_performance_report.csv",
)
DEFAULT_DBE_DIR = os.path.join(os.path.expanduser("~"), "Documents", "dbe-emis-downloads")
DEFAULT_PRIMARY = "National.xlsx"
DEFAULT_FALLBACK = "2018 Masterlist Ordinary schools National Masterlist.xlsx"

CSV_PATH = os.environ.get("CSV_PATH", DEFAULT_CSV)
DBE_DIR = os.environ.get("DBE_DIR", DEFAULT_DBE_DIR)
PRIMARY_PATH = os.environ.get("DBE_PRIMARY", os.path.join(DBE_DIR, DEFAULT_PRIMARY))
FALLBACK_PATH = os.environ.get("DBE_FALLBACK", os.path.join(DBE_DIR, DEFAULT_FALLBACK))

# South African mainland envelope, used to detect swapped latitude/longitude.
MIN_LONGITUDE, MAX_LONGITUDE = 16.0, 33.0
MIN_LATITUDE, MAX_LATITUDE = -35.0, -22.0

_MAIN = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"
_REL = "http://schemas.openxmlformats.org/package/2006/relationships"
_DOC_REL = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
_ROW = "{%s}row" % _MAIN
_CELL = "{%s}c" % _MAIN
_VALUE = "{%s}v" % _MAIN
_INLINE = "{%s}is" % _MAIN


def to_float(value: object) -> float | None:
    """Parse a cell value as a float, treating the usual blank markers as missing."""
    text = str(value or "").strip()
    if text in ("", "-", "NA", "N/A", "."):
        return None
    try:
        return float(text)
    except ValueError:
        return None


def restore_decimal(value: str) -> float | None:
    """Rebuild a coordinate whose decimal point was dropped.

    Every South African coordinate has a two-digit degree part, so a bare integer
    like -2907075 is -29.07075 and 247247 is 24.7247.
    """
    text = str(value or "").strip()
    if not text or "." in text or "e" in text.lower():
        return None
    negative = text.startswith("-")
    digits = "".join(char for char in text if char.isdigit())
    if len(digits) <= 2:
        return None
    return float(("-" if negative else "") + digits[:2] + "." + digits[2:])


def _coordinate(value: object) -> float | None:
    parsed = to_float(value)
    if parsed is None:
        return None
    if abs(parsed) > 100:
        return restore_decimal(str(value))
    return parsed


def _in_envelope(longitude: float, latitude: float) -> bool:
    return (
        MIN_LONGITUDE <= longitude <= MAX_LONGITUDE
        and MIN_LATITUDE <= latitude <= MAX_LATITUDE
    )


def coerce_coordinates(longitude: object, latitude: object) -> tuple[float, float] | None:
    """Return (longitude, latitude) inside South Africa, or None when unusable.

    Corrects masterlists that swap the two columns and drops values outside the
    country envelope rather than emitting wrong points.
    """
    lon = _coordinate(longitude)
    lat = _coordinate(latitude)
    if lon is None or lat is None:
        return None
    if _in_envelope(lon, lat):
        return (lon, lat)
    if _in_envelope(lat, lon):
        return (lat, lon)
    return None


def _cell_text(cell: ElementTree.Element, shared: list[str]) -> str:
    if cell.get("t") == "inlineStr":
        inline = cell.find(_INLINE)
        return "".join(inline.itertext()) if inline is not None else ""
    value = cell.find(_VALUE)
    if value is None or value.text is None:
        return ""
    if cell.get("t") == "s":
        return shared[int(value.text)]
    return value.text


def _first_sheet_path(archive: ZipFile) -> str:
    workbook = ElementTree.fromstring(archive.read("xl/workbook.xml"))
    sheet = workbook.find("{%s}sheets/{%s}sheet" % (_MAIN, _MAIN))
    if sheet is None:
        return "xl/worksheets/sheet1.xml"
    rid = sheet.get("{%s}id" % _DOC_REL)
    rels = ElementTree.fromstring(archive.read("xl/_rels/workbook.xml.rels"))
    for relationship in rels.findall("{%s}Relationship" % _REL):
        if relationship.get("Id") == rid:
            target = relationship.get("Target", "").lstrip("/")
            return target if target.startswith("xl/") else "xl/" + target
    return "xl/worksheets/sheet1.xml"


def read_workbook(path: str) -> dict[str, tuple[float, float]]:
    """Read a DBE masterlist into {EMIS: (longitude, latitude)} for usable rows."""
    if not os.path.exists(path):
        raise ValueError(
            f"DBE masterlist not found at {path}. Set DBE_DIR or DBE_PRIMARY."
        )
    with ZipFile(path) as archive:
        shared: list[str] = []
        if "xl/sharedStrings.xml" in archive.namelist():
            root = ElementTree.fromstring(archive.read("xl/sharedStrings.xml"))
            shared = ["".join(item.itertext()) for item in root]
        headers: dict[str, str] = {}
        coordinates: dict[str, tuple[float, float]] = {}
        with archive.open(_first_sheet_path(archive)) as sheet:
            for _, row in ElementTree.iterparse(sheet, events=("end",)):
                if row.tag != _ROW:
                    continue
                cells = {
                    "".join(char for char in cell.get("r", "") if char.isalpha()):
                    _cell_text(cell, shared)
                    for cell in row.findall(_CELL)
                }
                if not headers:
                    headers = cells
                else:
                    record = {headers.get(key, key): value for key, value in cells.items()}
                    emis = str(record.get("NatEmis", "")).strip()
                    lon_key = "GIS_Long" if "GIS_Long" in record else "GIS_Longitude"
                    lat_key = "GIS_Lat" if "GIS_Lat" in record else "GIS_Latitude"
                    coord = coerce_coordinates(record.get(lon_key), record.get(lat_key))
                    if emis and coord is not None:
                        coordinates[emis] = coord
                row.clear()
        if not headers or "NatEmis" not in headers.values():
            raise ValueError(f"{path}: no NatEmis column found")
        return coordinates


def _is_placeholder_emis(emis: str) -> bool:
    return (
        not emis
        or emis.upper() in ("NA", "N/A", "-")
        or to_float(emis) == 0
    )


def load_report(path: str) -> list[dict]:
    """Read the NSC performance report, dropping rows without a real EMIS."""
    with open(path, newline="", encoding="utf-8-sig") as handle:
        reader = csv.DictReader(handle)
        fields = reader.fieldnames or []
        for required in ("emis_number", "centre_name", "province"):
            if required not in fields:
                raise ValueError(f"{path}: missing required column {required}")
        rows: list[dict] = []
        seen: set[str] = set()
        for row in reader:
            emis = (row.get("emis_number") or "").strip()
            if _is_placeholder_emis(emis) or emis in seen:
                continue
            seen.add(emis)
            rows.append(row)
    return rows


def q(value: object) -> str:
    """Quote a value as a SQL string literal."""
    return "'" + str(value).replace("'", "''") + "'"


def build_values(
    report: list[dict], primary: dict, fallback: dict
) -> tuple[list[str], list[dict]]:
    """Return (SQL value tuples, unresolved report rows)."""
    values: list[str] = []
    unresolved: list[dict] = []
    for row in report:
        emis = (row.get("emis_number") or "").strip()
        coord = primary.get(emis) or fallback.get(emis)
        if coord is None:
            unresolved.append(row)
            continue
        longitude, latitude = coord
        values.append(
            f"({q(emis)},{q((row.get('centre_name') or '').strip())},"
            f"{q((row.get('province') or '').strip())},"
            f"{q((row.get('district_name') or '').strip())},"
            f"{int(to_float(row.get('quintile')) or 0)},"
            f"{int(to_float(row.get('total_wrote_2024')) or 0)},"
            f"{int(to_float(row.get('total_achieved_2024')) or 0)},false,"
            f"ST_SetSRID(ST_MakePoint({longitude:.6f},{latitude:.6f}),4326))"
        )
    return values, unresolved


def write_unresolved(rows: list[dict]) -> None:
    path = os.environ.get("UNRESOLVED_PATH")
    if not path or not rows:
        return
    fields = list(rows[0].keys())
    with open(path, "x", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    print(f"Wrote {len(rows)} schools without coordinates to {path}", file=sys.stderr)


def main() -> None:
    report = load_report(CSV_PATH)
    primary = read_workbook(PRIMARY_PATH)
    fallback: dict[str, tuple[float, float]] = {}
    if os.path.exists(FALLBACK_PATH):
        fallback = read_workbook(FALLBACK_PATH)

    values, unresolved = build_values(report, primary, fallback)
    if not values:
        print("No schools resolved to coordinates; no SQL emitted", file=sys.stderr)
        return

    print(
        f"Resolved {len(values)} of {len(report)} schools "
        f"({len(primary)}/{len(fallback)} usable coordinates in the masterlists)",
        file=sys.stderr,
    )
    if unresolved:
        print(f"{len(unresolved)} schools left without coordinates", file=sys.stderr)
        for row in unresolved[:10]:
            print(
                f"  {row.get('emis_number', '').strip()!r} {row.get('centre_name', '')!r}",
                file=sys.stderr,
            )
    write_unresolved(unresolved)

    out = sys.stdout
    out.write("BEGIN;\n")
    out.write(
        "INSERT INTO geo.school_locations "
        "(emis_number,name,province,district,quintile,wrote,passed,is_sample,geom)\n"
        "VALUES\n" + ",\n".join(values) + "\n"
        "ON CONFLICT (emis_number) DO UPDATE SET "
        "name=EXCLUDED.name, province=EXCLUDED.province, district=EXCLUDED.district, "
        "quintile=EXCLUDED.quintile, wrote=EXCLUDED.wrote, passed=EXCLUDED.passed, "
        "is_sample=false, geom=EXCLUDED.geom;\n"
    )
    out.write("COMMIT;\n")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, OSError) as error:
        print(f"Import refused: {error}", file=sys.stderr)
        sys.exit(1)
