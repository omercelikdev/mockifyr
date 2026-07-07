import { Building2, Check, ChevronsUpDown } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { TENANTS } from '@/lib/tenants'
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel,
  DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

/** Multi-tenancy control: switches the active tenant that scopes every admin request. */
export function TenantSwitcher({ collapsed }: { collapsed: boolean }) {
  const { t } = useTranslation()
  const { tenant, setTenant } = useUi()
  const active = TENANTS.find((tn) => tn.id === tenant) ?? TENANTS[0]

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
      <DropdownMenuContent side="top" align="start" className="w-[--radix-dropdown-menu-trigger-width] min-w-56">
        <DropdownMenuLabel>{t('common.switchTenant')}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        {TENANTS.map((tn) => (
          <DropdownMenuItem key={tn.id} onSelect={() => setTenant(tn.id)}>
            <Building2 className="size-4 text-muted-foreground" />
            {tn.name}
            {tn.id === tenant && <Check className="ms-auto size-4" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
