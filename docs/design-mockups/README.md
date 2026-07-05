# Design Mockups: Schools & Top Achievers Pages

## Overview

This document covers design ideas, component breakdowns, interaction patterns, and suggested API endpoints for two new dashboard pages: **Schools** (`/schools`) and **Top Achievers** (`/top-achievers`). These mockups are throwaway HTML prototypes for design discussion -- not production Svelte code.

Both pages follow the existing dashboard's design system:
- Warm stone-neutral base with slate-blue accents (HSL variables from `themes.css`)
- Tailwind CSS v4 with dark/light mode via `.dark` class
- Card-based layout with `bg-gradient-to-t from-primary/5 to-card`
- shadcn-svelte component conventions (Card, Badge, Table, Input, Button)

---

## 1. Schools Page (`/schools`)

### Layout Structure

```
+-------------------------------------------------------------+
| [Breadcrumb]  Dashboard > Schools                            |
|                                                              |
| Schools Overview                                     [Export] |
|                                                              |
| +----------+ +----------+ +----------+ +----------+          |
| | TOTAL    | | LEARNERS | | PASS     | | BACHELOR |          |
| | SCHOOLS  | | 2024     | | RATE     | | PASS     |          |
| | 6,894    | | 623,109  | | 78.3%    | | 45.2%    |          |
| +----------+ +----------+ +----------+ +----------+          |
|                                                              |
| [Search schools...] [Province: All] [Year: 2024] [View: Table]  |
|                                                              |
| +----------------------------------------------------------+ |
| | Rank | School Name    | Province  | Wrote | %     | Bach | |
| | 1    | Pretoria HS    | Gauteng   | 1,200 | 98.5% | 612  | |
| | 2    | Wynberg Girls  | WC        | 980   | 97.8% | 498  | |
| | ...                                                    | |
| | ← Page 1 of 50 →                                       | |
| +----------------------------------------------------------+ |
+-------------------------------------------------------------+
```

### Component Breakdown

| Component | Description | Reuse / New |
|-----------|-------------|-------------|
| **PageHeader** | Breadcrumb + page title + action buttons (export) | Reuse from ContentHeader pattern |
| **StatCardRow** | 4 stat cards: Total Schools, Total Learners 2024, Pass Rate 2024, Bachelor Pass Rate | Reuse `StatCard.svelte` |
| **SearchFilterBar** | Search input + province dropdown + year selector + quintile filter + pass rate range | New component |
| **ViewToggle** | Toggle between Table / Cards / Map / Tree views | New (uses existing view components) |
| **SchoolsTable** | Paginated table with sorting by columns | Can reuse/adapt existing `SchoolsTable.svelte` |
| **SchoolDetailPanel** | Slide-over panel when clicking a school row | New component (could use shadcn Drawer or Sheet) |
| **ProvinceFilterChips** | Clickable province badges for quick filtering | New (could adapt from SchoolCards) |

### School Detail Panel (Slide-over)

When clicking a school row, a slide-over panel opens from the right showing:

```
+--------------------------------------------+
| [X] School Detail                          |
|                                            |
| Pretoria High School for Girls             |
| Gauteng - Tshwane South District           |
|                                            |
| +----------+ +----------+ +----------+     |
| | WROTE    | | PASSED   | | BACHELOR |     |
| | 1,200    | | 1,182    | | 612      |     |
| +----------+ +----------+ +----------+     |
|                                            |
| Pass Rate Trend (2020-2024)                |
| [Line chart: 92% -> 94% -> 96% -> 97% -> 98.5%] |
|                                            |
| Related Schools (Tshwane South District)    |
| - Pretoria High School - 98.5%             |
| - Waterkloof High School - 96.2%           |
| +------------------------------------------+
```

### Interaction Patterns

1. **Search + Filter**: Debounced search input (300ms) filters results. Province, year, quintile, and pass rate range dropdowns act as additional filters. All filter state is URL-encoded for shareable links.
2. **Column Sorting**: Click any column header to sort ascending/descending. Sort indicator shown on active column.
3. **Row Click**: Opens the school detail slide-over panel. URL updates to `/schools/{id}` for deep linking.
4. **View Toggle**: Switch between table (default), card grid, map, and tree views. Active view preserved in URL.
5. **Export**: Download filtered results as CSV or Excel.
6. **Pagination**: Page-based with page size selector (10/25/50/100).

