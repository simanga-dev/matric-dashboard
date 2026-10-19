# School name cleaning

The NSC performance report uses short school names such as `BHOVULENGWE S S S`,
`PARYS SKOOL SS`, and `TSITSANA COMP S S SCHOOL`. The DBE EMIS masterlist has an
`Official_Institution_Name` column, so the report and masterlist can be joined by
EMIS number.

`scripts/clean_school_names.py` does that join and writes
`data/nsc/school_names_clean.csv`. It does not change the source report.

## Run it

```bash
python3 scripts/clean_school_names.py
# or
just clean-school-names
```

The same environment variables as the coordinate importer apply:

| Variable | Default |
|---|---|
| `CSV_PATH` | `2024_nsc_school_performance_report.csv` |
| `DBE_DIR` | `~/Documents/dbe-emis-downloads` |
| `DBE_PRIMARY` | `<DBE_DIR>/National.xlsx` |
| `OUTPUT_PATH` | `data/nsc/school_names_clean.csv` |

The 2018 masterlist is not used for names. Its names still contain codes such as
`S/S` and some entries are stale, so falling back to it would undo the cleaning.

## Output columns

| Column | Meaning |
|---|---|
| `report_school_name` | Name from the NSC report, whitespace normalized |
| `official_school_name` | Name from `National.xlsx`, unchanged except whitespace |
| `clean_school_name` | Official name with school-type codes expanded |
| `phase` | `Phase_PED` from the masterlist |
| `name_source` | `DBE National masterlist` or `NSC report` |
| `match_status` | `matched`, `unmatched`, or `placeholder_emis` |
| `name_changed` | `yes` when `clean_school_name` differs from the report name |
| `review_note` | Why the row needs review, if it does |

`clean_school_name` expands tails such as `S S S`, `S/S`, `C/S`, `P/S`, `AS/S`,
and `IS/S`, plus words such as `COMP`, `TECH`, and `COMM`. It uses `Phase_PED` when
a tail is ambiguous, for example `C/S` becomes `COMBINED SCHOOL` or
`INTERMEDIATE SCHOOL` depending on the recorded phase.

If the official name has no school-type word at all, the phase suffix is added.
For example, `LEKGARIETSE` becomes `LEKGARIETSE SECONDARY SCHOOL`. The original
masterlist value stays in `official_school_name`.

Initials are left alone. `S.K. MAKHANYA HIGH SCHOOL` stays as it is. Official
names also keep their language, so Afrikaans names such as `HOËR TEGNIESE SKOOL`
are not translated.

## Current results

Against the bundled 2024 report and the 2025 `National.xlsx`:

- 6,921 report rows written
- 6,826 of 6,871 rows with a real EMIS matched to the masterlist (99.3%)
- 45 rows with a real EMIS have no masterlist match
- 50 rows have a placeholder EMIS and keep their report name
- 5,002 rows changed name

Rows that did not match, or that still contain a school-type code, carry a
`review_note`. Filter that column to work through them. Placeholder EMIS rows are
the same centres quarantined by `scripts/import_schools_to_db.py`.

## Tests

```bash
python3 -B -m unittest discover -s scripts/tests -v
```
