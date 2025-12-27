import { IconArrowNarrowRight, IconTrendingDown, IconTrendingUp } from '@tabler/icons-react'
import { useQuery } from 'convex/react'
import { api } from '../../convex/_generated/api'

import { Badge } from '~/components/ui/badge'
import {
  Card,
  CardAction,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '~/components/ui/card'

function TopSchoolCard() {
  const data = useQuery(api.myFunctions.getTopSchools)

  if (data === undefined) {
    return (
      <Card className="@container/card animate-pulse">
        <CardHeader>
          <CardDescription>Top School Performers</CardDescription>
          <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
            —
          </CardTitle>
        </CardHeader>
      </Card>
    )
  }

  if (data === null) {
    return <Card className="@container/card" />
  }

  const isNullTrend = data.trendRate === null
  const isPositive = !isNullTrend && data.trendRate! >= 0
  const TrendIcon = isNullTrend
    ? IconArrowNarrowRight
    : isPositive
      ? IconTrendingUp
      : IconTrendingDown

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Top School Performers</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {data.totalSchools2023}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <TrendIcon />
            {data.trendRate !== null
              ? `${data.trendRate > 0 ? '+' : ''}${(data.trendRate * 100).toFixed(0)}%`
              : 'N/A'}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          100% pass rate <TrendIcon className="size-4" />
        </div>
        <div className="text-muted-foreground">
          {isNullTrend
            ? 'Not enough data to compare with last year'
            : isPositive
              ? 'More schools achieved 100% pass rate this year'
              : 'Fewer schools achieved 100% pass rate this year'}
        </div>
      </CardFooter>
    </Card>
  )
}

function ExamCentreCard() {
  const data = useQuery(api.myFunctions.getExamCenters)

  if (data === undefined) {
    return (
      <Card className="@container/card animate-pulse">
        <CardHeader>
          <CardDescription>Exam Centers</CardDescription>
          <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
            —
          </CardTitle>
        </CardHeader>
      </Card>
    )
  }

  if (data === null) {
    return <Card className="@container/card" />
  }

  const isNullTrend = data.trendRate === null
  const isConstant = data.trendRate === 0
  const trendDisplay = data.trendRate !== null ? `${data.trendRate > 0 ? '+' : ''}${(data.trendRate * 100).toFixed(0)}%` : 'N/A'

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Exam Centers</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {data.totalCenters2023.toLocaleString()}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <IconArrowNarrowRight />
            {trendDisplay}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          {isNullTrend ? 'Exam Centers' : isConstant ? 'Constant Exam Centers' : 'Exam Center Change'}{' '}
          <IconArrowNarrowRight className="size-4" />
        </div>
        <div className="text-muted-foreground">
          {isNullTrend
            ? 'Not enough data to compare with last year'
            : isConstant
              ? 'The number of exam centers remains the same'
              : `${Math.abs(data.totalCenters2023 - data.totalCenters2022)} ${data.totalCenters2023 > data.totalCenters2022 ? 'more' : 'fewer'} centers than last year`}
        </div>
      </CardFooter>
    </Card>
  )
}

function LearnersCountCard() {
  const data = useQuery(api.myFunctions.getTotalLearners)

  if (data === undefined) {
    return (
      <Card className="@container/card animate-pulse">
        <CardHeader>
          <CardDescription>Learners in 2023</CardDescription>
          <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
            —
          </CardTitle>
        </CardHeader>
      </Card>
    )
  }

  if (data === null) {
    return <Card className="@container/card" />
  }

  const isNullTrend = data.trendRate === null
  const isPositive = !isNullTrend && data.trendRate! >= 0
  const TrendIcon = isNullTrend
    ? IconArrowNarrowRight
    : isPositive
      ? IconTrendingUp
      : IconTrendingDown
  const trendPercent = data.trendRate !== null ? Math.abs(data.trendRate * 100).toFixed(0) : 0

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Learners in 2023</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {data.totalLearners2023.toLocaleString()}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <TrendIcon />
            {data.trendRate !== null ? `${data.trendRate >= 0 ? '+' : '-'}${trendPercent}%` : 'N/A'}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          {isNullTrend
            ? 'Learners in 2023'
            : `Learners ${isPositive ? 'increased' : 'decreased'} by ${trendPercent}%`}
          <TrendIcon className="size-4" />
        </div>
        <div className="text-muted-foreground">
          {isNullTrend
            ? 'Not enough data to compare with last year'
            : isPositive
              ? 'More students wrote matric this year'
              : 'Fewer students wrote matric this year than last year'}
        </div>
      </CardFooter>
    </Card>
  )
}

function PassRateCard() {
  const data = useQuery(api.myFunctions.getMatricPassRate)

  if (data === undefined) {
    return (
      <Card className="@container/card animate-pulse">
        <CardHeader>
          <CardDescription>Pass Rate 2023</CardDescription>
          <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
            —
          </CardTitle>
        </CardHeader>
      </Card>
    )
  }

  if (data === null) {
    return <Card className="@container/card" />
  }

  const isNullTrend = data.trendRate === null
  const isPositive = !isNullTrend && data.trendRate! >= 0
  const TrendIcon = isNullTrend
    ? IconArrowNarrowRight
    : isPositive
      ? IconTrendingUp
      : IconTrendingDown

  return (
    <Card className="@container/card">
      <CardHeader>
        <CardDescription>Pass Rate 2023</CardDescription>
        <CardTitle className="text-2xl font-semibold tabular-nums @[250px]/card:text-3xl">
          {data.passRate2023 !== null ? `${data.passRate2023}%` : 'N/A'}
        </CardTitle>
        <CardAction>
          <Badge variant="outline">
            <TrendIcon />
            {data.trendRate !== null
              ? `${data.trendRate >= 0 ? '+' : ''}${data.trendRate}pp`
              : 'N/A'}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardFooter className="flex-col items-start gap-1.5 text-sm">
        <div className="line-clamp-1 flex gap-2 font-medium">
          {isNullTrend
            ? 'Pass rate for 2023'
            : isPositive
              ? 'Trending up this year'
              : 'Trending down this year'}{' '}
          <TrendIcon className="size-4" />
        </div>
        <div className="text-muted-foreground">
          {isNullTrend
            ? 'Not enough data to compare with last year'
            : 'Learners who passed their Matric results in 2023'}
        </div>
      </CardFooter>
    </Card>
  )
}

export function SectionCards() {
  return (
    <div className="*:data-[slot=card]:from-primary/5 *:data-[slot=card]:to-card dark:*:data-[slot=card]:bg-card grid grid-cols-1 gap-4 px-4 *:data-[slot=card]:bg-gradient-to-t *:data-[slot=card]:shadow-xs lg:px-6 @xl/main:grid-cols-2 @5xl/main:grid-cols-4">
      <TopSchoolCard />
      <ExamCentreCard />
      <LearnersCountCard />
      <PassRateCard />
    </div>
  )
}
