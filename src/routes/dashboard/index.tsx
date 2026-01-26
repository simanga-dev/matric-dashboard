import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard/')({
  component: DashboardHome,
})

import { SectionCards } from '~/components/section-cards'
import { ChartAreaInteractive } from '~/components/chart-area-interactive'
import { DataTable } from '~/components/data-table'


function DashboardHome() {
  return (
    <div className="flex flex-1 flex-col">
      <div className="@container/main flex flex-1 flex-col gap-2">
        <div className="flex flex-col gap-4 py-4 md:gap-6 md:py-6">
          <SectionCards />
          <div className="px-4 lg:px-6">
            <ChartAreaInteractive />
          </div>
          <DataTable />
        </div>
      </div>
    </div>
  )
}
