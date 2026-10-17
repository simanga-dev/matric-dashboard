# NSC Overall Results: Back-filling 2010-2017 from Legacy Reports

**Date:** 2026-10-16
**Component:** `scripts/extract_nsc_results.py`, `data/nsc/matric.db` (staging)

## Summary

The per-school overall NSC results in the staging database previously covered
**2018-2025** only. This change extends the extractor to also parse the
**2012-2019 report layouts**, back-filling **2010-2017** while leaving every
existing 2018-2025 value intact.

`data/nsc/matric.db` now holds:

| Table | Before | After |
|---|---|---|
| `results` years | 2018-2025 | **2010-2025** |
| `results` rows | 54,419 | **106,301** |
| `schools` rows | 7,204 | **8,237** |

Per-year coverage is complete across the whole range (2010: 3,982 rows …
2025: 6,871 rows). The subject tables were not touched.

## Why 2010 and not 2008-2009

The gap could only be closed to **2010**. There is no per-school source for
2008 or 2009:

- No 2008 report is present anywhere under `data/`.
- The one 2009-era document on disk (`Final NSC Annexure 20 Jan 2010 for web.pdf`,
  under `data/reports/National Senior Certificate Examination Reports/`) contains
  **zero 9-digit EMIS tokens** — it is province/name-level, not per-school.

The legacy School Performance Reports begin with the 2012 publication (which
carries 2010-2012).

## What changed

### `scripts/extract_nsc_results.py`

Now handles two report generations that share the same wide per-province table
but differ in columns:

- **Modern (2020-2025 reports):** unchanged — each year prints
  Progressed / Total Wrote / Total Achieved / % Achieved.
- **Legacy (2012-2019 reports):** new header-driven parser. Older years print
  only Total Wrote / Achieved / %, gaining a Progressed column from 2015 on, and
  the metric order changes between report years. These per-report column orders
  are declared in `LEGACY_LAYOUTS`.

Key mechanics:

- **Header-driven, position-aware.** Year and metric anchors are detected by
  x-position from `pdftotext -bbox`; metric columns are matched to their nearest
  centre, so blank cells do not shift later values.
- **Individual-results tables only.** A page is accepted only if its header band
  contains a "Quintile" column. Special-needs / part-time appendices share the
  metric layout but omit Quintile, and their re-used EMIS numbers would otherwise
  overwrite mainstream results.
- **Regex-based page reading** (replacing the XML parse) so malformed legacy
  markup (entities, control characters) does not abort a report.
- **Newest non-null wins.** Legacy reports are extracted first, modern last, and
  the upsert uses `COALESCE(excluded, existing)` per metric field: a newer report
  overwrites a field only when it actually carries a value, and a later blank
  cell never erases an earlier value.

### `scripts/import_nsc_history.sh`

Header comment updated: history now extends **2010-2025** (was 2018-2025).

### `FILEMAP.md`

The `extract_nsc_results.py` impact row now names `LEGACY_LAYOUTS` (2012-2019)
alongside `ACHIEVED_REPORTS` (2020-2025).

## Safety check against the pre-change database

Compared against a backup of the previous `matric.db`:

- **0 rows removed.**
- **0 non-null values overwritten** (by metric field, for 2018-2025).
- 480 NULL→value fills and 427 added rows, all in 2018/2019 (267 + 160) — these
  are school-years the modern 2020 report window does not carry.
- `schools`: 1,033 added, 74 fields filled, **0 identities overwritten**.
- Subject tables byte-for-byte unchanged (`subject_results` 810,010 rows);
  `PRAGMA integrity_check` = ok.

Data-quality checks (achieved ≤ wrote, percent in 0-100, no negatives, valid
9-digit EMIS) all pass. The two source quirks that remain —
`progressed > wrote` (1,620 rows) and `wrote > 0` with a null achieved (38 rows) —
already exist in the modern data and are inherent to the reports, not extraction
artefacts.

Incidental fix: because `COALESCE` was introduced, 8 modern (2020-2024) rows that
had previously been blanked by a later report's empty cell are now populated.

## Reprocessing

```bash
python3 scripts/extract_nsc_results.py            # rebuilds schools + results only
```

To load the full 2010-2025 range into the serving database:

```bash
./scripts/import_nsc_history.sh
```

## Applied to the local database

The extended history was loaded into the local PostGIS container
(`marks."SchoolPerformances"`) via `./scripts/import_nsc_history.sh`:

- Rows: 53,319 → **98,896**; years: 8 (2018-2025) → **16 (2010-2025)**;
  schools with performances: **6,872**.
- 45,462 rows are now in the 2010-2017 range that was previously absent.
- No row was removed or nulled. 148 existing rows changed, all beneficial: 147
  had `ProgressedNumber` stuck at `0` (the previous import had no progressed
  data, so the importer coerced null → 0) and now carry the real count; 1 row
  regained a percent that had been stored as `0.0`.
- Post-import checks: `approved>wrote` = 0, percent > 100 = 0.

A pre-import export of the table is at `/tmp/opencode/schoolperformances_backup.csv`
(`COPY ... TO STDOUT WITH CSV HEADER`), if a rollback is ever needed.

## Follow-ups

- Load 2010-2025 into Postgres (`import_nsc_history.sh`) and check the overview
  panel if the app should surface the extended history.
- Consider a regression test around the legacy layouts (none exists today; the
  parser was validated by cross-report agreement on overlapping years).
