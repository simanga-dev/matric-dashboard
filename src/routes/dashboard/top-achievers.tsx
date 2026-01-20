import { createFileRoute } from '@tanstack/react-router'
import { useSuspenseQuery } from '@tanstack/react-query'
import { convexQuery } from '@convex-dev/react-query'
import { useMutation } from 'convex/react'
import { api } from '~/../convex/_generated/api'
import { useState } from 'react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '~/components/ui/tabs'
import { Card, CardContent } from '~/components/ui/card'
import { Avatar, AvatarFallback, AvatarImage } from '~/components/ui/avatar'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import { IconTrophy, IconDatabase } from '@tabler/icons-react'

export const Route = createFileRoute('/dashboard/top-achievers')({
  component: TopAchieversPage,
})

function TopAchieversPage() {
  const { data: years } = useSuspenseQuery(
    convexQuery(api.topAchievers.getAvailableYears, {})
  )

  const [selectedYear, setSelectedYear] = useState<number>(
    years[0] ?? new Date().getFullYear()
  )

  const seedData = useMutation(api.seedTopAchievers.seedTopAchievers)
  const [isSeeding, setIsSeeding] = useState(false)

  const handleSeed = async () => {
    setIsSeeding(true)
    try {
      await seedData()
      window.location.reload()
    } finally {
      setIsSeeding(false)
    }
  }

  if (years.length === 0) {
    return (
      <div className="flex flex-1 flex-col p-6">
        <h1 className="text-2xl font-bold mb-6">Top Achievers</h1>
        <div className="text-muted-foreground text-center py-12 flex flex-col items-center gap-4">
          <p>No top achievers data available yet.</p>
          <Button onClick={handleSeed} disabled={isSeeding}>
            <IconDatabase className="mr-2 h-4 w-4" />
            {isSeeding ? 'Seeding...' : 'Load Sample Data'}
          </Button>
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-1 flex-col p-6">
      <div className="flex items-center gap-3 mb-6">
        <IconTrophy className="h-8 w-8 text-yellow-500" />
        <h1 className="text-2xl font-bold">Top Achievers</h1>
      </div>

      <Tabs
        value={selectedYear.toString()}
        onValueChange={(v) => setSelectedYear(parseInt(v))}
      >
        <TabsList className="mb-6">
          {years.map((year) => (
            <TabsTrigger key={year} value={year.toString()}>
              {year}
            </TabsTrigger>
          ))}
        </TabsList>

        {years.map((year) => (
          <TabsContent key={year} value={year.toString()}>
            <AchieversList year={year} />
          </TabsContent>
        ))}
      </Tabs>
    </div>
  )
}

function AchieversList({ year }: { year: number }) {
  const { data: achievers } = useSuspenseQuery(
    convexQuery(api.topAchievers.getByYear, { year })
  )

  if (achievers.length === 0) {
    return (
      <div className="text-muted-foreground text-center py-12">
        No achievers found for {year}.
      </div>
    )
  }

  return (
    <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
      {achievers.map((achiever) => (
        <Card key={achiever._id} className="overflow-hidden">
          <CardContent className="p-6">
            <div className="flex items-start gap-4">
              <Avatar className="h-16 w-16">
                <AvatarImage src={achiever.headshot_url} alt={achiever.name} />
                <AvatarFallback className="text-lg">
                  {achiever.name
                    .split(' ')
                    .map((n) => n[0])
                    .join('')
                    .slice(0, 2)
                    .toUpperCase()}
                </AvatarFallback>
              </Avatar>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1">
                  <RankBadge rank={achiever.rank} />
                  <span className="font-semibold truncate">{achiever.name}</span>
                </div>
                <p className="text-sm text-muted-foreground truncate">
                  {achiever.school_name}
                </p>
                <p className="text-xs text-muted-foreground">
                  {achiever.province}
                </p>
              </div>
            </div>
            <div className="mt-4 flex items-center justify-between">
              <span className="text-sm text-muted-foreground">
                Overall Mark
              </span>
              <span className="text-2xl font-bold text-primary">
                {achiever.percentage_mark.toFixed(1)}%
              </span>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  )
}

function RankBadge({ rank }: { rank: number }) {
  if (rank === 1) {
    return (
      <Badge className="bg-yellow-500 hover:bg-yellow-600 text-white">
        🥇 1st
      </Badge>
    )
  }
  if (rank === 2) {
    return (
      <Badge className="bg-gray-400 hover:bg-gray-500 text-white">🥈 2nd</Badge>
    )
  }
  if (rank === 3) {
    return (
      <Badge className="bg-amber-700 hover:bg-amber-800 text-white">
        🥉 3rd
      </Badge>
    )
  }
  return <Badge variant="outline">#{rank}</Badge>
}
