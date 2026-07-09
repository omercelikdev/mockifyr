import { useEffect, useMemo, useState } from 'react'
import * as Dialog from '@radix-ui/react-dialog'
import { useTranslation } from 'react-i18next'
import { BookOpen, Search, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { TEMPLATING_HELPERS, type Helper } from '@/lib/templating-helpers'

// The popup is a single app-wide instance opened via this event, so any surface (the editor button, the
// ⌘K command palette, …) can raise it without threading state through the tree.
const OPEN_EVENT = 'open-helpers'
export const openHelpers = () => window.dispatchEvent(new Event(OPEN_EVENT))

/** The trigger button used in the stub editor's Response section. */
export function HelpersButton({ className }: { className?: string }) {
  const { t } = useTranslation()
  return (
    <button type="button" onClick={openHelpers} className={cn('inline-flex items-center gap-1.5 rounded-lg border border-border bg-background px-2.5 py-1.5 text-[13px] font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground', className)}>
      <BookOpen className="size-3.5" />{t('editor.helpers')}
    </button>
  )
}

function HelperCard({ h }: { h: Helper }) {
  return (
    <div className="rounded-xl border border-border bg-background p-3.5">
      <div className="flex items-center justify-between gap-2">
        <span className="font-mono text-[12.5px] font-semibold text-foreground">{h.name}</span>
      </div>
      <p className="mt-1 text-[12.5px] text-muted-foreground">{h.desc}</p>
      <pre className="mt-2 overflow-x-auto rounded-lg border border-border bg-muted/50 px-2.5 py-1.5 font-mono text-[12px] text-foreground">{h.syntax}</pre>
      {h.example && (
        <div className="mt-1.5 flex flex-wrap items-center gap-1.5 text-[11.5px] text-faint">
          <span className="font-mono">{h.example}</span>
          {h.output && <><span aria-hidden>→</span><span className="rounded bg-success-bg px-1.5 py-0.5 font-mono font-medium text-success">{h.output}</span></>}
        </div>
      )}
    </div>
  )
}

/**
 * Self-contained "Templating helpers" reference popup (#120): a searchable, categorized catalog of the
 * Handlebars helpers for mapping request values into responses. Renders its own trigger button.
 */
export function HelpersDialog() {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [q, setQ] = useState('')
  const [cat, setCat] = useState(TEMPLATING_HELPERS[0].key)

  useEffect(() => {
    const onOpen = () => setOpen(true)
    window.addEventListener(OPEN_EVENT, onOpen)
    return () => window.removeEventListener(OPEN_EVENT, onOpen)
  }, [])

  const query = q.trim().toLowerCase()
  const searching = query.length > 0
  const matches = useMemo(() => {
    const hit = (h: Helper) => `${h.name} ${h.syntax} ${h.desc}`.toLowerCase().includes(query)
    return TEMPLATING_HELPERS.map((c) => ({ cat: c, list: searching ? c.helpers.filter(hit) : c.helpers }))
  }, [query, searching])

  const visible = searching ? matches.filter((m) => m.list.length) : matches.filter((m) => m.cat.key === cat)

  return (
    <Dialog.Root open={open} onOpenChange={setOpen}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-50 bg-black/40 data-[state=open]:animate-in data-[state=open]:fade-in-0" />
        <Dialog.Content className="fixed left-1/2 top-1/2 z-50 flex h-[76vh] max-h-[680px] w-[92vw] max-w-[880px] -translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-2xl border border-border bg-background shadow-2xl outline-none data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95">
          <div className="flex items-center gap-2 border-b border-border px-5 py-3.5">
            <BookOpen className="size-4 text-violet" />
            <Dialog.Title className="text-[15px] font-semibold">{t('editor.helpers')}</Dialog.Title>
            <Dialog.Description className="sr-only">{t('editor.helpersHint')}</Dialog.Description>
            <Dialog.Close className="ms-auto rounded-lg p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"><X className="size-4" /></Dialog.Close>
          </div>

          <div className="border-b border-border p-3">
            <label className="flex h-9 items-center gap-2 rounded-lg border border-border bg-muted/50 px-3 focus-within:border-border-strong">
              <Search className="size-4 shrink-0 text-muted-foreground" />
              <input autoFocus value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('common.search')} className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground" />
              {q && <button onClick={() => setQ('')} aria-label={t('common.clear')} className="text-muted-foreground hover:text-foreground"><X className="size-3.5" /></button>}
            </label>
          </div>

          <div className="flex min-h-0 flex-1">
            <nav className="scroll-area w-52 shrink-0 space-y-0.5 overflow-y-auto border-e border-border p-2">
              {matches.map(({ cat: c, list }) => (
                <button key={c.key} onClick={() => { setCat(c.key); setQ('') }}
                  className={cn('flex w-full items-center justify-between gap-2 rounded-lg px-2.5 py-1.5 text-start text-[13px] transition-colors',
                    !searching && cat === c.key ? 'bg-muted font-medium text-foreground' : 'text-muted-foreground hover:bg-muted/60')}>
                  <span className="truncate">{c.label}</span>
                  <span className="shrink-0 text-[11px] tabular-nums text-faint">{searching ? list.length : c.helpers.length}</span>
                </button>
              ))}
            </nav>

            <div className="scroll-area min-w-0 flex-1 overflow-y-auto p-4">
              {visible.length === 0 ? (
                <p className="p-6 text-center text-sm text-muted-foreground">{t('common.noResults')}</p>
              ) : (
                <div className="space-y-5">
                  {visible.map(({ cat: c, list }) => (
                    <section key={c.key}>
                      {searching && <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-faint">{c.label}</h3>}
                      <div className="grid grid-cols-1 gap-2.5 md:grid-cols-2">
                        {list.map((h) => <HelperCard key={h.name} h={h} />)}
                      </div>
                    </section>
                  ))}
                </div>
              )}
            </div>
          </div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
