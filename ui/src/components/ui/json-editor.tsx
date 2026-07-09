import { useEffect, useRef } from 'react'
import { EditorState, type Extension } from '@codemirror/state'
import { EditorView, keymap, lineNumbers, highlightActiveLine, highlightActiveLineGutter } from '@codemirror/view'
import { defaultKeymap, history, historyKeymap, indentWithTab } from '@codemirror/commands'
import { bracketMatching, foldGutter, foldKeymap, indentOnInput, syntaxHighlighting, HighlightStyle } from '@codemirror/language'
import { json, jsonParseLinter } from '@codemirror/lang-json'
import { linter, lintGutter } from '@codemirror/lint'
import { tags as t } from '@lezer/highlight'

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

const extensions = (readOnly: boolean, onChange?: (v: string) => void): Extension[] => [
  lineNumbers(),
  foldGutter(),
  history(),
  indentOnInput(),
  bracketMatching(),
  highlightActiveLine(),
  highlightActiveLineGutter(),
  keymap.of([...defaultKeymap, ...historyKeymap, ...foldKeymap, indentWithTab]),
  json(),
  linter(jsonParseLinter()),
  lintGutter(),
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
export function JsonEditor({ value, onChange, readOnly = false, className }: {
  value: string
  onChange?: (v: string) => void
  readOnly?: boolean
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
      state: EditorState.create({ doc: value, extensions: extensions(readOnly, (v) => onChangeRef.current?.(v)) }),
    })
    view.current = cm
    return () => { cm.destroy(); view.current = null }
    // Intentionally init once; external value sync is handled by the effect below.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [readOnly])

  // Reconcile an external value change (e.g. Beautify, Upload, tab switch) into the editor.
  useEffect(() => {
    const cm = view.current
    if (cm && value !== cm.state.doc.toString()) {
      cm.dispatch({ changes: { from: 0, to: cm.state.doc.length, insert: value } })
    }
  }, [value])

  return <div ref={host} className={className} />
}
