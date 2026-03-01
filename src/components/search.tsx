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

import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { useDebounce } from '~/lib/utils'

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

function highlightMatch(text: string, query: string) {
  if (!query.trim()) return text
  const index = text.toLowerCase().indexOf(query.toLowerCase())
  if (index === -1) return text
  return (
    <>
      {text.slice(0, index)}
      <span className="text-primary font-semibold">
        {text.slice(index, index + query.length)}
      </span>
      {text.slice(index + query.length)}
    </>
  )
}

export function Search() {
  const [open, setOpen] = React.useState(false)
  const [searchTerm, setSearchTerm] = React.useState('')
  const debouncedSearch = useDebounce(searchTerm, 300)
  const navigate = useNavigate()
  const containerRef = React.useRef<HTMLDivElement>(null)

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

  // Click outside to close
  React.useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (
        containerRef.current &&
        !containerRef.current.contains(e.target as Node)
      ) {
        setOpen(false)
      }
    }
    if (open) {
      document.addEventListener('mousedown', handleClickOutside)
      return () => document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [open])

  const schoolQuery = useQuery({
    queryKey: ['search-school', debouncedSearch],
    queryFn: async (): Promise<SchoolSearchResult[]> => {
      if (!debouncedSearch.trim()) return []
      const response = await fetch(
        `https://search.simanga.dev/indexes/schools/search`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            Authorization: `Bearer MzJru9ouNNyL0jP2elnxNcTH+ikf0x7e4bGjmtRhx/w=`,
          },
          body: JSON.stringify({ q: debouncedSearch, limit: 10 }),
        },
      )
      const res = await response.json()
      return res.hits ?? []
    },
    enabled: debouncedSearch.trim().length > 0,
    placeholderData: keepPreviousData,
  })

  const handleSelect = (school: SchoolSearchResult) => {
    setOpen(false)
    setSearchTerm('')
    navigate({
      to: '/dashboard/schools/$emisNumber',
      params: { emisNumber: school.emis_number },
    })
  }

  const handleClear = () => {
    setSearchTerm('')
    setOpen(false)
  }

  return (
    <div ref={containerRef} className="relative mx-auto inline-flex">
      <Command shouldFilter={false} className="bg-border border-b-white">
        <CommandInput
          className="bg-border h-8 border-b border-transparent"
          wrapperClassName="w-[28rem]"
          placeholder="Search schools..."
          loading={schoolQuery.isFetching}
          value={searchTerm}
          onValueChange={(value) => {
            setSearchTerm(value)
            if (value.trim()) setOpen(true)
          }}
          onFocus={() => searchTerm.trim() && setOpen(true)}
          onClear={searchTerm ? handleClear : undefined}
        />
        <div className="text-muted-foreground pointer-events-none absolute inset-y-0 end-0 flex items-center justify-center pe-2">
          <kbd className="bg-background text-muted-foreground/70 inline-flex size-5 max-h-full items-center justify-center rounded px-1 font-[inherit] text-[0.625rem] font-medium shadow-xs">
            /
          </kbd>
        </div>

        <CommandList
          className={`bg-border absolute start-0 end-0 top-10 z-50 rounded-lg shadow-lg transition-all duration-200 ease-out ${
            open
              ? 'animate-in fade-in slide-in-from-top-2'
              : 'pointer-events-none hidden'
          }`}
        >
          {schoolQuery.isError && (
            <div className="text-destructive p-4 text-center text-sm">
              Something went wrong. Please try again.
            </div>
          )}
          <CommandEmpty>No schools found.</CommandEmpty>
          {schoolQuery.data && schoolQuery.data.length > 0 && (
            <CommandGroup heading="Schools">
              {schoolQuery.data.map((school) => (
                <CommandItem
                  key={school.id}
                  onSelect={() => handleSelect(school)}
                  className="cursor-pointer"
                >
                  <IconSchool className="mr-2 h-4 w-4" />
                  <div className="flex min-w-0 flex-1 flex-col">
                    <span>
                      {highlightMatch(school.centre_name, debouncedSearch)}
                    </span>
                    <span className="text-muted-foreground text-xs">
                      {school.district_name}, {school.province}
                    </span>
                  </div>
                  <span className="bg-primary/10 text-primary ml-auto shrink-0 rounded-full px-2 py-0.5 text-xs font-medium">
                    {school.percent_achieved_2024}%
                  </span>
                </CommandItem>
              ))}
            </CommandGroup>
          )}
        </CommandList>
      </Command>
    </div>
  )
}
