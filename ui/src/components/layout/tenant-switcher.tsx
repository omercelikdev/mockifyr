import { useState } from 'react'
import { Building2, Check, ChevronsUpDown, Plus, X } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

/**
 * Multi-tenancy control: switches the active tenant that scopes every admin request, and lets the
 * operator add/remove tenants from their working set (persisted locally). A tenant becomes real on the
 * server once it has stubs; removing one here only drops it from the switcher — its stubs are untouched.
 */
export function TenantSwitcher({ collapsed }: { collapsed: boolean }) {
  const { t } = useTranslation()
  const { tenant, setTenant, tenants, addTenant, removeTenant } = useUi()
  const active = tenants.find((tn) => tn.id === tenant) ?? tenants[0]
  const [draft, setDraft] = useState('')

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
        {tenants.map((tn) => (
          <DropdownMenuItem key={tn.id} onSelect={() => setTenant(tn.id)} className="group">
            <Building2 className="size-4 text-muted-foreground" />
            <span className="flex-1 truncate">{tn.name}</span>
            {tn.id === tenant ? (
              <Check className="size-4" />
            ) : tenants.length > 1 ? (
              <button
                type="button"
                aria-label={t('common.remove')}
                onPointerDown={(e) => e.stopPropagation()}
                onClick={(e) => { e.preventDefault(); e.stopPropagation(); removeTenant(tn.id) }}
                className="rounded p-0.5 text-faint opacity-0 transition-colors hover:bg-muted hover:text-danger group-data-[highlighted]:opacity-100"
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
