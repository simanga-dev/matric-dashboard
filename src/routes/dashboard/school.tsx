import { useState } from 'react'
import { createFileRoute, Link } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import {
  IconSchool,
  IconSearch,
  IconTrendingUp,
  IconTrendingDown,
  IconMinus,
  IconLayoutGrid,
  IconLayoutList,
} from '@tabler/icons-react'

import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '~/components/ui/card'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '~/components/ui/select'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '~/components/ui/table'
import { ToggleGroup, ToggleGroupItem } from '~/components/ui/toggle-group'
import { Skeleton } from '~/components/ui/skeleton'

export const Route = createFileRoute('/dashboard/school')({
  component: SchoolListingPage,
})

interface SchoolData {
  id: number
  province: string
  district_name: string
  emis_number: string
  centre_number: string
  centre_name: string
  quintile: string
  progressed_number_2022: string
  total_wrote_2022: string
  total_achieved_2022: string
  percent_achieved_2022: string
  progressed_number_2023: string
  total_wrote_2023: string
  total_achieved_2023: string
  percent_achieved_2023: string
  progressed_number_2024: string
  total_wrote_2024: string
  total_achieved_2024: string
  percent_achieved_2024: string
}

const PROVINCES = [
  'All Provinces',
  'Eastern Cape',
  'Free State',
  'Gauteng',
  'KwaZulu-Natal',
  'Limpopo',
  'Mpumalanga',
  'North West',
  'Northern Cape',
  'Western Cape',
]

const QUINTILES = ['All Quintiles', '1', '2', '3', '4', '5']

