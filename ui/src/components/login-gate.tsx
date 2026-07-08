import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useQueryClient } from '@tanstack/react-query'
import { Lock } from 'lucide-react'
import { verifyAdminAuth } from '@/lib/api'
import { Button } from '@/components/ui/button'
import { Input, Label } from '@/components/ui/field'

/**
 * Full-screen login overlay for hosts started with --admin-user/--admin-pass. It stays dormant until an
 * admin call comes back 401 (adminFetch dispatches a `mockifyr-auth-required` window event), then blocks
 * the app until valid credentials are entered. On success it stores the Basic token and invalidates every
 * query so the dashboard refetches with auth. Hosts without admin auth never emit the event, so the gate
 * never shows.
 */
export function LoginGate() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [user, setUser] = useState('')
  const [pass, setPass] = useState('')
  const [error, setError] = useState(false)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const show = () => setOpen(true)
    window.addEventListener('mockifyr-auth-required', show)
    return () => window.removeEventListener('mockifyr-auth-required', show)
  }, [])

  if (!open) return null

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(false)
    const ok = await verifyAdminAuth(user.trim(), pass)
    setBusy(false)
    if (!ok) {
      setError(true)
      return
    }
    setPass('')
    await queryClient.invalidateQueries()
    setOpen(false)
  }

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-app/80 p-4 backdrop-blur-sm">
      <form
        onSubmit={submit}
        className="w-[min(92vw,380px)] rounded-2xl border border-border bg-surface p-7 shadow-surface"
      >
        <div className="mb-6 flex flex-col items-center gap-2 text-center">
          <div className="flex size-11 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <Lock className="size-5" />
          </div>
          <h1 className="text-lg font-semibold">{t('login.title')}</h1>
          <p className="text-sm text-muted-foreground">{t('login.subtitle')}</p>
        </div>
        <div className="space-y-3">
          <div>
            <Label htmlFor="login-user">{t('login.username')}</Label>
            <Input
              id="login-user"
              autoFocus
              autoComplete="username"
              value={user}
              onChange={(event) => setUser(event.target.value)}
            />
          </div>
          <div>
            <Label htmlFor="login-pass">{t('login.password')}</Label>
            <Input
              id="login-pass"
              type="password"
              autoComplete="current-password"
              value={pass}
              onChange={(event) => setPass(event.target.value)}
            />
          </div>
          {error && <p className="text-sm text-danger">{t('login.invalid')}</p>}
          <Button type="submit" variant="primary" className="w-full" disabled={busy || !user || !pass}>
            {busy ? t('login.signingIn') : t('login.signIn')}
          </Button>
        </div>
      </form>
    </div>
  )
}
