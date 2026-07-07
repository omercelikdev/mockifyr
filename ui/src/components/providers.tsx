import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import { applyLocale, LOCALES, type LocaleCode } from '@/lib/i18n'

// Small app-wide UI state: sidebar collapse, theme (light/dark), and locale — each persisted so a
// reload keeps the operator's choices. Deliberately tiny; server state (stubs, journal…) is separate.

type Theme = 'light' | 'dark'

interface UiState {
  collapsed: boolean
  toggleCollapsed: () => void
  theme: Theme
  setTheme: (t: Theme) => void
  locale: LocaleCode
  setLocale: (c: LocaleCode) => void
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

  const value = useMemo<UiState>(
    () => ({ collapsed, toggleCollapsed, theme, setTheme: setThemeState, locale, setLocale: setLocaleState }),
    [collapsed, toggleCollapsed, theme, locale],
  )

  return <UiContext value={value}>{children}</UiContext>
}

export function useUi() {
  const ctx = useContext(UiContext)
  if (!ctx) throw new Error('useUi must be used within UiProvider')
  return ctx
}
