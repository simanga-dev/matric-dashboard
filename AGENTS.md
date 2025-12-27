# AGENTS.md

## Commands
- **Dev**: `pnpm dev` (runs Convex + Vite concurrently)
- **Build**: `pnpm build` (Vite build + TypeScript check)
- **Format**: `pnpm format` (Prettier)
- **Typecheck**: `tsc --noEmit`

## Architecture
- **Frontend**: React 19 + TanStack Router/Query + Vite
- **Backend**: Convex (serverless database + functions)
- **Styling**: Tailwind CSS v4 + shadcn/ui components (Radix)
- **Database schema**: `convex/schema.ts` (tables: `school`, `marks`, `numbers`)

## Project Structure
- `src/routes/` - TanStack Router file-based routes
- `src/components/` - React components (shadcn/ui in `ui/`)
- `src/lib/` - Utilities (cn, etc.)
- `convex/` - Convex functions & schema

## Code Style
- Use `~/` path alias for `src/` imports
- TypeScript strict mode enabled
- Convex: Always use new function syntax with `args` + `returns` validators
- Convex: Use `v.null()` for functions returning null
- Convex: Use `withIndex` instead of `filter` in queries
- Components follow shadcn/ui patterns with Radix primitives
