import { cn } from '@/lib/utils'
import type { StubStatus } from '@/lib/api'

// Method chips and status pills draw from the semantic token ramp — configurable in one place, kept
// separate from the accent so status always reads at a glance.
const METHOD_TONE: Record<string, string> = {
  GET: 'text-info bg-info-bg border-info-border',
  POST: 'text-success bg-success-bg border-success-border',
  PUT: 'text-warning bg-warning-bg border-warning-border',
  DELETE: 'text-danger bg-danger-bg border-danger-border',
  PATCH: 'text-violet bg-violet-bg border-violet-border',
}

export function MethodChip({ method }: { method: string }) {
  return (
    <span className={cn('inline-flex rounded-md border px-2 py-0.5 font-mono text-[11px] font-bold', METHOD_TONE[method] ?? 'text-muted-foreground bg-muted border-border')}>
      {method}
    </span>
  )
}

const STATUS: Record<StubStatus, { tone: string; dot: string; key: string }> = {
  live: { tone: 'text-success bg-success-bg border-success-border', dot: 'bg-success', key: 'status.live' },
  proxy: { tone: 'text-info bg-info-bg border-info-border', dot: 'bg-info', key: 'status.proxy' },
  draft: { tone: 'text-muted-foreground bg-muted border-border', dot: 'bg-faint', key: 'status.draft' },
}

export function StatusPill({ status, label }: { status: StubStatus; label: string }) {
  const s = STATUS[status]
  return (
    <span className={cn('inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-[11.5px] font-semibold', s.tone)}>
      <span className={cn('size-1.5 rounded-full', s.dot)} />
      {label}
    </span>
  )
}
