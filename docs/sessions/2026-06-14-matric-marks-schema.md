# Matric Marks Schema

**Date:** 2026-06-14
**Branch:** `feat/matric-marks-schema`
**Ticket:** MATRD-2

## Summary

Created the database schema for matric (NSC) school performance data. This replaces the mock data in `DashboardService` with real domain entities backed by the `marks` schema in PostgreSQL.

## Changes

### Domain Layer (`MatricDasbhoard.Domain.Entities`)

- **`School.cs`** — Domain entity for schools/matric examination centres. Fields: Province, DistrictName, EmisNumber (unique), CentreNumber, CentreName, Quintile. Extends `BaseEntity`. Has a `Performances` navigation collection.

- **`SchoolPerformance.cs`** — Yearly NSC performance data for a school. Fields: SchoolId (FK), Year, ProgressedNumber, TotalWrote, TotalAchieved, PercentAchieved. Unique constraint on (SchoolId, Year). Extends `BaseEntity`.

### Infrastructure Layer

- **`Features/Marks/Configurations/SchoolConfiguration.cs`** — EF Core config: `marks.Schools` table with unique index on `EmisNumber`, indexes on `Province` and `DistrictName`, cascade delete to performances.

- **`Features/Marks/Configurations/SchoolPerformanceConfiguration.cs`** — EF Core config: `marks.SchoolPerformances` table with unique composite index on `(SchoolId, Year)`, index on `Year`, decimal precision 5,1 for `PercentAchieved`.

- **`Persistence/MatricDasbhoardDbContext.cs`** — Added `DbSet<School>` and `DbSet<SchoolPerformance>`.

- **`Persistence/Migrations/20260614103518_AddMatricMarksSchema.cs`** — Migration creating `marks` schema, `Schools` and `SchoolPerformances` tables, indexes, and FK.

### Other

- **`.config/dotnet-tools.json`** — Updated `dotnet-ef` from 10.0.5 to 10.0.9.

## Data Model

```
Schools (marks schema)
├── Id (Guid, PK)
├── Province (varchar 100)
├── DistrictName (varchar 200)
├── EmisNumber (varchar 20, unique)
├── CentreNumber (varchar 20)
├── CentreName (varchar 500)
├── Quintile (int)
└── [BaseEntity audit fields]

SchoolPerformances (marks schema)
├── Id (Guid, PK)
├── SchoolId (Guid, FK → Schools)
├── Year (int)
├── ProgressedNumber (int)
├── TotalWrote (int)
├── TotalAchieved (int)
├── PercentAchieved (decimal 5,1)
├── UNIQUE(SchoolId, Year)
└── [BaseEntity audit fields]
```

## Verification

- `dotnet build` — 0 errors
- `dotnet test` — Unit: 97/97 passed. Architecture: 10/10 passed. Component: 541/541 passed. (API tests have pre-existing failures unrelated to these changes.)

## CSV Data

The source data file `2024_nsc_school_performance_report.csv` (6,922 rows) is at the project root. It contains school-level NSC performance data for 2022–2024 from the Department of Basic Education. A data import mechanism will be implemented separately.

## Notes

- Schema uses `marks` namespace to keep domain data separate from auth/infra tables.
- The `DashboardService` currently returns mock data — a follow-up PR should replace it with real EF queries against these entities and import the CSV.
