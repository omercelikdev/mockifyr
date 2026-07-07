import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { applyLocale, LOCALES, type LocaleCode } from '@/lib/i18n'
import { TENANTS } from '@/lib/tenants'

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
  const [tenant, setTenantState] = useState<string>(() => {
    const stored = localStorage.getItem('ui.tenant') ?? TENANTS[0].id
    return TENANTS.some((tn) => tn.id === stored) ? stored : TENANTS[0].id
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

  const value = useMemo<UiState>(
    () => ({ collapsed, toggleCollapsed, theme, setTheme: setThemeState, locale, setLocale: setLocaleState, tenant, setTenant }),
    [collapsed, toggleCollapsed, theme, locale, tenant, setTenant],
  )

  return <UiContext value={value}>{children}</UiContext>
}

export function useUi() {
  const ctx = useContext(UiContext)
  if (!ctx) throw new Error('useUi must be used within UiProvider')
  return ctx
}
