import { createFileRoute, Link } from '@tanstack/react-router'

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
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '~/components/ui/table'

export const Route = createFileRoute('/dashboard/schools')({
  component: SchoolsRoute,
})

const schools = [
  {
    emis: '10010001',
    name: 'Maseru High School',
    district: 'Maseru',
    province: 'Maseru',
    quintile: 5,
    learners: 182,
    passRate: 96,
  },
  {
    emis: '10010012',
    name: 'St. Marks College',
    district: 'Mafeteng',
    province: 'Mafeteng',
    quintile: 5,
    learners: 168,
    passRate: 94,
  },
  {
    emis: '10010023',
    name: 'Leribe Senior School',
    district: 'Leribe',
    province: 'Leribe',
    quintile: 4,
    learners: 204,
    passRate: 91,
  },
  {
    emis: '10010034',
    name: 'Berea College',
    district: 'Berea',
    province: 'Berea',
    quintile: 4,
    learners: 190,
    passRate: 88,
  },
  {
    emis: '10010045',
    name: 'Thaba-Tseka Academy',
    district: 'Thaba-Tseka',
    province: 'Thaba-Tseka',
    quintile: 3,
    learners: 155,
    passRate: 84,
  },
  {
    emis: '10010056',
    name: 'Quthing High School',
    district: 'Quthing',
    province: 'Quthing',
    quintile: 3,
    learners: 172,
    passRate: 82,
  },
]

function SchoolsRoute() {
  return (
    <div className="flex flex-1 flex-col gap-6">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold tracking-tight">
          Schools Directory
        </h1>
        <p className="text-muted-foreground">
          Sample list of schools with pass rate and learner counts.
        </p>
      </div>

      <div className="grid gap-4 md:grid-cols-3">
        <SummaryCard
          title="Total schools"
          value="6"
          detail="Sample directory entries"
        />
        <SummaryCard
          title="Average pass rate"
          value="89.2%"
          detail="Across the sample schools"
        />
        <SummaryCard
          title="Total learners"
          value="1,071"
          detail="Combined matric class size"
        />
      </div>

      <Card>
        <CardHeader>
          <CardTitle>School list</CardTitle>
          <CardDescription>
            Click a school to open the detailed profile page.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>School</TableHead>
                <TableHead>District</TableHead>
                <TableHead>Province</TableHead>
                <TableHead>Quintile</TableHead>
                <TableHead>Learners</TableHead>
                <TableHead>Pass rate</TableHead>
                <TableHead className="text-right">Action</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {schools.map((school) => (
                <TableRow key={school.emis}>
                  <TableCell>
                    <div className="grid gap-1">
                      <span className="font-medium">{school.name}</span>
                      <span className="text-muted-foreground text-xs">
                        EMIS {school.emis}
                      </span>
                    </div>
                  </TableCell>
                  <TableCell>{school.district}</TableCell>
                  <TableCell>{school.province}</TableCell>
                  <TableCell>
                    <Badge variant="outline">Quintile {school.quintile}</Badge>
                  </TableCell>
                  <TableCell>{school.learners}</TableCell>
                  <TableCell>{school.passRate}%</TableCell>
                  <TableCell className="text-right">
                    <Button asChild variant="ghost" size="sm">
                      <Link
                        to="/dashboard/schools/$emisNumber"
                        params={{ emisNumber: school.emis }}
                      >
                        View
                      </Link>
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </CardContent>
      </Card>
    </div>
  )
}

function SummaryCard({
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
