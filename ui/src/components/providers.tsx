import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { applyLocale, LOCALES, type LocaleCode } from '@/lib/i18n'
import { TENANTS, type Tenant } from '@/lib/tenants'

// Small app-wide UI state: sidebar collapse, theme (light/dark), locale, and the active tenant — each
// persisted so a reload keeps the operator's context. Deliberately tiny; server state (stubs, journal…)
// is fetched separately (TanStack Query), scoped by the active tenant.

type Theme = 'light' | 'dark'

interface UiState {
  collapsed: boolean
  toggleCollapsed: () => void
  theme: Theme
  setTheme: (t: Theme) => void
  locale: LocaleCode
  setLocale: (c: LocaleCode) => void
  tenant: string
  setTenant: (id: string) => void
  tenants: Tenant[]
  addTenant: (name: string) => void
  removeTenant: (id: string) => void
}

// The engine's tenants are implicit — one materializes server-side once it has stubs. The switcher is
// the operator's working set: seeded, then editable and persisted locally. Add = start working under a
// new tenant (create a stub to make it real); remove = drop it from the set (its stubs are untouched).
function readTenants(): Tenant[] {
  try {
    const stored = JSON.parse(localStorage.getItem('ui.tenants') ?? 'null') as Tenant[] | null
    if (Array.isArray(stored) && stored.length > 0 && stored.every((t) => t?.id && t?.name)) return stored
  } catch { /* fall through to the seed */ }
  return TENANTS
}

function slugify(name: string): string {
  return name.trim().toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '')
}

const UiContext = createContext<UiState | null>(null)

function readStored<T extends string>(key: string, fallback: T): T {
  return (localStorage.getItem(key) as T | null) ?? fallback
}

export function UiProvider({ children }: { children: React.ReactNode }) {
  const [collapsed, setCollapsed] = useState(() => localStorage.getItem('ui.collapsed') === '1')
  const [theme, setThemeState] = useState<Theme>(() => readStored<Theme>('ui.theme', 'light'))
  const [locale, setLocaleState] = useState<LocaleCode>(() => {
    const stored = readStored<LocaleCode>('ui.locale', 'en')
    return LOCALES.some((l) => l.code === stored) ? stored : 'en'
  })
  const [tenants, setTenants] = useState<Tenant[]>(readTenants)
  const [tenant, setTenantState] = useState<string>(() => {
    const list = readTenants()
    const stored = localStorage.getItem('ui.tenant') ?? list[0].id
    return list.some((tn) => tn.id === stored) ? stored : list[0].id
  })

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark')
    localStorage.setItem('ui.theme', theme)
  }, [theme])

  useEffect(() => {
    applyLocale(locale)
    localStorage.setItem('ui.locale', locale)
  }, [locale])

  const toggleCollapsed = useCallback(() => {
    setCollapsed((c) => {
      localStorage.setItem('ui.collapsed', c ? '0' : '1')
      return !c
    })
  }, [])

  const setTenant = useCallback((id: string) => {
    localStorage.setItem('ui.tenant', id)
    setTenantState(id)
  }, [])

  const addTenant = useCallback((name: string) => {
    const id = slugify(name)
    if (!id) return
    setTenants((current) => {
      if (current.some((t) => t.id === id)) { setTenant(id); return current }
      const next = [...current, { id, name: name.trim() }]
      localStorage.setItem('ui.tenants', JSON.stringify(next))
      setTenant(id)
      return next
    })
  }, [setTenant])

  const removeTenant = useCallback((id: string) => {
    setTenants((current) => {
      if (current.length <= 1) return current // keep at least one
      const next = current.filter((t) => t.id !== id)
      localStorage.setItem('ui.tenants', JSON.stringify(next))
      setTenantState((active) => (active === id ? next[0].id : active))
      if (localStorage.getItem('ui.tenant') === id) localStorage.setItem('ui.tenant', next[0].id)
      return next
    })
  }, [])

  const value = useMemo<UiState>(
    () => ({ collapsed, toggleCollapsed, theme, setTheme: setThemeState, locale, setLocale: setLocaleState, tenant, setTenant, tenants, addTenant, removeTenant }),
    [collapsed, toggleCollapsed, theme, locale, tenant, setTenant, tenants, addTenant, removeTenant],
  )

  return <UiContext value={value}>{children}</UiContext>
}

export function useUi() {
  const ctx = useContext(UiContext)
  if (!ctx) throw new Error('useUi must be used within UiProvider')
  return ctx
}
