import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import {
  type ColumnDef, flexRender, getCoreRowModel, getFilteredRowModel, getPaginationRowModel,
  getSortedRowModel, type SortingState, useReactTable,
} from '@tanstack/react-table'
import { ArrowUpDown, CheckCircle2, ChevronLeft, ChevronRight, Search, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { fetchJournal, type JournalEntry } from '@/lib/api'
import { MethodChip } from '@/components/ui/badges'
import { Button } from '@/components/ui/button'

function statusTone(status: number | null): string {
  if (status == null) return 'text-muted-foreground bg-muted border-border'
  if (status >= 500) return 'text-danger bg-danger-bg border-danger-border'
  if (status >= 400) return 'text-warning bg-warning-bg border-warning-border'
  return 'text-success bg-success-bg border-success-border'
}

export function JournalPage() {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const [unmatchedOnly, setUnmatchedOnly] = useState(false)
  const { data, isLoading } = useQuery({
    queryKey: ['journal', tenant, unmatchedOnly],
    queryFn: () => fetchJournal(tenant, unmatchedOnly),
    // Poll only when a host is actually answering; in sample mode (no host) fetch once and stop, so we
    // don't hammer a dead endpoint.
    refetchInterval: (query) => (query.state.data?.mock ? false : 5000),
  })

  const [globalFilter, setGlobalFilter] = useState('')
  const [sorting, setSorting] = useState<SortingState>([])

  const columns = useMemo<ColumnDef<JournalEntry>[]>(() => [
    { accessorKey: 'method', header: () => t('stubs.method'), cell: ({ getValue }) => <MethodChip method={getValue<string>()} /> },
    { accessorKey: 'url', header: () => t('stubs.url'), cell: ({ getValue }) => <span className="font-mono text-[12.5px]">{getValue<string>()}</span> },
    {
      accessorKey: 'status', header: () => t('journal.status'),
      cell: ({ getValue }) => {
        const s = getValue<number | null>()
        return <span className={cn('inline-flex rounded-md border px-2 py-0.5 font-mono text-[11px] font-bold', statusTone(s))}>{s ?? '—'}</span>
      },
    },
    {
      accessorKey: 'wasMatched', header: () => t('journal.result'),
      cell: ({ getValue }) => getValue<boolean>()
        ? <span className="inline-flex items-center gap-1.5 text-[12px] font-medium text-success"><CheckCircle2 className="size-3.5" />{t('journal.matched')}</span>
        : <span className="inline-flex items-center gap-1.5 text-[12px] font-medium text-warning"><XCircle className="size-3.5" />{t('journal.unmatched')}</span>,
    },
  ], [t])

  const table = useReactTable({
    data: data?.entries ?? [],
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    globalFilterFn: (row, _id, value) => row.original.url.toLowerCase().includes(String(value).toLowerCase()),
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 12 } },
  })

  return (
    <div className="mx-auto max-w-[1360px]">
      <div className="mb-6 inline-flex gap-1 rounded-xl bg-muted p-1">
        <button onClick={() => setUnmatchedOnly(false)} className={cn('rounded-lg px-3.5 py-1.5 text-sm font-semibold transition-colors', !unmatchedOnly ? 'bg-background text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground')}>{t('journal.allRequests')}</button>
        <button onClick={() => setUnmatchedOnly(true)} className={cn('rounded-lg px-3.5 py-1.5 text-sm font-semibold transition-colors', unmatchedOnly ? 'bg-background text-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground')}>{t('journal.unmatchedOnly')}</button>
      </div>

      <header className="mb-5">
        <h1 className="text-[22px] font-bold tracking-tight">{t('nav.journal')}</h1>
        <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('journal.subtitle')}</p>
      </header>

      <div className="overflow-hidden rounded-2xl border border-border bg-background shadow-surface">
        <div className="flex flex-wrap items-center gap-2 border-b border-border p-3">
          <label className="flex h-9 min-w-[220px] items-center gap-2 rounded-lg border border-border bg-muted/50 px-3">
            <Search className="size-4 text-muted-foreground" />
            <input value={globalFilter} onChange={(e) => setGlobalFilter(e.target.value)} placeholder={t('journal.filter')} className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground" />
          </label>
        </div>

        <div className="scroll-area overflow-x-auto">
          <table className="w-full min-w-[760px] border-collapse">
            <thead>
              {table.getHeaderGroups().map((hg) => (
                <tr key={hg.id}>
                  {hg.headers.map((h) => (
                    <th key={h.id} className="border-b border-border bg-muted/40 px-4 py-2.5 text-start text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                      <button onClick={h.column.getToggleSortingHandler()} className="inline-flex items-center gap-1.5 hover:text-foreground">
                        {flexRender(h.column.columnDef.header, h.getContext())}
                        <ArrowUpDown className="size-3" />
                      </button>
                    </th>
                  ))}
                </tr>
              ))}
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 6 }).map((_, i) => (
                  <tr key={i}><td colSpan={columns.length} className="px-4 py-3.5"><div className="h-4 w-full animate-pulse rounded bg-muted" /></td></tr>
                ))
              ) : table.getRowModel().rows.length === 0 ? (
                <tr><td colSpan={columns.length} className="px-4 py-16 text-center text-sm text-muted-foreground">{t('journal.empty')}</td></tr>
              ) : (
                table.getRowModel().rows.map((row) => (
                  <tr key={row.id} className="border-b border-border transition-colors hover:bg-muted/40">
                    {row.getVisibleCells().map((cell) => (
                      <td key={cell.id} className="px-4 py-3 align-middle">{flexRender(cell.column.columnDef.cell, cell.getContext())}</td>
                    ))}
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="flex flex-wrap items-center gap-3 border-t border-border bg-muted/30 px-4 py-3 text-[12.5px] text-muted-foreground">
          {data?.mock && (
            <span className="inline-flex items-center gap-1.5 rounded-full border border-warning-border bg-warning-bg px-2.5 py-0.5 text-[11.5px] font-medium text-warning">{t('stubs.sample')}</span>
          )}
          <span>{t('stubs.showing')} <b className="tabular-nums">{table.getRowModel().rows.length}</b> {t('stubs.of')} <b className="tabular-nums">{data?.entries.length ?? 0}</b></span>
          <div className="ms-auto flex items-center gap-1.5">
            <Button variant="outline" size="iconSm" onClick={() => table.previousPage()} disabled={!table.getCanPreviousPage()} aria-label="Previous"><ChevronLeft className="rtl:rotate-180" /></Button>
            <span className="px-1 tabular-nums">{table.getState().pagination.pageIndex + 1} / {Math.max(1, table.getPageCount())}</span>
            <Button variant="outline" size="iconSm" onClick={() => table.nextPage()} disabled={!table.getCanNextPage()} aria-label="Next"><ChevronRight className="rtl:rotate-180" /></Button>
          </div>
        </div>
      </div>
    </div>
  )
}
