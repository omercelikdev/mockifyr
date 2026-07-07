import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Boxes, Check, Database, Moon, Palette, ShieldCheck, Sun } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { fetchHealth, persistenceLabel } from '@/lib/api'
import { LOCALES } from '@/lib/i18n'
import { Button } from '@/components/ui/button'

export function SettingsPage() {
  const { t } = useTranslation()
  const { tenant, theme, setTheme, locale, setLocale } = useUi()
  const { data } = useQuery({ queryKey: ['health', tenant], queryFn: () => fetchHealth(tenant), refetchInterval: 8000 })
  const health = data?.health

  const providers = ['NullStubPersistence', 'FileSystemStubPersistence', 'LiteDbStubPersistence', 'PostgresStubPersistence', 'RedisStubPersistence']

  return (
    <div className="mx-auto max-w-[1360px]">
      <header className="mb-6">
        <h1 className="text-[22px] font-bold tracking-tight">{t('nav.settings')}</h1>
        <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('settings.subtitle')}</p>
      </header>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {/* Status */}
        <Card icon={Boxes} title={t('settings.status')}>
          {data?.mock && <SampleHint t={t} />}
          <dl className="grid grid-cols-2 gap-x-4 gap-y-3">
            <Stat label={t('settings.engine')} value={`${health?.name ?? 'Mockifyr'} v${health?.version ?? '1.0'}`} />
            <Stat label=".NET" value="10 (LTS)" />
            <Stat label={t('settings.tenants')} value={String(health?.tenants ?? '—')} />
            <Stat label={t('settings.totalStubs')} value={String(health?.totalStubs ?? '—')} />
          </dl>
        </Card>

        {/* Persistence */}
        <Card icon={Database} title={t('settings.persistence')}>
          <p className="mb-3 text-sm text-muted-foreground">{t('settings.persistenceHint')}</p>
          <div className="flex flex-wrap gap-2">
            {providers.map((p) => {
              const active = health?.persistence === p
              return (
                <span key={p} className={cn('inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-semibold',
                  active ? 'border-primary bg-primary text-primary-foreground' : 'border-border bg-muted text-muted-foreground')}>
                  {active && <Check className="size-3.5" />}{persistenceLabel(p)}
                </span>
              )
            })}
          </div>
        </Card>

        {/* Transport (host-config, read-only) */}
        <Card icon={ShieldCheck} title={t('settings.transport')}>
          <p className="mb-3 text-sm text-muted-foreground">{t('settings.transportHint')}</p>
          <ul className="space-y-2 text-sm">
            {['HTTPS / TLS', 'HTTP/2 (ALPN)', 'mTLS / client certificates', 'Multi-domain (host/port/scheme)', 'gRPC · GraphQL · WebSocket'].map((c) => (
              <li key={c} className="flex items-center gap-2"><Check className="size-4 text-success" />{c}</li>
            ))}
          </ul>
        </Card>

        {/* Appearance */}
        <Card icon={Palette} title={t('settings.appearance')}>
          <div className="mb-4">
            <div className="mb-2 text-xs font-semibold text-muted-foreground">{t('common.darkMode')}</div>
            <div className="inline-flex gap-1 rounded-lg bg-muted p-1">
              <button onClick={() => setTheme('light')} className={cn('flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-semibold', theme === 'light' ? 'bg-background shadow-sm' : 'text-muted-foreground')}><Sun className="size-4" />Light</button>
              <button onClick={() => setTheme('dark')} className={cn('flex items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-semibold', theme === 'dark' ? 'bg-background shadow-sm' : 'text-muted-foreground')}><Moon className="size-4" />Dark</button>
            </div>
          </div>
          <div>
            <div className="mb-2 text-xs font-semibold text-muted-foreground">{t('common.language')}</div>
            <div className="flex flex-wrap gap-1.5">
              {LOCALES.map((l) => (
                <Button key={l.code} size="sm" variant={l.code === locale ? 'primary' : 'outline'} onClick={() => setLocale(l.code)}>{l.native}</Button>
              ))}
            </div>
          </div>
        </Card>
      </div>
    </div>
  )
}

function Card({ icon: Icon, title, children }: { icon: React.ComponentType<{ className?: string }>; title: string; children: React.ReactNode }) {
  return (
    <section className="rounded-2xl border border-border bg-background p-5 shadow-surface">
      <div className="mb-4 flex items-center gap-2.5">
        <span className="flex size-8 items-center justify-center rounded-lg bg-muted text-muted-foreground"><Icon className="size-4" /></span>
        <h2 className="font-semibold">{title}</h2>
      </div>
      {children}
    </section>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 font-semibold tabular-nums">{value}</dd>
    </div>
  )
}

function SampleHint({ t }: { t: (k: string) => string }) {
  return <div className="mb-3 inline-flex rounded-full border border-warning-border bg-warning-bg px-2.5 py-0.5 text-[11.5px] font-medium text-warning">{t('stubs.sample')}</div>
}
