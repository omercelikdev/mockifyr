import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import {
  type ColumnDef, flexRender, getCoreRowModel, getFilteredRowModel, getPaginationRowModel,
  getSortedRowModel, type SortingState, useReactTable,
} from '@tanstack/react-table'
import {
  ArrowUpDown, ChevronLeft, ChevronRight, Import, MoreHorizontal, Plus, Rows2, Rows3, Search, Trash2,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { fetchStubs, type Protocol, type Stub } from '@/lib/api'
import { MethodChip, StatusPill } from '@/components/ui/badges'
import { Button } from '@/components/ui/button'

const PROTOCOLS: { key: 'all' | Protocol; label: string }[] = [
  { key: 'all', label: 'all' }, { key: 'http', label: 'HTTP' }, { key: 'grpc', label: 'gRPC' },
  { key: 'graphql', label: 'GraphQL' }, { key: 'websocket', label: 'WebSocket' },
]

export function StubsPage() {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const { data, isLoading } = useQuery({ queryKey: ['stubs', tenant], queryFn: () => fetchStubs(tenant) })

  const [proto, setProto] = useState<'all' | Protocol>('all')
  const [globalFilter, setGlobalFilter] = useState('')
  const [sorting, setSorting] = useState<SortingState>([])
  const [rowSelection, setRowSelection] = useState({})
  const [dense, setDense] = useState(false)

  const stubs = useMemo(
    () => (data?.stubs ?? []).filter((s) => proto === 'all' || s.protocol === proto),
    [data, proto],
  )

  const columns = useMemo<ColumnDef<Stub>[]>(() => [
    {
      id: 'select',
      header: ({ table }) => (
        <input type="checkbox" className="size-3.5 accent-[var(--primary)]"
          checked={table.getIsAllRowsSelected()} onChange={table.getToggleAllRowsSelectedHandler()} />
      ),
      cell: ({ row }) => (
        <input type="checkbox" className="size-3.5 accent-[var(--primary)]"
          checked={row.getIsSelected()} onChange={row.getToggleSelectedHandler()} />
      ),
      enableSorting: false,
      size: 36,
    },
    { accessorKey: 'method', header: () => t('stubs.method'), cell: ({ getValue }) => <MethodChip method={getValue<string>()} /> },
    { accessorKey: 'url', header: () => t('stubs.url'), cell: ({ getValue }) => <span className="font-mono text-[12.5px]">{getValue<string>()}</span> },
    { accessorKey: 'priority', header: () => t('stubs.priority'), cell: ({ getValue }) => <span className="tabular-nums text-muted-foreground">{getValue<number>()}</span> },
    { accessorKey: 'scenario', header: () => t('stubs.scenario'), cell: ({ getValue }) => getValue<string | null>() ?? <span className="text-muted-foreground">—</span> },
    { accessorKey: 'persistence', header: () => t('stubs.persistence'), cell: ({ getValue }) => <span className="text-muted-foreground">{getValue<string>()}</span> },
    { accessorKey: 'lastMatched', header: () => t('stubs.lastMatched'), cell: ({ getValue }) => <span className="tabular-nums text-muted-foreground">{getValue<string | null>() ?? t('stubs.never')}</span> },
    { accessorKey: 'status', header: () => t('stubs.status'), cell: ({ getValue }) => { const s = getValue<Stub['status']>(); return <StatusPill status={s} label={t(`status.${s}`)} /> } },
    {
      id: 'actions',
      header: () => null,
      cell: () => <Button variant="ghost" size="iconSm" aria-label="Actions"><MoreHorizontal /></Button>,
      enableSorting: false,
      size: 44,
    },
  ], [t])

  const table = useReactTable({
    data: stubs,
    columns,
    state: { sorting, globalFilter, rowSelection },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    onRowSelectionChange: setRowSelection,
    globalFilterFn: (row, _id, value) => row.original.url.toLowerCase().includes(String(value).toLowerCase()),
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 8 } },
  })

  const selectedCount = Object.keys(rowSelection).length
  const pad = dense ? 'py-2' : 'py-3'

  return (
    <div className="mx-auto max-w-[1360px]">
      {/* Protocol segmented tabs */}
      <div className="mb-6 inline-flex gap-1 rounded-xl bg-muted p-1">
        {PROTOCOLS.map((p) => (
          <button key={p.key} onClick={() => setProto(p.key)}
            className={cn('rounded-lg px-3.5 py-1.5 text-sm font-semibold transition-colors',
              proto === p.key ? 'bg-background text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground')}>
            {p.key === 'all' ? t('stubs.all') : p.label}
          </button>
        ))}
      </div>

      <header className="mb-5 flex items-start gap-4">
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">{t('nav.stubs')}</h1>
          <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('stubs.subtitle')}</p>
        </div>
        <div className="ms-auto flex gap-2">
          <Button variant="outline"><Import />{t('stubs.import')}</Button>
          <Button variant="primary"><Plus />{t('stubs.newStub')}</Button>
        </div>
      </header>

      <div className="overflow-hidden rounded-2xl border border-border bg-background shadow-surface">
        {/* Toolbar */}
        <div className="flex flex-wrap items-center gap-2 border-b border-border p-3">
          {selectedCount > 0 ? (
            <>
              <span className="ps-1 text-sm font-medium">{t('stubs.selected', { count: selectedCount })}</span>
              <Button variant="outline" size="sm" className="text-danger"><Trash2 />{t('stubs.delete')}</Button>
            </>
          ) : (
            <label className="flex h-9 min-w-[220px] items-center gap-2 rounded-lg border border-border bg-muted/50 px-3">
              <Search className="size-4 text-muted-foreground" />
              <input value={globalFilter} onChange={(e) => setGlobalFilter(e.target.value)} placeholder={t('stubs.filter')}
                className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground" />
            </label>
          )}
          <Button variant="outline" size="sm" className="ms-auto" onClick={() => setDense((d) => !d)}>
            {dense ? <Rows3 /> : <Rows2 />}{t('stubs.density')}
          </Button>
        </div>

        {/* Table */}
        <div className="scroll-area overflow-x-auto">
          <table className="w-full min-w-[900px] border-collapse">
            <thead>
              {table.getHeaderGroups().map((hg) => (
                <tr key={hg.id}>
                  {hg.headers.map((h) => (
                    <th key={h.id} style={{ width: h.getSize() !== 150 ? h.getSize() : undefined }}
                      className="border-b border-border bg-muted/40 px-4 py-2.5 text-start text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                      {h.column.getCanSort() ? (
                        <button onClick={h.column.getToggleSortingHandler()} className="inline-flex items-center gap-1.5 hover:text-foreground">
                          {flexRender(h.column.columnDef.header, h.getContext())}
                          <ArrowUpDown className="size-3" />
                        </button>
                      ) : (
                        flexRender(h.column.columnDef.header, h.getContext())
                      )}
                    </th>
                  ))}
                </tr>
              ))}
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 6 }).map((_, i) => (
                  <tr key={i}>
                    <td colSpan={columns.length} className="px-4 py-3.5">
                      <div className="h-4 w-full animate-pulse rounded bg-muted" />
                    </td>
                  </tr>
                ))
              ) : table.getRowModel().rows.length === 0 ? (
                <tr>
                  <td colSpan={columns.length} className="px-4 py-16 text-center text-sm text-muted-foreground">{t('stubs.empty')}</td>
                </tr>
              ) : (
                table.getRowModel().rows.map((row) => (
                  <tr key={row.id} className={cn('border-b border-border transition-colors hover:bg-muted/40', row.getIsSelected() && 'bg-muted/40')}>
                    {row.getVisibleCells().map((cell) => (
                      <td key={cell.id} className={cn('px-4 align-middle', pad)}>{flexRender(cell.column.columnDef.cell, cell.getContext())}</td>
                    ))}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Footer: sample hint + pagination */}
        <div className="flex flex-wrap items-center gap-3 border-t border-border bg-muted/30 px-4 py-3 text-[12.5px] text-muted-foreground">
          {data?.mock && (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-warning-border bg-warning-bg px-2.5 py-0.5 text-[11.5px] font-medium text-warning">
              {t('stubs.sample')}
            </span>
          )}
          <span>
            {t('stubs.showing')} <b className="tabular-nums">{table.getRowModel().rows.length}</b> {t('stubs.of')}{' '}
            <b className="tabular-nums">{stubs.length}</b>
          </span>
          <div className="ms-auto flex items-center gap-1.5">
            <Button variant="outline" size="iconSm" onClick={() => table.previousPage()} disabled={!table.getCanPreviousPage()} aria-label="Previous">
              <ChevronLeft className="rtl:rotate-180" />
            </Button>
            <span className="px-1 tabular-nums">
              {table.getState().pagination.pageIndex + 1} / {Math.max(1, table.getPageCount())}
            </span>
            <Button variant="outline" size="iconSm" onClick={() => table.nextPage()} disabled={!table.getCanNextPage()} aria-label="Next">
              <ChevronRight className="rtl:rotate-180" />
            </Button>
          </div>
        </div>
      </div>
    </div>
  )
}