### Suggested API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/dashboard/schools?page=1&pageSize=10&search=&province=&year=2024&quintile=&minPassRate=&maxPassRate=&sortBy=passRate&sortDesc=true` | Paginated school list with search, filters, and sorting |
| `GET` | `/api/v1/dashboard/schools/{id}` | Single school detail with full stats |
| `GET` | `/api/v1/dashboard/schools/{id}/trends?years=5` | Historical pass rate for a specific school |
| `GET` | `/api/v1/dashboard/schools/{id}/related?district=true` | Related schools in same circuit/district |
| `GET` | `/api/v1/dashboard/filters` | Available filter options (provinces, years, quintiles) |
| `GET` | `/api/v1/dashboard/schools/export?format=csv` | Export filtered schools as CSV/Excel |

---

## 2. Top Achievers Page (`/top-achievers`)

### Layout Structure

```
+-------------------------------------------------------------+
| [Breadcrumb]  Dashboard > Top Achievers                      |
|                                                              |
| Top Achievers 2024                                           |
|                                                              |
| +----------+ +----------+ +----------+ +----------+          |
| | TOP PASS | | #1 SCHOOL | | AVG TOP  | | TOP 100  |        |
| | RATE     | |           | | 10 PASS  | | TREND    |        |
| | 98.5%    | | Pretoria | | 96.2%    | | +2.3%    |        |
| +----------+ +----------+ +----------+ +----------+          |
|                                                              |
| +-----------+ +-----------+ +-----------+                    |
| | GOLD  🥇  | | SILVER 🥈 | | BRONZE 🥉 |                    |
| | 98.5%     | | 97.8%     | | 97.2%     |                    |
| | Pretoria  | | Wynberg   | | Rustenburg|                    |
| | Girls HS  | | Girls HS  | | Girls HS  |                    |
| | +0.5%     | | +1.2%     | | -0.3%     |                    |
| +-----------+ +-----------+ +-----------+                    |
|                                                              |
| [Year: 2024] [Province: All] [District: All] [Search...]    |
|                                                              |
| +----------------------------------------------------------+ |
| | Rank | School        | Province | Wrote | Pass Rate | Tr  | |
| | 1    | Pretoria GHS  | Gauteng  | 1,200 | 98.5%     | ↑  | |
| | 2    | Wynberg GHS   | WC       | 980   | 97.8%     | ↑  | |
| | 3    | Rustenburg    | WC       | 1,100 | 97.2%     | ↓  | |
| | ...                                                    | |
| +----------------------------------------------------------+ |
|                                                              |
| Province Leaders                                             |
| +----------+ +----------+ +----------+ +----------+          |
| | Gauteng  | | WC       | | KZN      | | EC       |        |
| | 98.5%    | | 97.8%    | | 96.1%    | | 95.4%    |        |
| +----------+ +----------+ +----------+ +----------+          |
+-------------------------------------------------------------+
```

### Component Breakdown

| Component | Description | Reuse / New |
|-----------|-------------|-------------|
| **PageHeader** | Page title + subtitle + year selector | Reuse pattern |
| **StatCardRow** | 4 stat cards: Top Pass Rate, #1 School, Avg Top 10, Top 100 Trend | Reuse `StatCard.svelte` |
| **PodiumCards** | Gold/Silver/Bronze spotlight cards for Top 3 schools | New component (medal/ribbon styling) |
| **LeaderboardTable** | Ranked table with position, school, province, district, wrote, pass rate, trend, bachelor | New component |
| **ProvinceLeaders** | Row of cards showing best school per province | New component |
| **FilterBar** | Year, province, district dropdowns + school name search | New component |
| **HistoryChart** | Year-over-year comparison for a selected school | New component |
| **TrendIndicator** | Arrow up/down/neutral badge showing year-over-year change | New utility component |

### Podium Cards (Top 3 Spotlight)

