import { createFileRoute, Outlet } from '@tanstack/react-router'
import * as React from 'react'

import { SidebarInset, SidebarProvider } from '~/components/ui/sidebar'
import { AppSidebar } from '~/components/app-sidebar'
import { SiteHeader } from '~/components/site-header'

export const Route = createFileRoute('/dashboard')({
  component: RouteComponent,
})

function RouteComponent() {
  return (
    <SidebarProvider
      style={
        {
          '--sidebar-width': 'calc(var(--spacing) * 72)',
        } as React.CSSProperties
      }
    >
      <AppSidebar />
      <SidebarInset>
        <SiteHeader />
        <React.Suspense fallback={<DashboardFallback />}>
          <div className="flex flex-1 flex-col gap-4 p-4 pt-0">
            <Outlet />
          </div>
        </React.Suspense>
      </SidebarInset>
    </SidebarProvider>
  )
}

function DashboardFallback() {
  return (
    <div className="flex flex-1 items-center justify-center p-6 text-sm text-muted-foreground">
      Loading dashboard...
    </div>
  )
}
