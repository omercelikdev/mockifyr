import { useCallback, useEffect, useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  type ColumnDef, flexRender, getCoreRowModel, getPaginationRowModel,
  getSortedRowModel, type SortingState, useReactTable,
} from '@tanstack/react-table'
import {
  ArrowUpDown, ChevronLeft, ChevronRight, Download, Import, MoreHorizontal, Pencil, Plus, Rows2, Rows3, Trash2,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { deleteStub, fetchStubs, type Protocol, type Stub } from '@/lib/api'
import { MethodChip, StatusPill } from '@/components/ui/badges'
import { Button } from '@/components/ui/button'
import { FacetFilter } from '@/components/ui/facet-filter'
import { SearchBox } from '@/components/ui/search-box'
import {
  applyFilters, clearFacet, countSelected, type FacetDef, facetOptions, type Selections, toggleSelection,
} from '@/lib/faceted'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { StubEditor } from '@/components/stubs/stub-editor'

const PROTOCOLS: { key: 'all' | Protocol; label: string }[] = [
  { key: 'all', label: 'all' }, { key: 'http', label: 'HTTP' }, { key: 'grpc', label: 'gRPC' },
  { key: 'graphql', label: 'GraphQL' }, { key: 'websocket', label: 'WebSocket' },
]

const EMPTY_SET = new Set<string>()
const FACETS: FacetDef<Stub>[] = [
  { id: 'method', get: (s) => s.method },
  { id: 'status', get: (s) => s.status },
  { id: 'persistence', get: (s) => s.persistence },
]

export function StubsPage() {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const queryClient = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: ['stubs', tenant], queryFn: () => fetchStubs(tenant) })

  const [searchParams, setSearchParams] = useSearchParams()
  const [editorOpen, setEditorOpen] = useState(false)
  const [editing, setEditing] = useState<Stub | null>(null)
  const [editorTab, setEditorTab] = useState<'form' | 'json'>('form')
  // A stub mutation can also add or remove a scenario (via scenarioName), so refresh both the stubs
  // and the scenarios queries — otherwise the sidebar's Scenarios count and the Scenarios page stay
  // stale until a manual reload.
  const refresh = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ['stubs', tenant] })
    void queryClient.invalidateQueries({ queryKey: ['scenarios', tenant] })
  }, [queryClient, tenant])

  // Deep-link: ?new=1 opens a blank editor, ?import=1 opens it on the JSON tab (from the command
  // palette or the dashboard's quick actions). Consumed once, then the query is cleared.
  useEffect(() => {
    if (searchParams.get('new') === '1') { setEditing(null); setEditorTab('form'); setEditorOpen(true); setSearchParams({}, { replace: true }) }
    else if (searchParams.get('import') === '1') { setEditing(null); setEditorTab('json'); setEditorOpen(true); setSearchParams({}, { replace: true }) }
  }, [searchParams, setSearchParams])

  const openNew = useCallback(() => { setEditing(null); setEditorTab('form'); setEditorOpen(true) }, [])
  const openImport = useCallback(() => { setEditing(null); setEditorTab('json'); setEditorOpen(true) }, [])

  // Export the tenant's stubs as a {"mappings":[…]} bundle — the same shape Import accepts,
  // so it round-trips and is portable across tools using the standard mapping format. Uses the full raw mappings the host returned.
  const exportAll = useCallback(() => {
    const mappings = (data?.stubs ?? []).map((s) => s.raw).filter(Boolean)
    const blob = new Blob([JSON.stringify({ mappings }, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `mockifyr-${tenant}-stubs.json`
    a.click()
    URL.revokeObjectURL(url)
  }, [data, tenant])
  const openEdit = useCallback((stub: Stub) => { setEditing(stub); setEditorTab('form'); setEditorOpen(true) }, [])
  const remove = useCallback(async (stub: Stub) => {
    const { mock } = await deleteStub(tenant, stub.id)
    toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('editor.deleted'))
    refresh()
  }, [tenant, refresh, t])

  const [proto, setProto] = useState<'all' | Protocol>('all')
  const [selected, setSelected] = useState<Selections>({})
  const [search, setSearch] = useState('')
  const [sorting, setSorting] = useState<SortingState>([])
  const [rowSelection, setRowSelection] = useState({})
  const [dense, setDense] = useState(false)

  // Protocol (segmented) narrows first; facets + search then filter that set on the client.
  const stubs = useMemo(
    () => (data?.stubs ?? []).filter((s) => proto === 'all' || s.protocol === proto),
    [data, proto],
  )
  const methodOptions = useMemo(() => facetOptions(stubs, (s) => s.method), [stubs])
  const statusOptions = useMemo(() => facetOptions(stubs, (s) => s.status), [stubs])
  const persistenceOptions = useMemo(() => facetOptions(stubs, (s) => s.persistence), [stubs])
  const filtered = useMemo(() => applyFilters(stubs, FACETS, selected, search, (s) => s.url), [stubs, selected, search])

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
      cell: ({ row }) => (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="iconSm" aria-label="Actions"><MoreHorizontal /></Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="min-w-40">
            <DropdownMenuItem onSelect={() => openEdit(row.original)}><Pencil className="size-4 text-muted-foreground" />{t('stubs.edit')}</DropdownMenuItem>
            <DropdownMenuItem onSelect={() => remove(row.original)} className="text-danger"><Trash2 className="size-4" />{t('stubs.delete')}</DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
      enableSorting: false,
      size: 44,
    },
  ], [t, openEdit, remove])

  const table = useReactTable({
    data: filtered,
    columns,
    state: { sorting, rowSelection },
    onSortingChange: setSorting,
    onRowSelectionChange: setRowSelection,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    autoResetPageIndex: false,
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
          <Button variant="outline" className="min-w-[116px]" onClick={exportAll} disabled={!data?.stubs.length}><Download />{t('stubs.export')}</Button>
          <Button variant="outline" className="min-w-[116px]" onClick={openImport}><Import />{t('stubs.import')}</Button>
          <Button variant="primary" className="min-w-[116px]" onClick={openNew}><Plus />{t('stubs.newStub')}</Button>
        </div>
      </header>

      <div className="overflow-hidden rounded-2xl border border-border bg-background shadow-surface">
        {/* Toolbar */}
        <div className="flex flex-wrap items-center gap-2 border-b border-border p-3">
          {selectedCount > 0 ? (
            <>
              <span className="ps-1 text-sm font-medium">{t('stubs.selected', { count: selectedCount })}</span>
              <Button variant="outline" className="text-danger"
                onClick={async () => {
                  const rows = table.getSelectedRowModel().rows
                  await Promise.all(rows.map((r) => deleteStub(tenant, r.original.id)))
                  toast.success(t('editor.deleted'))
                  setRowSelection({})
                  refresh()
                }}>
                <Trash2 />{t('stubs.delete')}
              </Button>
            </>
          ) : (
            <>
              <SearchBox value={search} onCommit={setSearch} placeholder={t('stubs.filter')} />
              <FacetFilter label={t('stubs.method')} options={methodOptions} selected={selected.method ?? EMPTY_SET}
                onToggle={(v) => setSelected((s) => toggleSelection(s, 'method', v))} onClear={() => setSelected((s) => clearFacet(s, 'method'))} clearLabel={t('common.clear')} />
              <FacetFilter label={t('stubs.status')} options={statusOptions} selected={selected.status ?? EMPTY_SET}
                onToggle={(v) => setSelected((s) => toggleSelection(s, 'status', v))} onClear={() => setSelected((s) => clearFacet(s, 'status'))} clearLabel={t('common.clear')} />
              <FacetFilter label={t('stubs.persistence')} options={persistenceOptions} selected={selected.persistence ?? EMPTY_SET}
                onToggle={(v) => setSelected((s) => toggleSelection(s, 'persistence', v))} onClear={() => setSelected((s) => clearFacet(s, 'persistence'))} clearLabel={t('common.clear')} />
              {countSelected(selected) > 0 && (
                <Button variant="ghost" size="sm" onClick={() => setSelected({})}>{t('common.clear')}</Button>
              )}
            </>
          )}
          <Button variant="outline" className="ms-auto" onClick={() => setDense((d) => !d)}>
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
            <b className="tabular-nums">{filtered.length}</b>
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

      <StubEditor open={editorOpen} onOpenChange={setEditorOpen} editing={editing} onSaved={refresh} initialTab={editorTab} />
    </div>
  )
}
