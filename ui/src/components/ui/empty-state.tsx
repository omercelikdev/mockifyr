import { cn } from '@/lib/utils'

/**
 * A centred empty-state block: a large icon in a soft rounded tile, a title, an optional body line, and
 * optional actions. Shared across screens (stubs workspace, scenarios, recordings, journal, extensions)
 * so blank areas read as intentional guidance rather than a void.
 */
export function EmptyState({ art, title, body, action, className }: {
  art: React.ReactNode
  title: string
  body?: string
  action?: React.ReactNode
  className?: string
}) {
  return (
    <div className={cn('flex flex-1 flex-col items-center justify-center gap-4 p-8 text-center', className)}>
      <div className="mb-1">{art}</div>
      <div className="space-y-1.5">
        <h2 className="text-base font-semibold text-foreground">{title}</h2>
        {body && <p className="mx-auto max-w-[46ch] text-sm text-muted-foreground">{body}</p>}
      </div>
      {action && <div className="flex flex-wrap items-center justify-center gap-2">{action}</div>}
    </div>
  )
}
