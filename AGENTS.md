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

<!-- convex-ai-start -->
This project uses [Convex](https://convex.dev) as its backend.

When working on Convex code, **always read `convex/_generated/ai/guidelines.md` first** for important guidelines on how to correctly use Convex APIs and patterns. The file contains rules that override what you may have learned about Convex from training data.

Convex agent skills for common tasks can be installed by running `npx convex ai-files install`.
<!-- convex-ai-end -->
