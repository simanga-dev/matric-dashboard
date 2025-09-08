import { createFileRoute, Outlet } from '@tanstack/react-router'

import { SidebarInset, SidebarProvider } from '~/components/ui/sidebar'
import { AppSidebar } from '~/components/app-sidebar'
import { SiteHeader } from '~/components/site-header'
import { SectionCards } from '~/components/section-cards'
import { ChartAreaInteractive } from '~/components/chart-area-interactive'
import { DataTable } from '~/components/data-table'
import { cn } from '~/lib/utils'

export const Route = createFileRoute('/dashboard')({
  component: RouteComponent,
})

function RouteComponent() {
  return (
    <SidebarProvider
      style={
        {
          '--sidebar-width': 'calc(var(--spacing) * 72)',
          '--header-height': 'calc(var(--spacing) * 12)',
        } as React.CSSProperties
      }
    >
      <AppSidebar variant="inset" />
      <SidebarInset>
        <SiteHeader />
        <div
          data-slot="sidebar-inset"
          className={cn(
            'bg-background relative m-2 h-(--main-height)',
            'md:peer-data-[variant=inset]: rounded-xl shadow-sm md:peer-data-[variant=inset]:m-2 md:peer-data-[variant=inset]:ml-0 md:peer-data-[variant=inset]:rounded-xl md:peer-data-[variant=inset]:peer-data-[state=collapsed]:ml-2',
            'sticky start-8 top-14 h-full w-full overflow-y-auto',
          )}
        >
          <Outlet />
        </div>
      </SidebarInset>
    </SidebarProvider>
  )
}
