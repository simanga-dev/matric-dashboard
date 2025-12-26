import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/analytic')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/dashboard/analytic"!</div>
}
