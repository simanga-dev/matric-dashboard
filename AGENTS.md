# Agent Guidelines for matric-dashboard

## Build/Lint/Test Commands

### Development

- `npm run dev` - Start development server with Convex and Vite
- `npm run dev:web` - Start Vite dev server only
- `npm run dev:convex` - Start Convex dev server only

### Build & Format

- `npm run build` - Build for production and type-check
- `npm run format` - Format code with Prettier

### Testing

- No test scripts defined - add test commands to package.json when implementing tests

## Code Style Guidelines

### TypeScript Configuration

- Strict mode enabled with comprehensive linting
- Path aliases: `~/*` maps to `./src/*`
- Target: ES2022, Module: ESNext
- Unused locals/parameters flagged as errors

### Formatting (Prettier)

- No semicolons
- Single quotes for strings
- Trailing commas everywhere
- Follow .prettierrc configuration

### Import Organization

```typescript
import * as React from 'react' // React imports first
import { Slot } from '@radix-ui/react-slot' // External libraries
import { cva } from 'class-variance-authority'

import { cn } from '~/lib/utils' // Internal imports
```

### Component Patterns

- Use class-variance-authority (cva) for variant props
- Combine classes with `cn()` utility (clsx + tailwind-merge)
- Export both component and variants
- Use `asChild` pattern with Radix Slot for flexible composition

### Naming Conventions

- Components: PascalCase (Button, DataTable)
- Functions: camelCase
- Files: kebab-case for components, camelCase for utilities
- Props: camelCase

### Error Handling

- Use TypeScript strict mode for compile-time safety
- Validate props and handle edge cases
- Follow Convex validation patterns for backend functions

## Cursor Rules Integration

Follow all guidelines in `.cursor/rules/convex_rules.mdc`:

- Use new Convex function syntax with explicit args/returns
- Always include validators for all Convex functions
- Follow schema design patterns with proper indexing
- Use TypeScript strict typing with Id<> types
- Implement proper error handling and validation

## File Structure

- `src/components/ui/` - Reusable UI components
- `src/components/` - App-specific components
- `src/routes/` - TanStack Router pages
- `src/lib/` - Utilities and shared logic
- `convex/` - Backend functions and schema
