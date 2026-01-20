# Matric Dashboard

A web application for exploring South African National Senior Certificate (NSC) matric results data. Browse school performance, view top achievers, and access study resources.

## Features

- **School Performance Browser** - Search and view detailed performance data for schools across all provinces
- **Top Achievers** - Showcase of top-performing matric students by year
- **Analytics Dashboard** - Visual insights into pass rates, provincial comparisons, and trends
- **Past Papers** - Access to previous NSC examination papers
- **Study Guide** - Resources to help students prepare for exams

## Tech Stack

| Layer | Technology |
|-------|------------|
| Frontend | React 19, Vite, TypeScript |
| Styling | Tailwind CSS v4, shadcn/ui (Radix) |
| Routing | TanStack Router |
| State/Data | TanStack Query, Convex |
| Charts | Recharts |

## Getting Started

### Prerequisites

- Node.js v18+
- [pnpm](https://pnpm.io/)

### Installation

```bash
pnpm install
```

### Development

```bash
pnpm dev
```

Starts Vite on `http://localhost:3000` and Convex dev server concurrently.

### Build

```bash
pnpm build
```

## Project Structure

```
src/
├── routes/           # TanStack Router file-based routes
│   └── dashboard/    # Dashboard pages (schools, analytics, top-achievers, etc.)
├── components/       # React components (shadcn/ui in ui/)
└── lib/              # Utilities
convex/
├── schema.ts         # Database schema (school, marks, top_achievers)
└── *.ts              # Convex functions
```

## Scripts

| Command | Description |
|---------|-------------|
| `pnpm dev` | Start development servers |
| `pnpm build` | Production build + typecheck |
| `pnpm format` | Format code with Prettier |