function SchoolListingPage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [province, setProvince] = useState('All Provinces')
  const [quintile, setQuintile] = useState('All Quintiles')
  const [viewMode, setViewMode] = useState<'grid' | 'table'>('grid')
  const [page, setPage] = useState(1)
  const limit = 20

  const buildSearchQuery = () => {
    const parts: string[] = []
    if (searchTerm.trim()) parts.push(searchTerm.trim())
    if (province !== 'All Provinces') parts.push(province)
    if (quintile !== 'All Quintiles') parts.push(`quintile:${quintile}`)
    return parts.join(' ') || '*'
  }

  const { data, isLoading } = useQuery({
    queryKey: ['schools', searchTerm, province, quintile, page],
    queryFn: async (): Promise<{ hits: SchoolData[]; totalHits: number }> => {
      const response = await fetch(
        `https://search.simanga.dev/indexes/schools/search`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer MzJru9ouNNyL0jP2elnxNcTH+ikf0x7e4bGjmtRhx/w=`,
          },
          body: JSON.stringify({
            q: buildSearchQuery(),
            limit,
            offset: (page - 1) * limit,
          }),
        },
      )
      const res = await response.json()
      return { hits: res.hits ?? [], totalHits: res.estimatedTotalHits ?? 0 }
    },
  })

  const schools = data?.hits ?? []
  const totalHits = data?.totalHits ?? 0
  const totalPages = Math.ceil(totalHits / limit)

  const getTrend = (school: SchoolData) => {
    const rate2023 = parseFloat(school.percent_achieved_2023) || 0
    const rate2024 = parseFloat(school.percent_achieved_2024) || 0
    const diff = rate2024 - rate2023
    if (diff > 1) return 'up'
    if (diff < -1) return 'down'
    return 'stable'
  }

  const getQuintileColor = (q: string) => {
    const num = parseInt(q)
    if (num <= 2) return 'bg-red-500/10 text-red-600 dark:text-red-400'
    if (num === 3) return 'bg-yellow-500/10 text-yellow-600 dark:text-yellow-400'
    return 'bg-green-500/10 text-green-600 dark:text-green-400'
  }

  return (
    <div className="flex flex-1 flex-col gap-6 p-4 lg:p-6">
      {/* Header */}
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-bold tracking-tight">Schools Directory</h1>
        <p className="text-muted-foreground text-sm">
          Browse and search {totalHits.toLocaleString()} schools across South
          Africa
        </p>
      </div>

      {/* Stats Cards */}
      <div className="grid gap-4 md:grid-cols-3">
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Total Schools</CardDescription>
            <CardTitle className="text-2xl tabular-nums">
              {isLoading ? <Skeleton className="h-8 w-20" /> : totalHits.toLocaleString()}
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Filtered Results</CardDescription>
            <CardTitle className="text-2xl tabular-nums">
              {isLoading ? <Skeleton className="h-8 w-20" /> : schools.length}
            </CardTitle>
          </CardHeader>
        </Card>
        <Card>
          <CardHeader className="pb-2">
            <CardDescription>Current Page</CardDescription>
            <CardTitle className="text-2xl tabular-nums">
              {page} / {totalPages || 1}
            </CardTitle>
          </CardHeader>
        </Card>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <IconSearch className="text-muted-foreground absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2" />
          <Input
            placeholder="Search by name or EMIS..."
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value)
              setPage(1)
            }}
            className="pl-9"
          />
        </div>

        <Select
          value={province}
          onValueChange={(v) => {
            setProvince(v)
            setPage(1)
          }}
        >
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder="Province" />
          </SelectTrigger>
          <SelectContent>
            {PROVINCES.map((p) => (
              <SelectItem key={p} value={p}>
                {p}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={quintile}
          onValueChange={(v) => {
            setQuintile(v)
            setPage(1)
          }}
        >
          <SelectTrigger className="w-[140px]">
            <SelectValue placeholder="Quintile" />
          </SelectTrigger>
          <SelectContent>
            {QUINTILES.map((q) => (
              <SelectItem key={q} value={q}>
                {q === 'All Quintiles' ? q : `Quintile ${q}`}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <ToggleGroup
          type="single"
          value={viewMode}
          onValueChange={(v) => v && setViewMode(v as 'grid' | 'table')}
          className="ml-auto"
        >
          <ToggleGroupItem value="grid" aria-label="Grid view">
            <IconLayoutGrid className="h-4 w-4" />
          </ToggleGroupItem>
          <ToggleGroupItem value="table" aria-label="Table view">
            <IconLayoutList className="h-4 w-4" />
          </ToggleGroupItem>
        </ToggleGroup>
      </div>

      {/* Content */}
      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {Array.from({ length: 8 }).map((_, i) => (
            <Card key={i} className="animate-pulse">
              <CardHeader>
                <Skeleton className="h-5 w-3/4" />
                <Skeleton className="mt-2 h-4 w-1/2" />
              </CardHeader>
              <CardContent>
                <Skeleton className="h-4 w-full" />
                <Skeleton className="mt-2 h-4 w-2/3" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : viewMode === 'grid' ? (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {schools.map((school) => {
            const trend = getTrend(school)
            const TrendIcon =
              trend === 'up'
                ? IconTrendingUp
                : trend === 'down'
                  ? IconTrendingDown
                  : IconMinus

            return (
              <Link
                key={school.id}
                to="/dashboard/schools/$emisNumber"
                params={{ emisNumber: school.emis_number }}
                className="group"
              >
                <Card className="h-full transition-all hover:shadow-md group-hover:border-primary/50">
                  <CardHeader className="pb-3">
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex items-center gap-2">
                        <IconSchool className="text-muted-foreground h-5 w-5 shrink-0" />
                        <CardTitle className="line-clamp-2 text-sm font-semibold">
                          {school.centre_name}
                        </CardTitle>
                      </div>
                      <Badge
                        variant="secondary"
                        className={getQuintileColor(school.quintile)}
                      >
                        Q{school.quintile}
                      </Badge>
                    </div>
                    <CardDescription className="text-xs">
                      {school.district_name}, {school.province}
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="pt-0">
                    <div className="flex items-center justify-between">
                      <div>
                        <p className="text-muted-foreground text-xs">
                          2024 Pass Rate
                        </p>
                        <p className="text-lg font-bold tabular-nums">
                          {school.percent_achieved_2024}%
                        </p>
                      </div>
                      <div
                        className={`flex items-center gap-1 text-sm ${
                          trend === 'up'
                            ? 'text-green-600'
                            : trend === 'down'
                              ? 'text-red-600'
                              : 'text-muted-foreground'
                        }`}
                      >
                        <TrendIcon className="h-4 w-4" />
                        <span className="text-xs">vs 2023</span>
                      </div>
                    </div>
                    <div className="mt-3 flex gap-4 text-xs text-muted-foreground">
                      <span>Wrote: {school.total_wrote_2024}</span>
                      <span>Passed: {school.total_achieved_2024}</span>
                    </div>
                  </CardContent>
                </Card>
              </Link>
            )
          })}
        </div>
      ) : (
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>School Name</TableHead>
                <TableHead>District</TableHead>
                <TableHead>Province</TableHead>
                <TableHead className="text-center">Quintile</TableHead>
                <TableHead className="text-right">Wrote</TableHead>
                <TableHead className="text-right">Passed</TableHead>
                <TableHead className="text-right">Pass Rate</TableHead>
                <TableHead className="text-center">Trend</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {schools.map((school) => {
                const trend = getTrend(school)
                const TrendIcon =
                  trend === 'up'
                    ? IconTrendingUp
                    : trend === 'down'
                      ? IconTrendingDown
                      : IconMinus

                return (
                  <TableRow key={school.id} className="cursor-pointer">
                    <TableCell>
                      <Link
                        to="/dashboard/schools/$emisNumber"
                        params={{ emisNumber: school.emis_number }}
                        className="font-medium hover:underline"
                      >
                        {school.centre_name}
                      </Link>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {school.district_name}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {school.province}
                    </TableCell>
                    <TableCell className="text-center">
                      <Badge
                        variant="secondary"
                        className={getQuintileColor(school.quintile)}
                      >
                        Q{school.quintile}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {school.total_wrote_2024}
                    </TableCell>
                    <TableCell className="text-right tabular-nums">
                      {school.total_achieved_2024}
                    </TableCell>
                    <TableCell className="text-right font-semibold tabular-nums">
                      {school.percent_achieved_2024}%
                    </TableCell>
                    <TableCell className="text-center">
                      <TrendIcon
                        className={`mx-auto h-4 w-4 ${
                          trend === 'up'
                            ? 'text-green-600'
                            : trend === 'down'
                              ? 'text-red-600'
                              : 'text-muted-foreground'
                        }`}
                      />
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </div>
      )}

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage((p) => Math.max(1, p - 1))}
            disabled={page === 1}
          >
            Previous
          </Button>
          <div className="flex items-center gap-1">
            {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
              let pageNum: number
              if (totalPages <= 5) {
                pageNum = i + 1
              } else if (page <= 3) {
                pageNum = i + 1
              } else if (page >= totalPages - 2) {
                pageNum = totalPages - 4 + i
              } else {
                pageNum = page - 2 + i
              }
              return (
                <Button
                  key={pageNum}
                  variant={page === pageNum ? 'default' : 'outline'}
                  size="sm"
                  onClick={() => setPage(pageNum)}
                  className="w-9"
                >
                  {pageNum}
                </Button>
              )
            })}
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            disabled={page === totalPages}
          >
            Next
          </Button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && schools.length === 0 && (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <IconSchool className="text-muted-foreground mb-4 h-12 w-12" />
          <h3 className="text-lg font-semibold">No schools found</h3>
          <p className="text-muted-foreground text-sm">
            Try adjusting your search or filter criteria
          </p>
        </div>
      )}
    </div>
  )
}
