# GEMINI.md

## Project Overview

This is a web application built with a modern frontend stack. The project is a dashboard application that uses the following technologies:

*   **Framework:** [React](https://react.dev/)
*   **Bundler:** [Vite](https://vitejs.dev/)
*   **Styling:** [Tailwind CSS](https://tailwindcss.com/)
*   **UI Components:** [Radix UI](https://www.radix-ui.com/) and [Lucide React](https://lucide.dev/guide/packages/lucide-react) for UI components, and [Recharts](https://recharts.org/) for charts.
*   **Routing:** [TanStack Router](https://tanstack.com/router/)
*   **Data Fetching and State Management:** [TanStack Query](https://tanstack.com/query/) and [Convex](https://www.convex.dev/)
*   **Type Checking:** [TypeScript](https://www.typescriptlang.org/)
*   **Schema Validation:** [Zod](https://zod.dev/)

The application uses Convex for its backend and data layer, with the schema defined in `convex/schema.ts`. The frontend is built with React and Vite, with routing handled by TanStack Router. The UI is styled with Tailwind CSS and built with Radix UI and other component libraries.

## Building and Running

### Prerequisites

*   [Node.js](https://nodejs.org/) (v18 or higher)
*   [pnpm](https://pnpm.io/)

### Installation

```bash
pnpm install
```

### Development

To start the development server, run the following command:

```bash
pnpm dev
```

This will start the Vite development server on `http://localhost:3000` and the Convex development server.

### Build

To build the project for production, run the following command:

```bash
pnpm build
```

This will create a `dist` directory with the production-ready assets.

## Development Conventions

### Formatting

This project uses [Prettier](https://prettier.io/) for code formatting. To format the code, run the following command:

```bash
pnpm format
```
