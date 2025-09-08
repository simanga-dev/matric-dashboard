import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/')({
  component: DashboardHome,
})

import data from './data.json'
import { SidebarInset, SidebarProvider } from '~/components/ui/sidebar'
import { AppSidebar } from '~/components/app-sidebar'
import { SiteHeader } from '~/components/site-header'
import { SectionCards } from '~/components/section-cards'
import { ChartAreaInteractive } from '~/components/chart-area-interactive'
import { DataTable } from '~/components/data-table'

export default function DashboardHome() {
  return (
    <div className="flex flex-1 flex-col">
      <div className="@container/main flex flex-1 flex-col gap-2">
        <div className="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
          <SectionCards />
          <div className="px-4 lg:px-6">
            <ChartAreaInteractive />
          </div>
          <DataTable data={data} />
        </div>
      </div>
    </div>
  )
}
