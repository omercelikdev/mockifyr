import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { toast } from 'sonner'
import { Check, Globe, Pencil, Plus, Trash2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import {
  ENV_NAME_PATTERN, type MockEnvironment, removeEnvironment, saveEnvironment, setActiveEnvironment, useEnvironments,
} from '@/lib/environments'
import { Button } from '@/components/ui/button'
import { Input, Label } from '@/components/ui/field'
import { EmptyState } from '@/components/ui/empty-state'
import { ConfirmDialog } from '@/components/ui/confirm-dialog'

/**
 * Postman-style environments (#157): named base URLs used as {{name}} in the app's URL fields
 * (webhook/callback URL, proxy base URL, recordings target). One environment can be active — it
 * additionally answers the generic {{baseUrl}} variable. Stored in the browser (localStorage);
 * resolution happens when a URL is used, so the saved mappings stay plain, portable URLs.
 */
export function EnvironmentsPage() {
  const { t } = useTranslation()
  const { environments, active } = useEnvironments()

  const [editing, setEditing] = useState<string | null>(null) // name being edited, '' = new row
  const [name, setName] = useState('')
  const [baseUrl, setBaseUrl] = useState('')
  const [confirmDelete, setConfirmDelete] = useState<string | null>(null)

  const startEdit = (env?: MockEnvironment) => {
    setEditing(env?.name ?? '')
    setName(env?.name ?? '')
    setBaseUrl(env?.baseUrl ?? '')
  }

  const nameInvalid = name.trim().length > 0 && !ENV_NAME_PATTERN.test(name.trim())
  const nameTaken = editing !== null && name.trim() !== editing && environments.some((e) => e.name === name.trim())

  const save = () => {
    const trimmedName = name.trim()
    const trimmedUrl = baseUrl.trim().replace(/\/$/, '') // {{name}}/path composes without double slashes
    if (!trimmedName || !trimmedUrl || nameInvalid || nameTaken) return
    saveEnvironment({ name: trimmedName, baseUrl: trimmedUrl }, editing || undefined)
    toast.success(t('env.saved'))
    setEditing(null)
  }

  return (
    <div className="mx-auto max-w-[1100px]">
      <header className="mb-5 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">{t('nav.environments')}</h1>
          <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('env.subtitle')}</p>
        </div>
        <Button variant="primary" onClick={() => startEdit()}><Plus />{t('env.add')}</Button>
      </header>

      <div className="overflow-hidden rounded-2xl border border-border bg-background shadow-surface">
        {environments.length === 0 && editing === null ? (
          <EmptyState art={<Globe className="size-10 text-faint" />} title={t('env.empty')} className="py-16" />
        ) : (
          <table className="w-full border-collapse">
            <thead>
              <tr>
                {[t('env.active'), t('env.name'), t('env.baseUrl'), ''].map((h, i) => (
                  <th key={i} className="border-b border-border bg-muted/40 px-4 py-2.5 text-start text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {environments.map((env) => (
                <tr key={env.name} className="border-b border-border">
                  <td className="w-20 px-4 py-2.5">
                    <button
                      onClick={() => setActiveEnvironment(active === env.name ? null : env.name)}
                      aria-label={t('env.activate')} title={t('env.activate')}
                      className={cn('flex size-5 items-center justify-center rounded-full border transition-colors',
                        active === env.name ? 'border-success bg-success text-white' : 'border-border hover:border-muted-foreground')}
                    >
                      {active === env.name && <Check className="size-3.5" />}
                    </button>
                  </td>
                  <td className="px-4 py-2.5 font-mono text-[12.5px] font-medium">{`{{${env.name}}}`}</td>
                  <td className="px-4 py-2.5 font-mono text-[12.5px] text-muted-foreground">{env.baseUrl}</td>
                  <td className="w-24 px-4 py-2.5">
                    <div className="flex justify-end gap-1">
                      <Button variant="ghost" size="iconSm" aria-label={t('stubs.edit')} onClick={() => startEdit(env)}><Pencil /></Button>
                      <Button variant="ghost" size="iconSm" aria-label={t('stubs.delete')} onClick={() => setConfirmDelete(env.name)}><Trash2 /></Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {editing !== null && (
          <div className="border-t border-border bg-muted/30 p-4">
            <div className="grid grid-cols-[200px_minmax(0,1fr)_auto_auto] items-end gap-2">
              <div>
                <Label>{t('env.name')}</Label>
                <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="local" className="font-mono"
                  onKeyDown={(e) => { if (e.key === 'Enter') save() }} autoFocus />
              </div>
              <div>
                <Label>{t('env.baseUrl')}</Label>
                <Input value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder="https://dev.example.intra" className="font-mono"
                  onKeyDown={(e) => { if (e.key === 'Enter') save() }} />
              </div>
              <Button variant="primary" onClick={save} disabled={!name.trim() || !baseUrl.trim() || nameInvalid || nameTaken}>{t('env.save')}</Button>
              <Button variant="ghost" onClick={() => setEditing(null)}>{t('editor.cancel')}</Button>
            </div>
            {(nameInvalid || nameTaken) && (
              <p className="mt-2 text-xs text-danger">{nameInvalid ? t('env.invalidName') : t('env.nameTaken')}</p>
            )}
          </div>
        )}
      </div>

      {/* The {ex} slot is filled here (not via i18next interpolation) because the example itself
          contains double curly braces, which i18next would treat as a variable. */}
      <p className="mt-4 max-w-[70ch] text-sm text-muted-foreground">
        {t('env.hint').split('{ex}').map((part, i, arr) => (
          <span key={i}>
            {part}
            {i < arr.length - 1 && <code className="rounded bg-muted px-1.5 py-0.5 font-mono text-[12px]">{'{{local}}'}/OrderManager/callback</code>}
          </span>
        ))}
      </p>

      <ConfirmDialog
        open={confirmDelete !== null} onOpenChange={(o) => { if (!o) setConfirmDelete(null) }}
        title={t('env.deleteTitle')} body={t('env.deleteBody')}
        confirmLabel={t('stubs.delete')} cancelLabel={t('editor.cancel')}
        onConfirm={() => { if (confirmDelete) { removeEnvironment(confirmDelete); toast.success(t('env.deleted')); setConfirmDelete(null) } }}
      />
    </div>
  )
}
