import { createFileRoute } from '@tanstack/react-router'
import { Bar, BarChart, CartesianGrid, Legend, XAxis, YAxis } from 'recharts'

import { Badge } from '~/components/ui/badge'
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '~/components/ui/card'
import {
  ChartContainer,
  ChartLegendContent,
  ChartTooltip,
  ChartTooltipContent,
} from '~/components/ui/chart'

export const Route = createFileRoute('/dashboard/analytic')({
  component: RouteComponent,
})

const topSchools = [
  { school: 'Maseru High', passRate: 96, learners: 182 },
  { school: 'St. Marks', passRate: 94, learners: 168 },
  { school: 'Leribe Senior', passRate: 91, learners: 204 },
  { school: 'Mafeteng Academy', passRate: 88, learners: 176 },
  { school: 'Berea College', passRate: 85, learners: 190 },
]

const schoolStats = [
  { year: '2020', students: 4200, staff: 280 },
  { year: '2021', students: 4450, staff: 295 },
  { year: '2022', students: 4620, staff: 310 },
  { year: '2023', students: 4890, staff: 328 },
  { year: '2024', students: 5120, staff: 340 },
]

const schoolChartConfig = {
  passRate: {
    label: 'Pass rate',
    color: 'var(--primary)',
  },
  students: {
    label: 'Students',
    color: 'var(--chart-2)',
  },
  staff: {
    label: 'Staff',
    color: 'var(--chart-4)',
  },
} satisfies Record<string, { label: string; color: string }>

function RouteComponent() {
  return (
    <div className="flex flex-1 flex-col gap-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold tracking-tight">
          Matric Analytics
        </h1>
        <p className="text-muted-foreground">
          Sample overview of pass rates, top schools, learners, and staff.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          title="Overall pass rate"
          value="87.3%"
          detail="+4.2% from last year"
        />
        <MetricCard
          title="Top schools"
          value="5"
          detail="Schools above 85% pass rate"
        />
        <MetricCard
          title="Learners"
          value="5,120"
          detail="Matric candidates in 2024"
        />
        <MetricCard
          title="Staff"
          value="340"
          detail="Teachers and support staff"
        />
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Top Schools by Pass Rate</CardTitle>
            <CardDescription>
              Sample school performance for the latest matric cycle.
            </CardDescription>
            <CardAction>
              <Badge variant="outline">2024 sample</Badge>
            </CardAction>
          </CardHeader>
          <CardContent>
            <ChartContainer
              config={schoolChartConfig}
              className="h-[340px] w-full"
            >
              <BarChart
                data={topSchools}
                layout="vertical"
                margin={{ left: 16, right: 16 }}
              >
                <CartesianGrid horizontal={false} />
                <XAxis
                  type="number"
                  domain={[0, 100]}
                  tickFormatter={(value) => `${value}%`}
                />
                <YAxis
                  dataKey="school"
                  type="category"
                  width={120}
                  tickLine={false}
                  axisLine={false}
                />
                <ChartTooltip
                  cursor={false}
                  content={
                    <ChartTooltipContent
                      indicator="dot"
                      formatter={(value) => [`${value}%`, 'Pass rate']}
                    />
                  }
                />
                <Bar
                  dataKey="passRate"
                  fill="var(--color-passRate)"
                  radius={8}
                />
              </BarChart>
            </ChartContainer>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Learners and Staff</CardTitle>
            <CardDescription>
              Growth in student numbers and staffing across recent years.
            </CardDescription>
            <CardAction>
              <Badge variant="outline">Trend view</Badge>
            </CardAction>
          </CardHeader>
          <CardContent>
            <ChartContainer
              config={schoolChartConfig}
              className="h-[340px] w-full"
            >
              <BarChart
                data={schoolStats}
                margin={{ top: 10, right: 16, left: 0 }}
              >
                <CartesianGrid vertical={false} />
                <XAxis
                  dataKey="year"
                  tickLine={false}
                  axisLine={false}
                  tickMargin={8}
                />
                <ChartTooltip
                  cursor={false}
                  content={<ChartTooltipContent indicator="dot" />}
                />
                <Legend content={<ChartLegendContent />} />
                <Bar
                  dataKey="students"
                  fill="var(--color-students)"
                  radius={8}
                />
                <Bar dataKey="staff" fill="var(--color-staff)" radius={8} />
              </BarChart>
            </ChartContainer>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}

function MetricCard({
  title,
  value,
  detail,
}: {
  title: string
  value: string
  detail: string
}) {
  return (
    <Card>
      <CardHeader>
        <CardDescription>{title}</CardDescription>
        <CardTitle className="text-3xl tabular-nums">{value}</CardTitle>
      </CardHeader>
      <CardContent className="text-muted-foreground text-sm">
        {detail}
      </CardContent>
    </Card>
  )
}
