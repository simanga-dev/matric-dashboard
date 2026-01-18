import React from 'react'

import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from './ui/command'

import { IconSchool } from '@tabler/icons-react'

import { useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'

interface SchoolSearchResult {
  id: number
  province: string
  district_name: string
  emis_number: string
  centre_number: string
  centre_name: string
  quintile: string
  percent_achieved_2024: string
}

export function Search() {
  const [open, setOpen] = React.useState(false)
  const [searchTerm, setSearchTerm] = React.useState('')
  const navigate = useNavigate()

  React.useEffect(() => {
    const down = (e: KeyboardEvent) => {
      if (e.key === '/' && e.ctrlKey) {
        e.preventDefault()
        setOpen((open) => !open)
      } else if (e.key === 'Escape') {
        setOpen(false)
      }
    }
    document.addEventListener('keydown', down)
    return () => document.removeEventListener('keydown', down)
  }, [])

  const schoolQuery = useQuery({
    queryKey: ['search-school', searchTerm],
    queryFn: async (): Promise<SchoolSearchResult[]> => {
      if (!searchTerm.trim()) return []
      const response = await fetch(
        `https://search.simanga.dev/indexes/schools/search`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer MzJru9ouNNyL0jP2elnxNcTH+ikf0x7e4bGjmtRhx/w=`,
          },
          body: JSON.stringify({ q: searchTerm, limit: 10 }),
        },
      )
      const res = await response.json()
      return res.hits ?? []
    },
    enabled: searchTerm.trim().length > 0,
  })

  const handleSelect = (school: SchoolSearchResult) => {
    setOpen(false)
    setSearchTerm('')
    navigate({
      to: '/dashboard/schools/$emisNumber',
      params: { emisNumber: school.emis_number },
    })
  }

  return (
    <div className="relative mx-auto inline-flex">
      <Command shouldFilter={false} className="bg-border border-b-white">
        <CommandInput
          className="bg-border h-8 w-fit min-w-80 border-b border-transparent ps-9 pe-9"
          placeholder="Search schools..."
          value={searchTerm}
          onValueChange={(value) => {
            setSearchTerm(value)
            if (value.trim()) setOpen(true)
          }}
          onFocus={() => searchTerm.trim() && setOpen(true)}
        />
        <div className="text-muted-foreground pointer-events-none absolute inset-y-0 end-0 flex items-center justify-center pe-2">
          <kbd className="bg-background text-muted-foreground/70 inline-flex size-5 max-h-full items-center justify-center rounded px-1 font-[inherit] text-[0.625rem] font-medium shadow-xs">
            /
          </kbd>
        </div>

        <CommandList
          className={`bg-border absolute start-0 end-0 top-10 z-50 rounded-lg ${open ? '' : 'hidden'}`}
        >
          {schoolQuery.isLoading && searchTerm.trim() && (
            <div className="text-muted-foreground p-4 text-center text-sm">
              Searching...
            </div>
          )}
          {!schoolQuery.isLoading && (
            <CommandEmpty>No schools found.</CommandEmpty>
          )}
          {schoolQuery.data && schoolQuery.data.length > 0 && (
            <CommandGroup heading="Schools">
              {schoolQuery.data.map((school) => (
                <CommandItem
                  key={school.id}
                  onSelect={() => handleSelect(school)}
                  className="cursor-pointer"
                >
                  <IconSchool className="mr-2 h-4 w-4" />
                  <div className="flex flex-col">
                    <span>{school.centre_name}</span>
                    <span className="text-muted-foreground text-xs">
                      {school.district_name}, {school.province}
                    </span>
                  </div>
                </CommandItem>
              ))}
            </CommandGroup>
          )}
        </CommandList>
      </Command>
    </div>
  )
}
