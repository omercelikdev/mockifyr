import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Activity, CheckCircle2, ListTree, Waypoints, XCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { fetchHealth, fetchJournal, fetchScenarios, fetchStubs, persistenceLabel } from '@/lib/api'

// Live tenant-scoped metrics, read from /__admin. When no host answers, every query falls back to
// representative sample data and the "sample" hint is shown — the numbers are never fabricated.
export function DashboardPage() {
  const { t } = useTranslation()
  const { tenant } = useUi()

  const stubs = useQuery({ queryKey: ['stubs', tenant], queryFn: () => fetchStubs(tenant) })
  const journal = useQuery({ queryKey: ['journal', tenant, false], queryFn: () => fetchJournal(tenant, false) })
  const unmatched = useQuery({ queryKey: ['journal', tenant, true], queryFn: () => fetchJournal(tenant, true) })
  const scenarios = useQuery({ queryKey: ['scenarios', tenant], queryFn: () => fetchScenarios(tenant) })
  const health = useQuery({ queryKey: ['health', tenant], queryFn: () => fetchHealth(tenant) })

  const mock = stubs.data?.mock ?? false
  const num = (n?: number) => (n == null ? '—' : n.toLocaleString())

  return (
    <div className="mx-auto max-w-[1360px]">
      <header className="mb-6 flex items-start gap-3">
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">{t('dashboard.title')}</h1>
          <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('dashboard.subtitle')}</p>
        </div>
        {mock && (
          <span className="ms-auto shrink-0 rounded-full border border-warning-border bg-warning-bg px-2.5 py-0.5 text-[11.5px] font-medium text-warning">{t('stubs.sample')}</span>
        )}
      </header>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard icon={ListTree} label={t('dashboard.activeStubs')} value={num(stubs.data?.stubs.length)} loading={stubs.isLoading} />
        <StatCard icon={Activity} label={t('dashboard.requests')} value={num(journal.data?.total)} loading={journal.isLoading} />
        <StatCard icon={XCircle} label={t('dashboard.unmatched')} value={num(unmatched.data?.total)} loading={unmatched.isLoading} tone={(unmatched.data?.total ?? 0) > 0 ? 'danger' : undefined} />
        <StatCard icon={Waypoints} label={t('nav.scenarios')} value={num(scenarios.data?.scenarios.length)} loading={scenarios.isLoading} />
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-2 rounded-2xl border border-border bg-background p-4 text-sm text-muted-foreground shadow-surface">
        <CheckCircle2 className="size-4 text-success" />
        <span className="font-medium text-success">{t('dashboard.verified')}</span>
        <span className="text-border-strong">·</span>
        <span>
          {health.data ? `${health.data.health.name} ${health.data.health.version} · ${persistenceLabel(health.data.health.persistence)}` : '·'}
        </span>
        <span className="ms-auto font-mono text-xs">
          {t('settings.tenants')}: <b className="tabular-nums">{num(health.data?.health.tenants)}</b>
        </span>
      </div>
    </div>
  )
}

const TONE: Record<string, string> = { danger: 'text-danger' }

function StatCard({
  icon: Icon, label, value, loading, tone,
}: {
  icon: React.ComponentType<{ className?: string }>
  label: string
  value: string
  loading?: boolean
  tone?: keyof typeof TONE
}) {
  return (
    <div className="rounded-2xl border border-border bg-background p-4 shadow-surface">
      <div className="flex items-center gap-2 text-xs font-semibold text-muted-foreground">
        <Icon className="size-4" />
        {label}
      </div>
      {loading ? (
        <div className="mt-3 h-7 w-20 animate-pulse rounded bg-muted" />
      ) : (
        <div className={cn('mt-2.5 text-[27px] font-bold tracking-tight tabular-nums', tone && TONE[tone])}>{value}</div>
      )}
    </div>
  )
}
