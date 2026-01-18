import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'

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

export const Route = createFileRoute('/dashboard/schools/$emisNumber')({
  component: SchoolDetailPage,
})

function SchoolDetailPage() {
  const { emisNumber } = Route.useParams()

  const { data: school, isLoading } = useQuery({
    queryKey: ['school', emisNumber],
    queryFn: async (): Promise<SchoolData | null> => {
      const response = await fetch(
        `https://search.simanga.dev/indexes/schools/search`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer MzJru9ouNNyL0jP2elnxNcTH+ikf0x7e4bGjmtRhx/w=`,
          },
          body: JSON.stringify({ q: emisNumber, limit: 1 }),
        },
      )
      const res = await response.json()
      return res.hits?.[0] ?? null
    },
  })

  if (isLoading) {
    return (
      <div className="flex h-full items-center justify-center p-8">
        <div className="text-muted-foreground">Loading school details...</div>
      </div>
    )
  }

  if (!school) {
    return (
      <div className="flex h-full items-center justify-center p-8">
        <div className="text-muted-foreground">School not found</div>
      </div>
    )
  }

  const years = [
    {
      year: 2022,
      progressed: school.progressed_number_2022,
      wrote: school.total_wrote_2022,
      achieved: school.total_achieved_2022,
      percent: school.percent_achieved_2022,
    },
    {
      year: 2023,
      progressed: school.progressed_number_2023,
      wrote: school.total_wrote_2023,
      achieved: school.total_achieved_2023,
      percent: school.percent_achieved_2023,
    },
    {
      year: 2024,
      progressed: school.progressed_number_2024,
      wrote: school.total_wrote_2024,
      achieved: school.total_achieved_2024,
      percent: school.percent_achieved_2024,
    },
  ]

  return (
    <div className="flex flex-1 flex-col gap-6 p-6">
      <div className="rounded-lg border p-6">
        <h1 className="text-2xl font-bold">{school.centre_name}</h1>
        <div className="text-muted-foreground mt-2 grid gap-1 text-sm">
          <p>
            <span className="font-medium">Province:</span> {school.province}
          </p>
          <p>
            <span className="font-medium">District:</span>{' '}
            {school.district_name}
          </p>
          <p>
            <span className="font-medium">EMIS Number:</span>{' '}
            {school.emis_number}
          </p>
          <p>
            <span className="font-medium">Centre Number:</span>{' '}
            {school.centre_number}
          </p>
          <p>
            <span className="font-medium">Quintile:</span> {school.quintile}
          </p>
        </div>
      </div>

      <div className="rounded-lg border p-6">
        <h2 className="mb-4 text-xl font-semibold">Performance History</h2>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="px-4 py-2 text-left font-medium">Year</th>
                <th className="px-4 py-2 text-right font-medium">Progressed</th>
                <th className="px-4 py-2 text-right font-medium">
                  Total Wrote
                </th>
                <th className="px-4 py-2 text-right font-medium">
                  Total Achieved
                </th>
                <th className="px-4 py-2 text-right font-medium">Pass Rate</th>
              </tr>
            </thead>
            <tbody>
              {years.map((row) => (
                <tr key={row.year} className="border-b">
                  <td className="px-4 py-2 font-medium">{row.year}</td>
                  <td className="px-4 py-2 text-right">{row.progressed}</td>
                  <td className="px-4 py-2 text-right">{row.wrote}</td>
                  <td className="px-4 py-2 text-right">{row.achieved}</td>
                  <td className="px-4 py-2 text-right font-semibold">
                    {row.percent}%
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  )
}
