import { useEffect, useRef, useState } from 'react'
import { EditorState, type Extension } from '@codemirror/state'
import { EditorView, keymap, lineNumbers, highlightActiveLine, highlightActiveLineGutter } from '@codemirror/view'
import { defaultKeymap, history, historyKeymap, indentWithTab } from '@codemirror/commands'
import { bracketMatching, foldGutter, foldKeymap, indentOnInput, syntaxHighlighting, HighlightStyle } from '@codemirror/language'
import { json, jsonParseLinter } from '@codemirror/lang-json'
import { linter, lintGutter } from '@codemirror/lint'
import { tags as t } from '@lezer/highlight'
import { Check, Copy, Sparkles, Upload } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

// Syntax colours map to our own status ramp (var(--info)/success/warning/violet), so the editor's
// highlighting flips with light/dark automatically — no CodeMirror light/dark theme swap needed.
const highlight = HighlightStyle.define([
  { tag: [t.propertyName], color: 'var(--info)' },
  { tag: [t.string], color: 'var(--success)' },
  { tag: [t.number], color: 'var(--warning)' },
  { tag: [t.bool, t.null, t.keyword], color: 'var(--violet)' },
  { tag: [t.punctuation, t.separator, t.brace, t.squareBracket], color: 'var(--muted-foreground)' },
  { tag: [t.invalid], color: 'var(--danger)' },
])

// Chrome themed with our tokens: transparent surface, muted gutter, subtle active line, our focus ring.
const theme = EditorView.theme({
  '&': { fontSize: '12.5px', color: 'var(--foreground)', backgroundColor: 'transparent', height: '100%' },
  '.cm-scroller': { fontFamily: 'var(--font-mono, ui-monospace, SFMono-Regular, Menlo, monospace)', lineHeight: '1.6' },
  '.cm-content': { padding: '8px 0', caretColor: 'var(--foreground)' },
  '.cm-gutters': { backgroundColor: 'transparent', color: 'var(--faint)', border: 'none' },
  '.cm-activeLine': { backgroundColor: 'color-mix(in srgb, var(--muted) 45%, transparent)' },
  '.cm-activeLineGutter': { backgroundColor: 'transparent', color: 'var(--muted-foreground)' },
  '.cm-foldGutter .cm-gutterElement': { cursor: 'pointer', color: 'var(--faint)' },
  '&.cm-focused': { outline: 'none' },
  '.cm-selectionBackground, &.cm-focused .cm-selectionBackground': { backgroundColor: 'color-mix(in srgb, var(--info) 22%, transparent)' },
  '.cm-cursor': { borderLeftColor: 'var(--foreground)' },
  '.cm-lintRange-error': { textDecoration: 'underline wavy var(--danger)' },
  '.cm-tooltip': { backgroundColor: 'var(--background)', border: '0.5px solid var(--border)', borderRadius: '8px', color: 'var(--foreground)' },
})

interface EditorOpts { lint: boolean; minimal: boolean }

const extensions = (readOnly: boolean, onChange: ((v: string) => void) | undefined, { lint, minimal }: EditorOpts): Extension[] => [
  // `minimal` drops the gutters/active-line chrome for compact inline fields (response/webhook body).
  ...(minimal ? [] : [lineNumbers(), foldGutter(), highlightActiveLine(), highlightActiveLineGutter()]),
  history(),
  indentOnInput(),
  bracketMatching(),
  keymap.of([...defaultKeymap, ...historyKeymap, ...foldKeymap, indentWithTab]),
  json(),
  // Bodies may hold Handlebars templates ({{…}}), so linting is opt-out to avoid false parse errors.
  ...(lint ? [linter(jsonParseLinter()), lintGutter()] : []),
  syntaxHighlighting(highlight),
  theme,
  EditorState.readOnly.of(readOnly),
  EditorView.editable.of(!readOnly),
  EditorView.lineWrapping,
  ...(onChange
    ? [EditorView.updateListener.of((u) => { if (u.docChanged) onChange(u.state.doc.toString()) })]
    : []),
]

/**
 * A CodeMirror 6 JSON editor: syntax highlighting, foldable nodes, bracket matching, an inline
 * parse-error linter, and line numbers — themed to our tokens so it adapts to light/dark. Controlled
 * by `value`/`onChange`; external value changes are reconciled without stealing the cursor.
 */
