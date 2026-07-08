import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Building2, Check, ChevronsUpDown, Plus, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { fetchTenants } from '@/lib/api'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

const prettify = (id: string) => id.split(/[-_]/).filter(Boolean).map((w) => w[0].toUpperCase() + w.slice(1)).join(' ')

/**
 * Multi-tenancy control: switches the active tenant that scopes every admin request, and lets the
 * operator add/remove tenants from their working set (persisted locally). A tenant becomes real on the
 * server once it has stubs; removing one here only drops it from the switcher — its stubs are untouched.
 */
export function TenantSwitcher({ collapsed }: { collapsed: boolean }) {
  const { t } = useTranslation()
  const { tenant, setTenant, tenants, addTenant, removeTenant } = useUi()
  const [draft, setDraft] = useState('')

  // Merge the operator's local working set with tenants discovered server-side (created via the API).
  // Local entries are removable; server-only ones are shown (they exist because they hold stubs).
  const server = useQuery({ queryKey: ['tenants'], queryFn: fetchTenants })
  const localIds = new Set(tenants.map((tn) => tn.id))
  const rows = [
    ...tenants.map((tn) => ({ id: tn.id, name: tn.name, local: true })),
    ...(server.data?.tenants ?? []).filter((id) => !localIds.has(id)).map((id) => ({ id, name: prettify(id), local: false })),
  ]
  const active = rows.find((r) => r.id === tenant) ?? rows[0]

  const add = () => { const name = draft.trim(); if (name) { addTenant(name); setDraft('') } }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button className={cn('flex w-full items-center gap-2.5 rounded-lg border border-border bg-background text-start transition-colors hover:bg-muted', collapsed ? 'justify-center p-1.5' : 'p-2')}>
          <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
            <Building2 className="size-4" />
          </span>
          {!collapsed && (
            <>
              <span className="min-w-0 flex-1">
                <span className="block truncate text-sm font-semibold leading-tight">{active.name}</span>
                <span className="block truncate text-xs leading-tight text-muted-foreground">{t('common.tenant')}</span>
              </span>
              <ChevronsUpDown className="size-4 shrink-0 text-faint" />
            </>
          )}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="start" className="w-[--radix-dropdown-menu-trigger-width] min-w-60">
        <DropdownMenuLabel>{t('common.switchTenant')}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {rows.map((tn) => (
          <DropdownMenuItem key={tn.id} onSelect={() => setTenant(tn.id)} className="group">
            <Building2 className="size-4 text-muted-foreground" />
            <span className="flex-1 truncate">{tn.name}</span>
            {tn.id === tenant ? (
              <Check className="size-4" />
            ) : tn.local && tenants.length > 1 ? (
              <button
                type="button"
                aria-label={t('common.remove')}
                // Remove on pointer-down, not click: Radix selects the menu item on pointer-up and
                // immediately unmounts the menu, so an onClick here never fires (the button is gone)
                // and only the item's onSelect runs — switching to the tenant instead of removing it.
                // Firing + swallowing the event on pointer-down removes it and blocks that selection.
                onPointerDown={(e) => { e.preventDefault(); e.stopPropagation(); removeTenant(tn.id) }}
                onClick={(e) => { e.preventDefault(); e.stopPropagation() }}
                className="rounded p-0.5 text-faint transition-colors hover:bg-danger-bg hover:text-danger"
              >
                <X className="size-3.5" />
              </button>
            ) : null}
          </DropdownMenuItem>
        ))}
        <DropdownMenuSeparator />
        <div className="flex items-center gap-1.5 p-1.5">
          <input
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            onKeyDown={(e) => { e.stopPropagation(); if (e.key === 'Enter') { e.preventDefault(); add() } }}
            placeholder={t('common.tenantName')}
            className="h-8 w-full rounded-md border border-border bg-background px-2 text-sm outline-none focus:border-border-strong"
          />
          <button
            type="button"
            onClick={add}
            disabled={!draft.trim()}
            aria-label={t('common.addTenant')}
            className="flex size-8 shrink-0 items-center justify-center rounded-md bg-primary text-primary-foreground transition-opacity hover:opacity-90 disabled:opacity-40"
          >
            <Plus className="size-4" />
          </button>
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