The top 3 schools get special spotlight cards with:
- **Gold (#1)**: Large card with trophy icon, golden gradient background (`from-yellow-400/20 to-yellow-600/10`), prominent rank badge
- **Silver (#2)**: Medium card with silver/gray gradient, medal badge
- **Bronze (#3)**: Smaller card with bronze/amber gradient, medal badge
- Each card shows: school name, province, pass rate (large), trend arrow, wrote/passed counts, bachelor count

### Interaction Patterns

1. **Year Filter**: Switch between years to compare historical top achievers.
2. **Province/District Filter**: Filter the leaderboard while keeping top 3 always visible (or showing top 3 within the filtered set).
3. **School Search**: Type-ahead search that scrolls to the school's position in the ranking.
4. **Row Click**: Opens school detail slide-over (same panel as schools page) showing full stats.
5. **Column Sorting**: Click headers to re-sort (off by default - default sort is by rank).
6. **History Tooltip**: Hover over trend arrow to see the school's past 3 years of pass rates.
7. **Responsive**: On mobile, podium cards stack vertically; leaderboard shows fewer columns.

### Suggested API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/dashboard/top-achievers?year=2024&province=&district=&page=1&pageSize=20&search=` | Paginated ranked leaderboard |
| `GET` | `/api/v1/dashboard/top-achievers/podium?year=2024` | Top 3 schools only (for spotlight cards) |
| `GET` | `/api/v1/dashboard/top-achievers/province-leaders?year=2024` | Best school per province |
| `GET` | `/api/v1/dashboard/top-achievers/stats?year=2024` | Summary stats for the page header |
| `GET` | `/api/v1/dashboard/top-achievers/history?schoolId={id}&years=5` | Historical rank and pass rate for a school |
| `GET` | `/api/v1/dashboard/top-achievers/search?q=pretoria&year=2024` | Search for a school's rank position |
| `GET` | `/api/v1/dashboard/top-achievers/export?year=2024&format=csv` | Export leaderboard as CSV |

---

## 3. Data Visualizations Suggested

| Visualization | Page | Description |
|---------------|------|-------------|
| **Pass Rate Trend Line Chart** | Schools (detail) | 5-year pass rate history for a specific school |
| **Province Distribution Bar Chart** | Schools | Number of schools per province with pass rate overlay |
| **Pass Rate Distribution Histogram** | Schools | Distribution of schools across pass rate buckets (0-100%) |
| **Top 10 Year-over-Year Comparison** | Top Achievers | Bar chart comparing current top 10 with previous year |
| **Province Leader Overview** | Top Achievers | Map or bar chart showing best-performing school per province |
| **Trend Sparklines** | Both | Mini line charts embedded in table rows showing 3-5 year trend |

---

## 4. Color Palette (from existing theme)

| Token | Light | Dark | Usage |
|-------|-------|------|-------|
| `--background` | 40 20% 97% | 24 8% 10% | Page background |
| `--card` | 40 15% 98.5% | 24 7% 12.5% | Card backgrounds |
| `--primary` | 220 25% 20% | 30 6% 82% | Headings, primary text |
| `--muted` | 35 12% 93% | 24 5% 17% | Badges, secondary elements |
| `--border` | 30 12% 88% | 24 5% 18% | Borders, dividers |
| `--success` | 150 40% 38% | 150 35% 28% | Green pass rate indicators |
| `--destructive` | 0 55% 55% | 0 40% 35% | Red pass rate indicators |
| `--warning` | 42 75% 50% | 42 55% 30% | Amber/medium indicators |

---

## 5. Reuse Strategy

| Existing Component | Reuse in Schools Page | Reuse in Top Achievers Page |
|--------------------|----------------------|---------------------------|
| `StatCard` | Yes - 4 summary stats | Yes - 4 summary stats |
| `SchoolsTable` | Yes - adapt with search/filter/sort | No - new leaderboard table |
| `SchoolCards` | Yes - as an alternative view | No |
| `SchoolTree` | Yes - as an alternative view | No |
| `SchoolMap` | Yes - as an alternative view | No - but province map could show leaders |
| `Card` (shadcn) | Yes - throughout | Yes - throughout |
| `Badge` (shadcn) | Yes - pass rate, province tags | Yes - rank badges, trend indicators |
| `Button` (shadcn) | Yes - actions, filters | Yes - actions, filters |
| `Input` (shadcn) | Yes - search | Yes - search |
| `Table` (shadcn) | Yes - school list | Yes - leaderboard |
| `Skeleton` | Yes - loading states | Yes - loading states |
| `Sheet`/`Drawer` | Yes - school detail panel | Yes - school detail panel |

---

## 6. Responsive Behavior

| Breakpoint | Schools Page | Top Achievers Page |
|------------|-------------|-------------------|
| **Mobile (< 640px)** | Single column stat cards, full-width table with horizontal scroll, filters stacked | Stacked podium, scrollable table, province cards in 2-col grid |
| **Tablet (640-1024px)** | 2-col stat cards, table with fewer columns visible, inline filters | 3-col podium (compact), table with key columns, 4-col province grid |
| **Desktop (1024px+)** | 4-col stat cards, full table with all columns, filter bar inline | Full podium layout, complete table, province grid horizontal |

---

## 7. Next Steps / Open Questions

1. Should the school detail panel be a shared component used by both pages (and possibly the existing dashboard)?
2. For the Top Achievers page, should historical rankings be shown as a multi-year table or a chart?
3. Should quintile data be displayed (SA schools are ranked Quintile 1-5 based on poverty)?
4. How should we handle the "Related Schools" - by district, circuit, or geographic proximity?
5. Should we add a "Compare Schools" feature (select 2+ schools to compare side by side)?
6. What is the expected data volume? Do we need infinite scroll or traditional pagination?
7. Should Top Achievers include individual student achievements (top learners), or only school-level performance?
