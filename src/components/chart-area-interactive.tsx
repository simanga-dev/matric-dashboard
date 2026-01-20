'use client'

import * as React from 'react'
import { Area, AreaChart, CartesianGrid, XAxis } from 'recharts'

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
  ChartTooltip,
  ChartTooltipContent,
} from '~/components/ui/chart'
import type { ChartConfig } from '~/components/ui/chart'
import { ToggleGroup, ToggleGroupItem } from '~/components/ui/toggle-group'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '~/components/ui/select'

export const description = 'An interactive area chart'

const chartData = [
  { year: '2008', passRate: 62.6, totalLearners: 533561 },
  { year: '2009', passRate: 60.6, totalLearners: 552073 },
  { year: '2010', passRate: 67.8, totalLearners: 537543 },
  { year: '2011', passRate: 70.2, totalLearners: 496090 },
  { year: '2012', passRate: 73.9, totalLearners: 511152 },
  { year: '2013', passRate: 78.2, totalLearners: 562112 },
  { year: '2014', passRate: 75.8, totalLearners: 532860 },
  { year: '2015', passRate: 70.7, totalLearners: 644536 },
  { year: '2016', passRate: 72.5, totalLearners: 610178 },
  { year: '2017', passRate: 75.1, totalLearners: 629155 },
  { year: '2018', passRate: 78.2, totalLearners: 624733 },
  { year: '2019', passRate: 81.3, totalLearners: 616754 },
  { year: '2020', passRate: 76.2, totalLearners: 578468 },
  { year: '2021', passRate: 76.4, totalLearners: 706451 },
  { year: '2022', passRate: 80.1, totalLearners: 699659 },
  { year: '2023', passRate: 82.9, totalLearners: 719541 },
  { year: '2024', passRate: 87.3, totalLearners: 737472 },
]

const chartConfig = {
  passRate: {
    label: 'Pass Rate',
    color: 'var(--primary)',
  },
} satisfies ChartConfig

export function ChartAreaInteractive() {
  const [timeRange, setTimeRange] = React.useState('all')

  const filteredData = React.useMemo(() => {
    if (timeRange === 'all') return chartData
    const currentYear = 2024
    const yearsToShow = timeRange === '5y' ? 5 : 10
    const startYear = currentYear - yearsToShow + 1
    return chartData.filter((item) => Number(item.year) >= startYear)
  }, [timeRange])

  const getDescription = () => {
    if (timeRange === '5y') return 'Last 5 years (2020 - 2024)'
    if (timeRange === '10y') return 'Last 10 years (2015 - 2024)'
    return '2008 - 2024'
  }

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardTitle>NSC Pass Rate Trends</CardTitle>
        <CardDescription>
          <span className="hidden @[540px]/card:block">
            National Senior Certificate pass rates — {getDescription()}
          </span>
          <span className="@[540px]/card:hidden">{getDescription()}</span>
        </CardDescription>
        <CardAction>
          <ToggleGroup
            type="single"
            value={timeRange}
            onValueChange={(value) => value && setTimeRange(value)}
            variant="outline"
            className="hidden *:data-[slot=toggle-group-item]:!px-4 @[767px]/card:flex"
          >
            <ToggleGroupItem value="5y">Last 5 Years</ToggleGroupItem>
            <ToggleGroupItem value="10y">Last 10 Years</ToggleGroupItem>
            <ToggleGroupItem value="all">All Years</ToggleGroupItem>
          </ToggleGroup>
          <Select value={timeRange} onValueChange={setTimeRange}>
            <SelectTrigger
              className="flex w-40 **:data-[slot=select-value]:block **:data-[slot=select-value]:truncate @[767px]/card:hidden"
              size="sm"
              aria-label="Select time range"
            >
              <SelectValue placeholder="All Years" />
            </SelectTrigger>
            <SelectContent className="rounded-xl">
              <SelectItem value="5y" className="rounded-lg">
                Last 5 Years
              </SelectItem>
              <SelectItem value="10y" className="rounded-lg">
                Last 10 Years
              </SelectItem>
              <SelectItem value="all" className="rounded-lg">
                All Years
              </SelectItem>
            </SelectContent>
          </Select>
        </CardAction>
      </CardHeader>
      <CardContent className="px-2 pt-4 sm:px-6 sm:pt-6">
        <ChartContainer
          config={chartConfig}
          className="aspect-auto h-[250px] w-full"
        >
          <AreaChart data={filteredData}>
            <defs>
              <linearGradient id="fillPassRate" x1="0" y1="0" x2="0" y2="1">
                <stop
                  offset="5%"
                  stopColor="var(--color-passRate)"
                  stopOpacity={0.8}
                />
                <stop
                  offset="95%"
                  stopColor="var(--color-passRate)"
                  stopOpacity={0.1}
                />
              </linearGradient>

            </defs>
            <CartesianGrid vertical={false} />
            <XAxis
              dataKey="year"
              tickLine={false}
              axisLine={false}
              tickMargin={8}
            />
            <ChartTooltip
              cursor={false}
              content={
                <ChartTooltipContent
                  labelFormatter={(value) => `Year ${value}`}
                  indicator="dot"
                  formatter={(value, name) => {
                    if (name === 'passRate') {
                      return [`${value}%`, 'Pass Rate']
                    }
                    return [value, name]
                  }}
                />
              }
            />
            <Area
              dataKey="passRate"
              type="monotone"
              fill="url(#fillPassRate)"
              stroke="var(--color-passRate)"
            />
          </AreaChart>
        </ChartContainer>
      </CardContent>
    </Card>
  )
}
