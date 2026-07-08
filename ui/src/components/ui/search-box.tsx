import { useEffect, useState } from 'react'
import { Search, X } from 'lucide-react'

/**
 * A search input that commits on Enter (not on every keystroke) — matching a server-side search mental
 * model and avoiding per-character churn. The clear button commits an empty term. The draft resyncs
 * when the committed value is reset externally (e.g. switching tenant).
 */
export function SearchBox({
  value, onCommit, placeholder,
}: {
  value: string
  onCommit: (v: string) => void
  placeholder?: string
}) {
  const [draft, setDraft] = useState(value)
  useEffect(() => { setDraft(value) }, [value])

  return (
    <label className="flex h-9 min-w-[220px] flex-1 items-center gap-2 rounded-lg border border-border bg-muted/50 px-3 transition-colors focus-within:border-border-strong">
      <Search className="size-4 shrink-0 text-muted-foreground" />
      <input
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); onCommit(draft.trim()) } }}
        placeholder={placeholder}
        className="w-full bg-transparent text-sm outline-none placeholder:text-muted-foreground"
      />
      {(draft || value) && (
        <button
          type="button"
          onClick={() => { setDraft(''); onCommit('') }}
          aria-label="Clear"
          className="shrink-0 text-muted-foreground transition-colors hover:text-foreground"
        >
          <X className="size-3.5" />
        </button>
      )}
    </label>
  )
}
