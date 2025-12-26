import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/past-papers')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/dashboard/past-papers"!</div>
}
