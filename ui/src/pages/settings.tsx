import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import { ArrowDownToLine, ArrowUpFromLine, Boxes, Check, Database, GitBranch, KeyRound, Moon, Palette, ShieldCheck, Sun } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { fetchGitStatus, fetchHealth, gitConfigure, gitPull, gitPush, gitSetCredentials, persistenceLabel } from '@/lib/api'
import { LOCALES } from '@/lib/i18n'
import { Button } from '@/components/ui/button'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'
import { Input } from '@/components/ui/field'

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

        {/* Git sync (ADR 0007): status + explicit push/pull against the host's configured remote */}
        <GitCard />

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

/**
 * Git sync card: the host's sync state (remote/branch/dirty/ahead/behind) plus explicit Pull and
 * Push actions. Push opens a small dialog for an optional commit message. Typed host errors
 * (pull-first, diverged, invalid remote tree, auth) surface verbatim in an error toast. Hidden
 * behaviors: unconfigured hosts get the setup hint; unreachable hosts (sample mode) show nothing.
 */
function GitCard() {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const queryClient = useQueryClient()
  // No refetch interval: the host's status endpoint fetches from the remote, so polling would
  // hammer the Git server. It refreshes after every action instead.
  const { data: status } = useQuery({ queryKey: ['gitStatus'], queryFn: () => fetchGitStatus(tenant), refetchOnWindowFocus: false })
  const [busy, setBusy] = useState<'push' | 'pull' | null>(null)
  const [pushOpen, setPushOpen] = useState(false)
  const [message, setMessage] = useState('')

  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['gitStatus'] })

  async function pull() {
    setBusy('pull')
    const result = await gitPull(tenant)
    setBusy(null)
    if (!result.ok) toast.error(result.message)
    else if (result.reason === 'up-to-date') toast.message(t('git.upToDate'))
    else {
      toast.success(t('git.pulled', { count: result.stubsLoaded ?? 0 }))
      // The served stub set changed — every tenant-scoped view refetches.
      void queryClient.invalidateQueries()
    }
    refresh()
  }

  async function push() {
    setPushOpen(false)
    setBusy('push')
    const result = await gitPush(tenant, message)
    setBusy(null)
    setMessage('')
    if (!result.ok) toast.error(result.message)
    else toast[result.reason === 'nothing-to-push' ? 'message' : 'success'](
      result.reason === 'nothing-to-push' ? t('git.nothingToPush') : t('git.pushed'))
    refresh()
  }

  const [remoteUrl, setRemoteUrl] = useState('')
  const [branch, setBranch] = useState('main')
  const [connecting, setConnecting] = useState(false)

  // Credentials (#153): sent once to the host, held in its process memory only — never persisted,
  // never echoed back. The status only reports the source (none/environment/dashboard).
  const [token, setToken] = useState('')
  const [username, setUsername] = useState('')
  const [savingCreds, setSavingCreds] = useState(false)

  async function saveCredentials() {
    setSavingCreds(true)
    const result = await gitSetCredentials(tenant, token.trim(), username)
    setSavingCreds(false)
    if ('error' in result) toast.error(result.message)
    else {
      toast.success(token.trim() ? t('git.credentialsSaved') : t('git.credentialsCleared'))
      setToken('')
      setUsername('')
    }
    refresh()
  }

  async function connect() {
    if (!remoteUrl.trim()) return
    setConnecting(true)
    // Save the optional token first, so the connect's own status fetch already authenticates.
    if (token.trim()) {
      const creds = await gitSetCredentials(tenant, token.trim(), username)
      if ('error' in creds) { toast.error(creds.message); setConnecting(false); return }
      setToken('')
      setUsername('')
    }
    const result = await gitConfigure(tenant, remoteUrl.trim(), branch)
    setConnecting(false)
    if ('error' in result) toast.error(result.message)
    else {
      toast.success(t('git.connected'))
      setRemoteUrl('')
    }
    refresh()
  }

  return (
    <Card icon={GitBranch} title={t('git.title')}>
      <p className="mb-3 text-sm text-muted-foreground">{t('git.hint')}</p>
      {!status?.configured ? (
        // Connect form (#151): remote + branch only — the local working copy resolves host-side, and
        // credentials never pass through the browser (private HTTPS remotes use MOCKIFYR_GIT_TOKEN).
        <div className="space-y-2.5">
          <div className="grid grid-cols-[minmax(0,1fr)_130px] gap-2">
            <Input value={remoteUrl} onChange={(e) => setRemoteUrl(e.target.value)}
              placeholder="https://github.com/team/stubs.git" className="font-mono"
              onKeyDown={(e) => { if (e.key === 'Enter') void connect() }} />
            <Input value={branch} onChange={(e) => setBranch(e.target.value)} placeholder="main" className="font-mono" />
          </div>
          <div className="grid grid-cols-[minmax(0,1fr)_130px] gap-2">
            <Input type="password" autoComplete="off" value={token} onChange={(e) => setToken(e.target.value)}
              placeholder={t('git.tokenPlaceholder')} className="font-mono" />
            <Input value={username} onChange={(e) => setUsername(e.target.value)} placeholder={t('git.usernamePlaceholder')} className="font-mono" />
          </div>
          <div className="flex items-center gap-3">
            <Button size="sm" variant="primary" onClick={() => void connect()} disabled={connecting || !remoteUrl.trim()}>
              <GitBranch />{connecting ? '…' : t('git.connect')}
            </Button>
            <p className="text-xs text-faint">{t('git.tokenHint')}</p>
          </div>
        </div>
      ) : (
        <>
          <dl className="mb-3 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
            <dt className="text-xs text-muted-foreground">{t('git.remote')}</dt>
            <dd className="min-w-0 truncate font-mono text-[12.5px]">{status.remote}</dd>
            <dt className="text-xs text-muted-foreground">{t('git.branch')}</dt>
            <dd className="font-mono text-[12.5px]">{status.branch}</dd>
          </dl>
          {status.configuredBy === 'flags' && (
            <p className="mb-3 text-xs text-faint">{t('git.pinnedByFlags')}</p>
          )}
          <div className="mb-4 flex flex-wrap gap-1.5">
            <Chip tone={status.dirty ? 'warning' : 'success'}>{status.dirty ? t('git.dirty') : t('git.clean')}</Chip>
            {status.ahead > 0 && <Chip tone="info">↑ {t('git.ahead', { count: status.ahead })}</Chip>}
            {status.behind > 0 && <Chip tone="info">↓ {t('git.behind', { count: status.behind })}</Chip>}
            {status.fetchError && <Chip tone="danger">{t('git.fetchError')}</Chip>}
          </div>
          <div className="mb-4 space-y-2">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <KeyRound className="size-3.5" />
              <span>{t('git.credentials')}</span>
              {status.credentialsSource === 'dashboard' && <Chip tone="success">{t('git.credsDashboard')}</Chip>}
              {status.credentialsSource === 'environment' && <Chip tone="info">{t('git.credsEnv')}</Chip>}
            </div>
            <div className="grid grid-cols-[minmax(0,1fr)_130px_auto] gap-2">
              <Input type="password" autoComplete="off" value={token} onChange={(e) => setToken(e.target.value)}
                placeholder={status.credentialsSource === 'dashboard' ? '••••••••' : t('git.tokenPlaceholder')} className="font-mono"
                onKeyDown={(e) => { if (e.key === 'Enter' && token.trim()) void saveCredentials() }} />
              <Input value={username} onChange={(e) => setUsername(e.target.value)} placeholder={t('git.usernamePlaceholder')} className="font-mono" />
              <Button size="sm" variant="outline" onClick={() => void saveCredentials()}
                disabled={savingCreds || (!token.trim() && status.credentialsSource !== 'dashboard')}>
                {savingCreds ? '…' : token.trim() ? t('git.saveCredentials') : t('git.clearCredentials')}
              </Button>
            </div>
            <p className="text-xs text-faint">{t('git.credentialsHint')}</p>
          </div>
          <div className="flex gap-2">
            <Button size="sm" variant="outline" onClick={() => void pull()} disabled={busy !== null}>
              <ArrowDownToLine />{busy === 'pull' ? '…' : t('git.pull')}
            </Button>
            <Button size="sm" variant="primary" onClick={() => setPushOpen(true)} disabled={busy !== null}>
              <ArrowUpFromLine />{busy === 'push' ? '…' : t('git.push')}
            </Button>
          </div>
          <ConfirmDialog
            open={pushOpen} onOpenChange={setPushOpen}
            title={t('git.pushTitle')} body={t('git.pushHint')}
            confirmLabel={t('git.push')} cancelLabel={t('editor.cancel')}
            onConfirm={() => void push()}
          >
            <Input className="mt-3" value={message} onChange={(e) => setMessage(e.target.value)}
              placeholder={t('git.messagePlaceholder')} onKeyDown={(e) => { if (e.key === 'Enter') void push() }} />
          </ConfirmDialog>
        </>
      )}
    </Card>
  )
}

function Chip({ tone, children }: { tone: 'success' | 'warning' | 'info' | 'danger'; children: React.ReactNode }) {
  const tones = {
    success: 'border-success-border bg-success-bg text-success',
    warning: 'border-warning-border bg-warning-bg text-warning',
    info: 'border-info-border bg-info-bg text-info',
    danger: 'border-danger-border bg-danger-bg text-danger',
  }
  return <span className={cn('inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-[11.5px] font-medium', tones[tone])}>{children}</span>
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
