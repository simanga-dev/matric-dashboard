import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/seed-data')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/dashboard/seed-data"!</div>
}
