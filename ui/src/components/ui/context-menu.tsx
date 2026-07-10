import { Fragment, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { cn } from '@/lib/utils'

export interface ContextMenuAction {
  label: string
  icon?: React.ReactNode
  disabled?: boolean
  separatorBefore?: boolean
  onSelect: () => void
}

/**
 * A cursor-positioned context menu (right-click). Renders at the given viewport point, clamped so it
 * never overflows the window; a full-screen backdrop closes it on any outside click, and Escape
 * dismisses it too. Selecting an action runs it, then closes the menu.
 */
export function ContextMenu({ x, y, actions, onClose }: {
  x: number
  y: number
  actions: ContextMenuAction[]
  onClose: () => void
}) {
  const ref = useRef<HTMLDivElement>(null)
  const [pos, setPos] = useState({ x, y })

  useLayoutEffect(() => {
    const rect = ref.current?.getBoundingClientRect()
    if (!rect) return
    setPos({
      x: Math.max(8, Math.min(x, window.innerWidth - rect.width - 8)),
      y: Math.max(8, Math.min(y, window.innerHeight - rect.height - 8)),
    })
  }, [x, y])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])

  return (
    <div className="fixed inset-0 z-50" onMouseDown={onClose} onContextMenu={(e) => { e.preventDefault(); onClose() }}>
      <div
        ref={ref} role="menu" style={{ left: pos.x, top: pos.y }} onMouseDown={(e) => e.stopPropagation()}
        className="absolute min-w-52 rounded-xl border border-border bg-background p-1.5 text-foreground shadow-[0_12px_36px_rgb(24_24_27/0.15)]"
      >
        {actions.map((a, i) => (
          <Fragment key={i}>
            {a.separatorBefore && <div className="my-1.5 h-px bg-border" />}
            <button
              type="button" role="menuitem" disabled={a.disabled}
              onClick={() => { onClose(); a.onSelect() }}
              className={cn(
                'flex w-full cursor-pointer select-none items-center gap-2.5 rounded-lg px-2.5 py-1.5 text-start text-[13px] outline-none transition-colors',
                'hover:bg-muted focus-visible:bg-muted disabled:cursor-default disabled:opacity-40 disabled:hover:bg-transparent',
              )}
            >
              {a.icon}
              {a.label}
            </button>
          </Fragment>
        ))}
      </div>
    </div>
  )
}
