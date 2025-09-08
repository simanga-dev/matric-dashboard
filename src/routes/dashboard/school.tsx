import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/school')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/dashboard/school"!</div>
}
