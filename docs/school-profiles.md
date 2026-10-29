# School profiles and enrolment

`scripts/import_school_profiles.py` enriches the NSC school roster with the
contact and classification columns from the DBE EMIS masterlist
(`National.xlsx`). It updates `marks."Schools"` and upserts one
`marks."SchoolEnrolments"` row per school for the masterlist's `DataYear`.

Run it after the API database migrations; it only touches schools that already
exist in `marks."Schools"` (matched by EMIS number), so it never invents a school.

```bash
just school-profiles
# or
DBE_DIR=/path/to/dbe-emis-downloads python3 scripts/import_school_profiles.py | psql ...
```

## Schema

Static per-school attributes live on `marks."Schools"`:

| Column | Type | Source column |
|---|---|---|
| `Email` | `varchar(320)` | `Email` |
| `Telephone` | `varchar(32)` | `Telephone`, normalized to E.164 |
| `LandOwnership` | `varchar(20)` | `OwnerLand` |
| `LandOwnershipRaw` | `varchar(100)` | `OwnerLand`, unchanged |
| `BuildingOwnership` | `varchar(20)` | `OwnerBuild` |
| `BuildingOwnershipRaw` | `varchar(100)` | `OwnerBuild`, unchanged |
| `IsSection21` | `boolean` | `Section21` |
| `IsNoFeeSchool` | `boolean` | `NoFeeSchool` |
| `UrbanRural` | `varchar(20)` | `Urban_Rural` |
| `Sector` | `varchar(20)` | `Sector` |
| `Phase` | `varchar(20)` | `Phase_PED` |
| `Quintile` | `integer`, nullable | `Quintile`, only when it publishes Q1–Q5 |
| `QuintileCode` | `varchar(10)` | `Quintile`, unchanged |
| `MasterlistDataYear` | `integer` | `DataYear` |

Learner and educator counts change every year, so they are rows rather than
columns. One row per school per year, unique on `(SchoolId, Year)`:

| Column | Type |
|---|---|
| `Id` | `uuid` |
| `SchoolId` | `uuid`, FK to `marks."Schools"`, cascade delete |
| `Year` | `integer`, the masterlist `DataYear` |
| `Learners` | `integer`, nullable |
| `Educators` | `integer`, nullable |

Adding a new masterlist year only needs an insert, not a migration.

`Quintile` is not overwritten when the masterlist has no numeric value: the
import uses `COALESCE`, so a school keeps the quintile published in the NSC
report. `QuintileCode` always records the masterlist's own value. In the 2025
masterlist, 1,538 secondary and combined schools publish `N/A` or `99`.

## API and UI

`GET /api/v1/dashboard/schools/{id}` returns the profile as flat nullable fields
(`quintileCode`, `email`, `telephone`, `sector`, `phase`, `landOwnership`,
`buildingOwnership`, `isSection21`, `isNoFeeSchool`, `urbanRural`) plus a nested
`enrolment` object (`year`, `learners`, `educators`). `quintile` is nullable.

The school detail page (`src/frontend/src/routes/(app)/schools/[id]/`) renders a
Contact section (email and telephone as `mailto:`/`tel:` links, learner and
educator counts with the derived ratio) and a School profile section (sector,
phase, urban/rural, land and building ownership, Section 21, no-fee). Missing
values render as `n/a`; the page has its own hardcoded copy like the rest of the
detail view, so no i18n keys were added.

## Normalization

The masterlist is dirty, so every value is cleaned before it reaches the
database. `LandOwnershipRaw` / `BuildingOwnershipRaw` keep the original string
so the mapping stays auditable.

| Field | Rule |
|---|---|
| `Email` | Lowercase and trim; dropped when blank, `99`, or missing `@` |
| `Telephone` | Digits only; 9 digits gain `+27`, a leading `0` is replaced by `+27`, existing `27` / `0027` prefixes normalized; `99`, `0`, and other lengths dropped |
| Ownership | `GOVERNMENT`, `Govt`, `Govt Education`, `PUBLIC`, `STATE`, `MUNICIPALITY`, `DOE …` → `Government`; `INDEPENDENT` → `Independent`; `UNKNOWN` / `Other` → `Unknown`; blank and `99` → null; every other value is a named private owner (`MJB PROPERTIES`, `MS. NTULI`, `AFM CHURCH`, `Trust Land`, `Farm`, …) → `Private` |
| `Section21` | `YES` / `Y` / `1` → true, `NO` / `N` / `0` → false, else null |
| `NoFeeSchool` | `No Fee` / `YES` → true, `Fee Charging` / `NO` → false, else null |
| `Urban_Rural` | `URBAN` / `RURAL` → enum, `99` and `TO BE UPDATED` → null, else `Unknown` |
| `Sector` | `PUBLIC` / `INDEPENDENT`, else `Unknown`; blank and `99` → null |
| `Phase_PED` | Maps the phase to `Primary`, `Secondary`, `Combined`, `Intermediate`, `SpecialNeeds` (school or centre), or `Skills`; anything else → `Other` |
| `Quintile` | `Q1`–`Q5` → 1–5; `N/A`, `99`, and blank → null. The raw value is stored in `QuintileCode`, so "not applicable" stays distinguishable from "not captured" |
| Counts | Blank and `99` → null; negative values dropped |

`99` is the masterlist's catch-all placeholder and appears in every optional
column. Treating it as null is deliberate — storing 99 learners would be wrong.

## Coverage in the 2025 masterlist

Measured against `National.xlsx` (25,527 rows):

- `Email`: 5,714 usable addresses (22%)
- `Telephone`: 21,418 usable numbers, 1,732 blank, 1,985 `99` sentinels
- Enrolment: 25,311 rows, 25,247 with learners and 25,298 with educators
- `OwnerLand` normalizes to 15,195 Government, 2,724 Private, 1,284 Independent, 2,328 Unknown, 3,996 not provided
- `OwnerBuild` normalizes to 11,906 Government, 1,155 Private, 1,359 Independent, 7,106 Unknown, 4,001 not provided
- `OwnerLand` alone has 46 distinct spellings, `OwnerBuild` 84

Email coverage is low, so the frontend must treat it as optional.

## Tests

`scripts/tests/test_school_profiles.py` covers each normalization rule and the
emitted SQL shape. The importer is idempotent: re-running updates existing rows
and the `(SchoolId, Year)` conflict target keeps enrolment unique.
