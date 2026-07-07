import { useTranslation } from 'react-i18next'
import { Construction } from 'lucide-react'

/** A friendly stand-in for routes landing in later phases (Stubs, Journal, …). */
export function PlaceholderPage({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation()
  return (
    <div className="mx-auto flex max-w-[1360px] flex-col">
      <h1 className="text-[22px] font-bold tracking-tight">{t(titleKey)}</h1>
      <div className="mt-6 flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/40 px-6 py-20 text-center">
        <Construction className="size-8 text-faint" />
        <p className="text-sm font-medium text-muted-foreground">{t(titleKey)}</p>
        <p className="max-w-sm text-sm text-faint">This screen arrives in a later phase. The shell, theming, i18n and navigation are live.</p>
      </div>
    </div>
  )
}
