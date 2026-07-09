import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ChevronRight, Download, Import, Plus, Trash2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { deleteStub, fetchStubs, type Stub } from '@/lib/api'
import { buildStubTree, countLeaves, type StubTreeNode } from '@/lib/stub-tree'
import { MethodChip } from '@/components/ui/badges'
import { Button } from '@/components/ui/button'
import { FacetFilter } from '@/components/ui/facet-filter'
import { SearchBox } from '@/components/ui/search-box'
import { applyFilters, clearFacet, countSelected, type FacetDef, facetOptions, type Selections, toggleSelection } from '@/lib/faceted'
import { StubEditorForm } from '@/components/stubs/stub-editor'

const EMPTY_SET = new Set<string>()
const FACETS: FacetDef<Stub>[] = [
  { id: 'method', get: (s) => s.method },
  { id: 'status', get: (s) => s.status },
]

interface Tab { key: string; kind: 'stub' | 'new' | 'import'; stubId?: string; initial: 'form' | 'json' }

export function StubsPage() {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const queryClient = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: ['stubs', tenant], queryFn: () => fetchStubs(tenant) })
  const stubs = useMemo(() => data?.stubs ?? [], [data])

  const refresh = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: ['stubs', tenant] })
    void queryClient.invalidateQueries({ queryKey: ['scenarios', tenant] })
  }, [queryClient, tenant])

  // Search + method/status facets narrow the tree.
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<Selections>({})
  const filtering = search.trim().length > 0 || countSelected(selected) > 0
  const filtered = useMemo(() => applyFilters(stubs, FACETS, selected, search, (s) => `${s.name ?? ''} ${s.url}`), [stubs, selected, search])
  const tree = useMemo(() => buildStubTree(filtered), [filtered])
  const methodOptions = useMemo(() => facetOptions(stubs, (s) => s.method), [stubs])
  const statusOptions = useMemo(() => facetOptions(stubs, (s) => s.status), [stubs])

  // Open tabs — persistent (localStorage) per tenant. Stub tabs restore across reloads; new/import tabs
  // are ephemeral. Every open tab's editor stays mounted (hidden when inactive), so unsaved edits
  // survive switching tabs.
  const storageKey = `ui.stubTabs.${tenant}`
  const [tabs, setTabs] = useState<Tab[]>([])
  const [active, setActive] = useState<string>('')
  const [dirty, setDirty] = useState<Record<string, boolean>>({})
  const restored = useRef('')

  useEffect(() => {
    if (isLoading || restored.current === tenant) return
    restored.current = tenant
    try {
      const saved = JSON.parse(localStorage.getItem(storageKey) ?? 'null') as { ids?: string[]; active?: string } | null
      const ids = (saved?.ids ?? []).filter((id) => stubs.some((s) => s.id === id))
      const restoredTabs = ids.map<Tab>((id) => ({ key: `stub:${id}`, kind: 'stub', stubId: id, initial: 'form' }))
      setTabs(restoredTabs)
      setActive(restoredTabs.some((x) => x.key === saved?.active) ? saved!.active! : restoredTabs[0]?.key ?? '')
    } catch { /* start clean */ }
  }, [isLoading, tenant, stubs, storageKey])

  useEffect(() => {
    const ids = tabs.filter((x) => x.kind === 'stub' && x.stubId).map((x) => x.stubId!)
    localStorage.setItem(storageKey, JSON.stringify({ ids, active }))
  }, [tabs, active, storageKey])

  const openStub = useCallback((stub: Stub) => {
    const key = `stub:${stub.id}`
    setTabs((prev) => (prev.some((x) => x.key === key) ? prev : [...prev, { key, kind: 'stub', stubId: stub.id, initial: 'form' }]))
    setActive(key)
  }, [])

  const openBlank = useCallback((initial: 'form' | 'json') => {
    const key = `${initial === 'json' ? 'import' : 'new'}:${tabs.length}-${active}`
    setTabs((prev) => [...prev, { key, kind: initial === 'json' ? 'import' : 'new', initial }])
    setActive(key)
  }, [tabs.length, active])

  const closeTab = useCallback((key: string) => {
    setTabs((prev) => {
      const idx = prev.findIndex((x) => x.key === key)
      const next = prev.filter((x) => x.key !== key)
      setActive((cur) => (cur !== key ? cur : next[Math.max(0, idx - 1)]?.key ?? ''))
      return next
    })
    setDirty((d) => { const { [key]: _, ...rest } = d; return rest })
  }, [])

  const [searchParams, setSearchParams] = useSearchParams()
  useEffect(() => {
    if (searchParams.get('new') === '1') { openBlank('form'); setSearchParams({}, { replace: true }) }
    else if (searchParams.get('import') === '1') { openBlank('json'); setSearchParams({}, { replace: true }) }
  }, [searchParams, setSearchParams, openBlank])

  const exportAll = useCallback(() => {
    const mappings = stubs.map((s) => s.raw).filter(Boolean)
    const blob = new Blob([JSON.stringify({ mappings }, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `mockifyr-${tenant}-stubs.json`
    a.click()
    URL.revokeObjectURL(url)
  }, [stubs, tenant])

  const remove = useCallback(async (stub: Stub) => {
    const { mock } = await deleteStub(tenant, stub.id)
    toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('editor.deleted'))
    closeTab(`stub:${stub.id}`)
    refresh()
  }, [tenant, refresh, t, closeTab])

  const onTabSaved = useCallback((tab: Tab, saved: boolean) => {
    refresh()
    if (saved && tab.kind !== 'stub') closeTab(tab.key)
    else setDirty((d) => ({ ...d, [tab.key]: false }))
  }, [refresh, closeTab])

  const empty = !isLoading && stubs.length === 0

  return (
    <div className="flex h-full min-h-0 gap-4">
      {/* Tree panel */}
      <aside className="flex w-[272px] shrink-0 flex-col overflow-hidden rounded-2xl border border-border bg-background shadow-surface">
        <div className="flex flex-col gap-2.5 border-b border-border p-3">
          <div className="flex items-center gap-2">
            <h1 className="text-[15px] font-bold">{t('nav.stubs')}</h1>
            <span className="rounded-full bg-muted px-1.5 text-[11px] tabular-nums text-muted-foreground">{stubs.length}</span>
            <div className="ms-auto flex gap-1">
              <Button variant="ghost" size="iconSm" aria-label={t('stubs.export')} onClick={exportAll} disabled={!stubs.length}><Download /></Button>
              <Button variant="ghost" size="iconSm" aria-label={t('stubs.import')} onClick={() => openBlank('json')}><Import /></Button>
              <Button variant="primary" size="iconSm" aria-label={t('stubs.newStub')} onClick={() => openBlank('form')}><Plus /></Button>
            </div>
          </div>
          <SearchBox value={search} onCommit={setSearch} placeholder={t('stubs.filter')} />
          <div className="flex gap-1.5">
            <FacetFilter label={t('stubs.method')} options={methodOptions} selected={selected.method ?? EMPTY_SET}
              onToggle={(v) => setSelected((s) => toggleSelection(s, 'method', v))} onClear={() => setSelected((s) => clearFacet(s, 'method'))} clearLabel={t('common.clear')} />
            <FacetFilter label={t('stubs.status')} options={statusOptions} selected={selected.status ?? EMPTY_SET}
              onToggle={(v) => setSelected((s) => toggleSelection(s, 'status', v))} onClear={() => setSelected((s) => clearFacet(s, 'status'))} clearLabel={t('common.clear')} />
          </div>
        </div>
        <div className="scroll-area min-h-0 flex-1 overflow-y-auto p-1.5">
          {isLoading ? (
            <div className="space-y-2 p-2">{Array.from({ length: 6 }).map((_, i) => <div key={i} className="h-6 animate-pulse rounded bg-muted" />)}</div>
          ) : filtered.length === 0 ? (
            <p className="p-3 text-sm text-faint">{filtering ? t('stubs.empty') : t('dashboard.getStarted')}</p>
          ) : (
            <TreeView node={tree} depth={0} path="" forceOpen={filtering} activeStubId={tabs.find((x) => x.key === active)?.stubId} onOpen={openStub} onDelete={remove} />
          )}
        </div>
        {data?.mock && <div className="border-t border-border p-2 text-center"><span className="rounded-full border border-warning-border bg-warning-bg px-2 py-0.5 text-[11px] font-medium text-warning">{t('stubs.sample')}</span></div>}
      </aside>

      {/* Workspace */}
      <section className="flex min-w-0 flex-1 flex-col overflow-hidden rounded-2xl border border-border bg-background shadow-surface">
        {tabs.length === 0 ? (
          <EmptyWorkspace t={t} empty={empty} onNew={() => openBlank('form')} onImport={() => openBlank('json')} />
        ) : (
          <>
            <div className="flex items-stretch overflow-x-auto border-b border-border bg-muted/30">
              {tabs.map((tab) => {
                const stub = tab.stubId ? stubs.find((s) => s.id === tab.stubId) : null
                const label = stub ? (stub.name || stub.url) : tab.kind === 'import' ? t('editor.importTitle') : t('editor.newTitle')
                return (
                  <button key={tab.key} onClick={() => setActive(tab.key)}
                    className={cn('group flex max-w-[220px] shrink-0 items-center gap-2 border-e border-border px-3 py-2 text-[12.5px] transition-colors',
                      active === tab.key ? 'border-b-2 border-b-primary bg-background text-foreground' : 'text-muted-foreground hover:text-foreground')}>
                    {stub && <MethodChip method={stub.method} />}
                    <span className="truncate">{label}</span>
                    {dirty[tab.key] && <span className="size-1.5 shrink-0 rounded-full bg-primary" aria-label="unsaved" />}
                    <span role="button" tabIndex={-1} aria-label={t('editor.cancel')} onClick={(e) => { e.stopPropagation(); closeTab(tab.key) }}
                      className="rounded p-0.5 text-faint hover:bg-muted hover:text-foreground"><X className="size-3.5" /></span>
                  </button>
                )
              })}
              <button onClick={() => openBlank('form')} aria-label={t('stubs.newStub')} className="shrink-0 px-3 text-muted-foreground hover:text-foreground"><Plus className="size-4" /></button>
            </div>
            {tabs.map((tab) => (
              <div key={tab.key} className={cn('min-h-0 flex-1', active === tab.key ? 'flex flex-col' : 'hidden')}>
                <StubEditorForm
                  editing={tab.stubId ? stubs.find((s) => s.id === tab.stubId) ?? null : null}
                  initialTab={tab.initial}
                  onSaved={(saved) => onTabSaved(tab, saved)}
                  onDirtyChange={(d) => setDirty((prev) => (prev[tab.key] === d ? prev : { ...prev, [tab.key]: d }))}
                />
              </div>
            ))}
          </>
        )}
      </section>
    </div>
  )
}

function TreeView({ node, depth, path, forceOpen, activeStubId, onOpen, onDelete }: {
  node: StubTreeNode
  depth: number
  path: string
  forceOpen: boolean
  activeStubId?: string
  onOpen: (s: Stub) => void
  onDelete: (s: Stub) => void
}) {
  const groups = [...node.groups.entries()].sort((a, b) => a[0].localeCompare(b[0]))
  const leaves = [...node.leaves].sort((a, b) => a.label.localeCompare(b.label))
  return (
    <div>
      {groups.map(([seg, child]) => (
        <Group key={seg} seg={seg} child={child} depth={depth} path={`${path}/${seg}`} forceOpen={forceOpen} activeStubId={activeStubId} onOpen={onOpen} onDelete={onDelete} />
      ))}
      {leaves.map(({ label, stub }) => (
        <div key={stub.id} style={{ paddingInlineStart: depth * 14 + 8 }}
          onClick={() => onOpen(stub)}
          className={cn('group flex cursor-pointer items-center gap-2 rounded-lg py-1.5 pe-1.5 text-[12.5px] transition-colors',
            stub.id === activeStubId ? 'bg-muted text-foreground' : 'text-foreground hover:bg-muted/60')}>
          <MethodChip method={stub.method} />
          <span className="min-w-0 flex-1 truncate">{label}</span>
          <span role="button" tabIndex={-1} aria-label="Delete" onClick={(e) => { e.stopPropagation(); onDelete(stub) }}
            className="rounded p-0.5 text-faint opacity-0 transition-opacity hover:bg-danger-bg hover:text-danger group-hover:opacity-100"><Trash2 className="size-3.5" /></span>
        </div>
      ))}
    </div>
  )
}

function Group({ seg, child, depth, path, forceOpen, activeStubId, onOpen, onDelete }: {
  seg: string
  child: StubTreeNode
  depth: number
  path: string
  forceOpen: boolean
  activeStubId?: string
  onOpen: (s: Stub) => void
  onDelete: (s: Stub) => void
}) {
  const [open, setOpen] = useState(true)
  const expanded = forceOpen || open
  return (
    <div>
      <div style={{ paddingInlineStart: depth * 14 + 4 }}
        onClick={() => setOpen((o) => !o)}
        className="flex cursor-pointer items-center gap-1.5 rounded-lg py-1.5 pe-1.5 text-[12.5px] text-muted-foreground transition-colors hover:bg-muted/60">
        <ChevronRight className={cn('size-3.5 shrink-0 transition-transform', expanded && 'rotate-90')} />
        <span className="min-w-0 flex-1 truncate font-medium">/{seg}</span>
        <span className="text-[11px] tabular-nums text-faint">{countLeaves(child)}</span>
      </div>
      {expanded && <TreeView node={child} depth={depth + 1} path={path} forceOpen={forceOpen} activeStubId={activeStubId} onOpen={onOpen} onDelete={onDelete} />}
    </div>
  )
}

function EmptyWorkspace({ t, empty, onNew, onImport }: { t: (k: string) => string; empty: boolean; onNew: () => void; onImport: () => void }) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-3 p-8 text-center">
      <h2 className="text-base font-semibold">{empty ? t('dashboard.getStarted') : t('stubs.pickHint')}</h2>
      <p className="max-w-[42ch] text-sm text-muted-foreground">{empty ? t('dashboard.getStartedHint') : t('stubs.pickHintBody')}</p>
      <div className="flex gap-2">
        <Button variant="primary" size="sm" onClick={onNew}><Plus />{t('stubs.newStub')}</Button>
        <Button variant="outline" size="sm" onClick={onImport}><Download />{t('stubs.import')}</Button>
      </div>
    </div>
  )
}
