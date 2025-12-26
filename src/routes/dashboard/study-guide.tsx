import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/study-guide')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/dashboard/study-guide"!</div>
}
