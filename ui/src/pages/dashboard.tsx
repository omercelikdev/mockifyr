import { useTranslation } from 'react-i18next'
import { Activity, CheckCircle2, Clock, ListTree, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

export function DashboardPage() {
  const { t } = useTranslation()

  return (
    <div className="mx-auto max-w-[1360px]">
      <header className="mb-6">
        <h1 className="text-[22px] font-bold tracking-tight">{t('dashboard.title')}</h1>
        <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('dashboard.subtitle')}</p>
      </header>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard icon={ListTree} label={t('dashboard.activeStubs')} value="248" delta={`+12 ${t('dashboard.thisWeek')}`} tone="success" points="0,20 12,18 23,19 35,12 47,13 59,8 70,7 82,4" />
        <StatCard icon={Activity} label={t('dashboard.requests')} value="18,402" delta="+8.4%" tone="info" points="0,16 12,18 23,10 35,14 47,9 59,12 70,6 82,8" />
        <StatCard icon={XCircle} label={t('dashboard.unmatched')} value="37" delta="+5" tone="danger" points="0,20 12,19 23,21 35,17 47,18 59,14 70,16 82,11" />
        <StatCard icon={Clock} label={t('dashboard.matchTime')} value="0.8 ms" delta={t('dashboard.stable')} tone="muted" points="0,13 12,14 23,12 35,13 47,12 59,14 70,13 82,12" />
      </div>

      <div className="mt-4 flex items-center gap-2 rounded-2xl border border-border bg-background p-4 text-sm text-muted-foreground shadow-surface">
        <CheckCircle2 className="size-4 text-success" />
        <span className="font-medium text-success">{t('dashboard.verified')}</span>
        <span className="text-border-strong">·</span>
        <span>Engine v1.0 · .NET 10</span>
        <span className="ms-auto font-mono text-xs">p50 0.8ms · p99 3.1ms</span>
      </div>
    </div>
  )
}

const TONE: Record<string, string> = { success: 'text-success', info: 'text-info', danger: 'text-danger', muted: 'text-muted-foreground' }

function StatCard({
  icon: Icon, label, value, delta, tone, points,
}: { icon: React.ComponentType<{ className?: string }>; label: string; value: string; delta: string; tone: keyof typeof TONE; points: string }) {
  return (
    <div className="rounded-2xl border border-border bg-background p-4 shadow-surface">
      <div className="flex items-center gap-2 text-xs font-semibold text-muted-foreground">
        <Icon className="size-4" />
        {label}
      </div>
      <div className="mt-2.5 text-[27px] font-bold tracking-tight tabular-nums">{value}</div>
      <div className="mt-2 flex items-center justify-between">
        <span className={cn('text-xs font-semibold tabular-nums', TONE[tone])}>{delta}</span>
        <svg viewBox="0 0 82 26" preserveAspectRatio="none" className="h-6 w-[82px]">
          <polyline fill="none" stroke="currentColor" strokeWidth="1.5" points={points} className={TONE[tone]} />
        </svg>
      </div>
    </div>
  )
}
