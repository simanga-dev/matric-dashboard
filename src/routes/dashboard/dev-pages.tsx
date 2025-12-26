import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/dev-pages')({
  component: RouteComponent,
})

function RouteComponent() {
  return <div>Hello "/dashboard/dev-pages"!</div>
}
