# School name cleaning

The NSC performance report uses short school names such as `BHOVULENGWE S S S`,
`PARYS SKOOL SS`, and `TSITSANA COMP S S SCHOOL`. The DBE EMIS masterlist has an
`Official_Institution_Name` column, so the report and masterlist can be joined by
EMIS number.

`scripts/clean_school_names.py` does that join and writes
`data/nsc/school_names_clean.csv`. It does not change the source report.

## Source data

The raw report is not committed to the repository because it is a large external
dataset. Place the DBE NSC school performance report at the repository root as
`2024_nsc_school_performance_report.csv`, or point `CSV_PATH` at a local copy.
The same file feeds `import_schools_to_db.py`, `import_school_locations.py`,
and `upload_to_meilisearch.py`. Without it those scripts cannot run, and the two
tests that parse the real report skip.

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

## Apply names to the databases

`scripts/apply_clean_school_names.py` extends the cleaned export with names from
`National.xlsx` for schools that are not in the report, updates the SQLite
`schools` table in one transaction, and emits transactional Postgres SQL.

```bash
# SQLite and Postgres, using the running Aspire or Compose container
./scripts/apply_clean_school_names.sh
# or
just clean-school-names-apply
```

The shell wrapper auto-detects the Postgres container and database. Override with
`PG_CONTAINER`, `PG_USER`, `PG_DB`, and `PG_PASSWORD` when more than one stack is
running.

Before writing SQLite, the script copies it to `data/nsc/backups/`. It also writes
`data/nsc/school_name_updates.csv` with every changed SQLite name, the old value,
the new value, and the source. Postgres updates both
`marks."Schools"."CentreName"` and `geo.school_locations.name` by EMIS, and sets
`UpdatedAt` when a school name changes.

To apply only the cleaned names to Postgres without the wrapper:

```bash
python3 scripts/apply_clean_school_names.py \
    | docker exec -i -e PGPASSWORD=... <container> psql -U <user> -d <database>
```

## Search index

`scripts/upload_to_meilisearch.py` reads `data/nsc/school_names_clean.csv` by
default and replaces `centre_name` for matching EMIS values before uploading.
Use `--clean-csv` to point at a different export. Re-run it after changing the
name export, otherwise free-text search keeps ranking the old names.

```bash
MEILI_URL=http://127.0.0.1:32827 MEILI_MASTER_KEY=... \
    python3 scripts/upload_to_meilisearch.py
```

## Tests

```bash
python3 -B -m unittest discover -s scripts/tests -v
```
