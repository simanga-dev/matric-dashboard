import * as React from 'react'
import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  MouseSensor,
  TouchSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type UniqueIdentifier,
} from '@dnd-kit/core'
import { restrictToVerticalAxis } from '@dnd-kit/modifiers'
import {
  arrayMove,
  SortableContext,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import {
  IconChevronDown,
  IconChevronLeft,
  IconChevronRight,
  IconChevronsLeft,
  IconChevronsRight,
  IconCircleCheckFilled,
  IconDotsVertical,
  IconGripVertical,
  IconLayoutColumns,
  IconTrendingDown,
  IconTrendingUp,
} from '@tabler/icons-react'
import { useQuery } from 'convex/react'

import {
  flexRender,
  getCoreRowModel,
  getFacetedRowModel,
  getFacetedUniqueValues,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  useReactTable,
  type ColumnDef,
  type ColumnFiltersState,
  type Row,
  type SortingState,
  type VisibilityState,
} from '@tanstack/react-table'

import { Area, AreaChart, CartesianGrid, XAxis } from 'recharts'
import { z } from 'zod'

import { api } from '../../convex/_generated/api'
import { useIsMobile } from '~/hooks/use-mobile'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import {
  ChartContainer,
  ChartTooltip,
  ChartTooltipContent,
} from '~/components/ui/chart'
import type { ChartConfig } from '~/components/ui/chart'
import { Checkbox } from '~/components/ui/checkbox'
import {
  Drawer,
  DrawerClose,
  DrawerContent,
  DrawerDescription,
  DrawerFooter,
  DrawerHeader,
  DrawerTitle,
  DrawerTrigger,
} from '~/components/ui/drawer'
import {
  DropdownMenu,
  DropdownMenuCheckboxItem,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '~/components/ui/dropdown-menu'
import { Input } from '~/components/ui/input'
import { Label } from '~/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '~/components/ui/select'
import { Separator } from '~/components/ui/separator'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '~/components/ui/table'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '~/components/ui/tabs'

export const schoolPerformanceSchema = z.object({
  _id: z.string(), // Convex Id serializes to string on the client
  natemis: z.number(),
  schoolName: z.string(),
  province: z.string(),
  district: z.string(),
  quintile: z.number().nullable(),
  passRateCurrent: z.number().nullable(),
  passRatePrevious: z.number().nullable(),
  currentYear: z.number(),
  previousYear: z.number(),
  totalWrote: z.number(),
  totalAchieved: z.number(),
  trend: z.number().nullable(),
  status: z.string(),
})

export type SchoolPerformance = z.infer<typeof schoolPerformanceSchema>

function DragHandle({ id }: { id: string }) {
  const { attributes, listeners } = useSortable({
    id,
  })

  return (
    <Button
      {...attributes}
      {...listeners}
      variant="ghost"
      size="icon"
      className="text-muted-foreground size-7 hover:bg-transparent"
    >
      <IconGripVertical className="text-muted-foreground size-3" />
      <span className="sr-only">Drag to reorder</span>
    </Button>
  )
}

function getStatusBadgeVariant(status: string) {
  switch (status) {
    case 'Excellent':
      return 'default'
    case 'Good':
      return 'secondary'
    case 'Average':
      return 'outline'
    case 'Needs Improvement':
      return 'destructive'
    default:
      return 'outline'
  }
}

function getStatusIcon(status: string) {
  if (status === 'Excellent') {
    return (
      <IconCircleCheckFilled className="fill-green-500 dark:fill-green-400" />
    )
  }
  return null
}

const columns: ColumnDef<SchoolPerformance>[] = [
  {
    id: 'drag',
    header: () => null,
    cell: ({ row }) => <DragHandle id={row.original._id} />,
  },
  {
    id: 'select',
    header: ({ table }) => (
      <div className="flex items-center justify-center">
        <Checkbox
          checked={
            table.getIsAllPageRowsSelected() ||
            (table.getIsSomePageRowsSelected() && 'indeterminate')
          }
          onCheckedChange={(value) => table.toggleAllPageRowsSelected(!!value)}
          aria-label="Select all"
        />
      </div>
    ),
    cell: ({ row }) => (
      <div className="flex items-center justify-center">
        <Checkbox
          checked={row.getIsSelected()}
          onCheckedChange={(value) => row.toggleSelected(!!value)}
          aria-label="Select row"
        />
      </div>
    ),
    enableSorting: false,
    enableHiding: false,
  },
  {
    accessorKey: 'schoolName',
    header: 'School Name',
    cell: ({ row }) => <TableCellViewer item={row.original} />,
    enableHiding: false,
  },
  {
    accessorKey: 'province',
    header: 'Province',
    cell: ({ row }) => (
      <div className="max-w-32 truncate">
        <Badge variant="outline" className="text-muted-foreground px-1.5">
          {row.original.province}
        </Badge>
      </div>
    ),
  },
  {
    accessorKey: 'district',
    header: 'District',
    cell: ({ row }) => (
      <div className="max-w-40 truncate text-sm">{row.original.district}</div>
    ),
  },
  {
    accessorKey: 'quintile',
    header: 'Quintile',
    cell: ({ row }) => (
      <Badge variant="secondary" className="px-2">
        Q{row.original.quintile ?? 'N/A'}
      </Badge>
    ),
  },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => (
      <Badge
        variant={getStatusBadgeVariant(row.original.status)}
        className="gap-1 px-1.5"
      >
        {getStatusIcon(row.original.status)}
        {row.original.status}
      </Badge>
    ),
  },
  {
    accessorKey: 'passRateCurrent',
    header: () => <div className="w-full text-right">Pass Rate</div>,
    cell: ({ row }) => (
      <div className="text-right font-medium">
        {row.original.passRateCurrent !== null
          ? `${row.original.passRateCurrent.toFixed(1)}%`
          : 'N/A'}
      </div>
    ),
  },
  {
    accessorKey: 'totalWrote',
    header: () => <div className="w-full text-right">Total Wrote</div>,
    cell: ({ row }) => (
      <div className="text-right tabular-nums">
        {row.original.totalWrote.toLocaleString()}
      </div>
    ),
  },
  {
    accessorKey: 'totalAchieved',
    header: () => <div className="w-full text-right">Achieved</div>,
    cell: ({ row }) => (
      <div className="text-right tabular-nums">
        {row.original.totalAchieved.toLocaleString()}
      </div>
    ),
  },
  {
    accessorKey: 'trend',
    header: () => <div className="w-full text-right">Trend</div>,
    cell: ({ row }) => {
      const trend = row.original.trend
      if (trend === null)
        return <div className="text-right text-muted-foreground">—</div>

      const isPositive = trend >= 0
      return (
        <div
          className={`flex items-center justify-end gap-1 ${
            isPositive
              ? 'text-green-600 dark:text-green-400'
              : 'text-red-600 dark:text-red-400'
          }`}
        >
          {isPositive ? (
            <IconTrendingUp className="size-4" />
          ) : (
            <IconTrendingDown className="size-4" />
          )}
          <span className="tabular-nums">
            {isPositive ? '+' : ''}
            {trend.toFixed(1)}%
          </span>
        </div>
      )
    },
  },
  {
    accessorKey: 'natemis',
    header: 'EMIS',
    cell: ({ row }) => (
      <div className="text-muted-foreground text-sm tabular-nums">
        {row.original.natemis}
      </div>
    ),
  },
  {
    id: 'actions',
    cell: () => (
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            className="data-[state=open]:bg-muted text-muted-foreground flex size-8"
            size="icon"
          >
            <IconDotsVertical />
            <span className="sr-only">Open menu</span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-32">
          <DropdownMenuItem>View Details</DropdownMenuItem>
          <DropdownMenuItem>Compare</DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem>Export</DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    ),
  },
]

function DraggableRow({ row }: { row: Row<SchoolPerformance> }) {
  const { transform, transition, setNodeRef, isDragging } = useSortable({
    id: row.original._id,
  })

  return (
    <TableRow
      data-state={row.getIsSelected() && 'selected'}
      data-dragging={isDragging}
      ref={setNodeRef}
      className="relative z-0 data-[dragging=true]:z-10 data-[dragging=true]:opacity-80"
      style={{
        transform: CSS.Transform.toString(transform),
        transition: transition,
      }}
    >
      {row.getVisibleCells().map((cell) => (
        <TableCell key={cell.id}>
          {flexRender(cell.column.columnDef.cell, cell.getContext())}
        </TableCell>
      ))}
    </TableRow>
  )
}

export function DataTable() {
  const schoolData = useQuery(api.myFunctions.getSchoolPerformance, {})

  const [data, setData] = React.useState<SchoolPerformance[]>([])
  const [rowSelection, setRowSelection] = React.useState({})
  const [columnVisibility, setColumnVisibility] =
    React.useState<VisibilityState>({})
  const [columnFilters, setColumnFilters] = React.useState<ColumnFiltersState>(
    [],
  )
  const [sorting, setSorting] = React.useState<SortingState>([])
  const [pagination, setPagination] = React.useState({
    pageIndex: 0,
    pageSize: 10,
  })
  const sortableId = React.useId()
  const sensors = useSensors(
    useSensor(MouseSensor, {}),
    useSensor(TouchSensor, {}),
    useSensor(KeyboardSensor, {}),
  )

  React.useEffect(() => {
    if (schoolData) {
      setData(schoolData)
    }
  }, [schoolData])

  const dataIds = React.useMemo<UniqueIdentifier[]>(
    () => data?.map(({ _id }) => _id) || [],
    [data],
  )

  const table = useReactTable({
    data,
    columns,
    state: {
      sorting,
      columnVisibility,
      rowSelection,
      columnFilters,
      pagination,
    },
    getRowId: (row) => row._id,
    enableRowSelection: true,
    onRowSelectionChange: setRowSelection,
    onSortingChange: setSorting,
    onColumnFiltersChange: setColumnFilters,
    onColumnVisibilityChange: setColumnVisibility,
    onPaginationChange: setPagination,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getFacetedRowModel: getFacetedRowModel(),
    getFacetedUniqueValues: getFacetedUniqueValues(),
  })

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event
    if (active && over && active.id !== over.id) {
      setData((data) => {
        const oldIndex = dataIds.indexOf(active.id)
        const newIndex = dataIds.indexOf(over.id)
        return arrayMove(data, oldIndex, newIndex)
      })
    }
  }

  if (!schoolData) {
    return (
      <div className="flex h-64 items-center justify-center">
        <div className="text-muted-foreground">Loading school data...</div>
      </div>
    )
  }

  return (
    <Tabs
      defaultValue="outline"
      className="w-full flex-col justify-start gap-6"
    >
      <div className="flex items-center justify-between px-4 lg:px-6">
        <Label htmlFor="view-selector" className="sr-only">
          View
        </Label>
        <Select defaultValue="outline">
          <SelectTrigger
            className="flex w-fit @4xl/main:hidden"
            size="sm"
            id="view-selector"
          >
            <SelectValue placeholder="Select a view" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="outline">Schools</SelectItem>
            <SelectItem value="past-performance">Past Performance</SelectItem>
            <SelectItem value="by-district">By District</SelectItem>
            <SelectItem value="by-province">By Province</SelectItem>
          </SelectContent>
        </Select>
        <TabsList className="**:data-[slot=badge]:bg-muted-foreground/30 hidden **:data-[slot=badge]:size-5 **:data-[slot=badge]:rounded-full **:data-[slot=badge]:px-1 @4xl/main:flex">
          <TabsTrigger value="outline">Schools</TabsTrigger>
          <TabsTrigger value="past-performance">
            Past Performance <Badge variant="secondary">3</Badge>
          </TabsTrigger>
          <TabsTrigger value="by-district">By District</TabsTrigger>
          <TabsTrigger value="by-province">By Province</TabsTrigger>
        </TabsList>
        <div className="flex items-center gap-2">
          <Input
            placeholder="Filter schools..."
            value={
              (table.getColumn('schoolName')?.getFilterValue() as string) ?? ''
            }
            onChange={(event) =>
              table.getColumn('schoolName')?.setFilterValue(event.target.value)
            }
            className="h-8 w-40 lg:w-64"
          />
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="outline" size="sm">
                <IconLayoutColumns />
                <span className="hidden lg:inline">Customize Columns</span>
                <span className="lg:hidden">Columns</span>
                <IconChevronDown />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-56">
              {table
                .getAllColumns()
                .filter(
                  (column) =>
                    typeof column.accessorFn !== 'undefined' &&
                    column.getCanHide(),
                )
                .map((column) => {
                  return (
                    <DropdownMenuCheckboxItem
                      key={column.id}
                      className="capitalize"
                      checked={column.getIsVisible()}
                      onCheckedChange={(value) =>
                        column.toggleVisibility(!!value)
                      }
                    >
                      {column.id}
                    </DropdownMenuCheckboxItem>
                  )
                })}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
      <TabsContent
        value="outline"
        className="relative flex flex-col gap-4 overflow-auto px-4 lg:px-6"
      >
        <div className="overflow-hidden rounded-lg border">
          <DndContext
            collisionDetection={closestCenter}
            modifiers={[restrictToVerticalAxis]}
            onDragEnd={handleDragEnd}
            sensors={sensors}
            id={sortableId}
          >
            <Table>
              <TableHeader className="bg-muted sticky top-0 z-10">
                {table.getHeaderGroups().map((headerGroup) => (
                  <TableRow key={headerGroup.id}>
                    {headerGroup.headers.map((header) => {
                      return (
                        <TableHead key={header.id} colSpan={header.colSpan}>
                          {header.isPlaceholder
                            ? null
                            : flexRender(
                                header.column.columnDef.header,
                                header.getContext(),
                              )}
                        </TableHead>
                      )
                    })}
                  </TableRow>
                ))}
              </TableHeader>
              <TableBody className="**:data-[slot=table-cell]:first:w-8">
                {table.getRowModel().rows?.length ? (
                  <SortableContext
                    items={dataIds}
                    strategy={verticalListSortingStrategy}
                  >
                    {table.getRowModel().rows.map((row) => (
                      <DraggableRow key={row.id} row={row} />
                    ))}
                  </SortableContext>
                ) : (
                  <TableRow>
                    <TableCell
                      colSpan={columns.length}
                      className="h-24 text-center"
                    >
                      No results.
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </DndContext>
        </div>
        <div className="flex items-center justify-between px-4">
          <div className="text-muted-foreground hidden flex-1 text-sm lg:flex">
            {table.getFilteredSelectedRowModel().rows.length} of{' '}
            {table.getFilteredRowModel().rows.length} row(s) selected.
          </div>
          <div className="flex w-full items-center gap-8 lg:w-fit">
            <div className="hidden items-center gap-2 lg:flex">
              <Label htmlFor="rows-per-page" className="text-sm font-medium">
                Rows per page
              </Label>
              <Select
                value={`${table.getState().pagination.pageSize}`}
                onValueChange={(value) => {
                  table.setPageSize(Number(value))
                }}
              >
                <SelectTrigger size="sm" className="w-20" id="rows-per-page">
                  <SelectValue
                    placeholder={table.getState().pagination.pageSize}
                  />
                </SelectTrigger>
                <SelectContent side="top">
                  {[10, 20, 30, 40, 50].map((pageSize) => (
                    <SelectItem key={pageSize} value={`${pageSize}`}>
                      {pageSize}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex w-fit items-center justify-center text-sm font-medium">
              Page {table.getState().pagination.pageIndex + 1} of{' '}
              {table.getPageCount()}
            </div>
            <div className="ml-auto flex items-center gap-2 lg:ml-0">
              <Button
                variant="outline"
                className="hidden h-8 w-8 p-0 lg:flex"
                onClick={() => table.setPageIndex(0)}
                disabled={!table.getCanPreviousPage()}
              >
                <span className="sr-only">Go to first page</span>
                <IconChevronsLeft />
              </Button>
              <Button
                variant="outline"
                className="size-8"
                size="icon"
                onClick={() => table.previousPage()}
                disabled={!table.getCanPreviousPage()}
              >
                <span className="sr-only">Go to previous page</span>
                <IconChevronLeft />
              </Button>
              <Button
                variant="outline"
                className="size-8"
                size="icon"
                onClick={() => table.nextPage()}
                disabled={!table.getCanNextPage()}
              >
                <span className="sr-only">Go to next page</span>
                <IconChevronRight />
              </Button>
              <Button
                variant="outline"
                className="hidden size-8 lg:flex"
                size="icon"
                onClick={() => table.setPageIndex(table.getPageCount() - 1)}
                disabled={!table.getCanNextPage()}
              >
                <span className="sr-only">Go to last page</span>
                <IconChevronsRight />
              </Button>
            </div>
          </div>
        </div>
      </TabsContent>
      <TabsContent
        value="past-performance"
        className="flex flex-col px-4 lg:px-6"
      >
        <div className="aspect-video w-full flex-1 rounded-lg border border-dashed"></div>
      </TabsContent>
      <TabsContent value="by-district" className="flex flex-col px-4 lg:px-6">
        <div className="aspect-video w-full flex-1 rounded-lg border border-dashed"></div>
      </TabsContent>
      <TabsContent value="by-province" className="flex flex-col px-4 lg:px-6">
        <div className="aspect-video w-full flex-1 rounded-lg border border-dashed"></div>
      </TabsContent>
    </Tabs>
  )
}

const chartConfig = {
  passRate: {
    label: 'Pass Rate',
    color: 'var(--primary)',
  },
} satisfies ChartConfig

function TableCellViewer({ item }: { item: SchoolPerformance }) {
  const isMobile = useIsMobile()

  const chartData = [
    { year: String(item.previousYear), passRate: item.passRatePrevious ?? 0 },
    { year: String(item.currentYear), passRate: item.passRateCurrent ?? 0 },
  ]

  return (
    <Drawer direction={isMobile ? 'bottom' : 'right'}>
      <DrawerTrigger asChild>
        <Button
          variant="link"
          className="text-foreground w-fit max-w-64 truncate px-0 text-left"
        >
          {item.schoolName}
        </Button>
      </DrawerTrigger>
      <DrawerContent>
        <DrawerHeader className="gap-1">
          <DrawerTitle>{item.schoolName}</DrawerTitle>
          <DrawerDescription>
            {item.district}, {item.province}
          </DrawerDescription>
        </DrawerHeader>
        <div className="flex flex-col gap-4 overflow-y-auto px-4 text-sm">
          {!isMobile && (
            <>
              <ChartContainer config={chartConfig}>
                <AreaChart
                  accessibilityLayer
                  data={chartData}
                  margin={{
                    left: 0,
                    right: 10,
                  }}
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
                  <Area
                    dataKey="passRate"
                    type="natural"
                    fill="var(--color-passRate)"
                    fillOpacity={0.4}
                    stroke="var(--color-passRate)"
                  />
                </AreaChart>
              </ChartContainer>
              <Separator />
              <div className="grid gap-2">
                <div className="flex gap-2 leading-none font-medium">
                  {item.trend !== null && (
                    <>
                      {item.trend >= 0 ? 'Improved' : 'Declined'} by{' '}
                      {Math.abs(item.trend).toFixed(1)}% from 2023
                      {item.trend >= 0 ? (
                        <IconTrendingUp className="size-4 text-green-500" />
                      ) : (
                        <IconTrendingDown className="size-4 text-red-500" />
                      )}
                    </>
                  )}
                </div>
                <div className="text-muted-foreground">
                  Performance comparison between 2023 and 2024 matric results.
                </div>
              </div>
              <Separator />
            </>
          )}
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1">
              <span className="text-muted-foreground text-xs">EMIS Number</span>
              <span className="font-medium">{item.natemis}</span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-muted-foreground text-xs">Quintile</span>
              <span className="font-medium">Q{item.quintile ?? 'N/A'}</span>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1">
              <span className="text-muted-foreground text-xs">
                Pass Rate {item.currentYear}
              </span>
              <span className="font-medium">
                {item.passRateCurrent != null
                  ? `${item.passRateCurrent.toFixed(1)}%`
                  : 'N/A'}
              </span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-muted-foreground text-xs">
                Pass Rate {item.previousYear}
              </span>
              <span className="font-medium">
                {item.passRatePrevious != null
                  ? `${item.passRatePrevious.toFixed(1)}%`
                  : 'N/A'}
              </span>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1">
              <span className="text-muted-foreground text-xs">Total Wrote</span>
              <span className="font-medium">
                {item.totalWrote.toLocaleString()}
              </span>
            </div>
            <div className="flex flex-col gap-1">
              <span className="text-muted-foreground text-xs">
                Total Achieved
              </span>
              <span className="font-medium">
                {item.totalAchieved.toLocaleString()}
              </span>
            </div>
          </div>
          <div className="flex flex-col gap-1">
            <span className="text-muted-foreground text-xs">Status</span>
            <Badge
              variant={getStatusBadgeVariant(item.status)}
              className="w-fit gap-1"
            >
              {getStatusIcon(item.status)}
              {item.status}
            </Badge>
          </div>
        </div>
        <DrawerFooter>
          <DrawerClose asChild>
            <Button variant="outline">Close</Button>
          </DrawerClose>
        </DrawerFooter>
      </DrawerContent>
    </Drawer>
  )
}