export function JsonEditor({ value, onChange, readOnly = false, lint = true, minimal = false, className }: {
  value: string
  onChange?: (v: string) => void
  readOnly?: boolean
  lint?: boolean
  minimal?: boolean
  className?: string
}) {
  const host = useRef<HTMLDivElement>(null)
  const view = useRef<EditorView | null>(null)
  const onChangeRef = useRef(onChange)
  onChangeRef.current = onChange

  useEffect(() => {
    if (!host.current) return
    const cm = new EditorView({
      parent: host.current,
      state: EditorState.create({ doc: value, extensions: extensions(readOnly, (v) => onChangeRef.current?.(v), { lint, minimal }) }),
    })
    view.current = cm
    return () => { cm.destroy(); view.current = null }
    // Intentionally init once (per config); external value sync is handled by the effect below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [readOnly, lint, minimal])

  // Reconcile an external value change (e.g. Beautify, Upload, tab switch) into the editor.
  useEffect(() => {
    const cm = view.current
    if (cm && value !== cm.state.doc.toString()) {
      cm.dispatch({ changes: { from: 0, to: cm.state.doc.length, insert: value } })
    }
  }, [value])

  return <div ref={host} className={className} />
}

/**
 * A framed JSON editor with an on-field toolbar (Beautify, Copy, optional Upload) that floats top-right.
 * Copy flips to an inline check for ~1.2s — no toast. `fill` makes it stretch to the parent (JSON tab);
 * otherwise it takes a fixed `height` (compact body fields). Beautify is a no-op on non-JSON content.
 */
export function JsonField({
  value, onChange, height = 160, fill = false, lint = true, minimal = false, invalid = false, readOnly = false, onUpload,
}: {
  value: string
  onChange?: (v: string) => void
  height?: number
  fill?: boolean
  lint?: boolean
  minimal?: boolean
  invalid?: boolean
  readOnly?: boolean
  onUpload?: () => void
}) {
  const { t } = useTranslation()
  const [copied, setCopied] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  useEffect(() => () => clearTimeout(timer.current), [])

  const copy = () => {
    void navigator.clipboard?.writeText(value)
    setCopied(true)
    clearTimeout(timer.current)
    timer.current = setTimeout(() => setCopied(false), 1200)
  }
  const beautify = () => {
    if (!onChange) return
    try { onChange(JSON.stringify(JSON.parse(value), null, 2)) } catch { /* leave as-is: body may be templated / non-JSON */ }
  }

  return (
    <div
      className={cn('group relative overflow-hidden rounded-lg border bg-background', invalid ? 'border-danger' : 'border-input', fill && 'h-full')}
      style={fill ? undefined : { height }}
    >
      <div className="absolute end-1.5 top-1.5 z-10 flex items-center gap-0.5 rounded-md border border-border/70 bg-background/85 p-0.5 opacity-70 shadow-sm backdrop-blur transition-opacity group-hover:opacity-100 group-focus-within:opacity-100">
        {onUpload && <ToolBtn title={t('editor.upload')} onClick={onUpload}><Upload className="size-3.5" /></ToolBtn>}
        {!readOnly && onChange && <ToolBtn title={t('editor.beautify')} onClick={beautify}><Sparkles className="size-3.5" /></ToolBtn>}
        <ToolBtn title={copied ? t('editor.copied') : t('editor.copy')} onClick={copy} className={cn(copied && 'text-success')}>
          {copied ? <Check className="size-3.5" /> : <Copy className="size-3.5" />}
        </ToolBtn>
      </div>
      <JsonEditor value={value} onChange={onChange} readOnly={readOnly} lint={lint} minimal={minimal} className="h-full" />
    </div>
  )
}

function ToolBtn({ title, onClick, className, children }: { title: string; onClick: () => void; className?: string; children: React.ReactNode }) {
  return (
    <button
      type="button" title={title} aria-label={title} onClick={onClick}
      className={cn('flex size-6 items-center justify-center rounded text-muted-foreground transition-colors hover:bg-muted hover:text-foreground', className)}
    >
      {children}
    </button>
  )
}
