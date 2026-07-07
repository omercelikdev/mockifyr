import { useTranslation } from 'react-i18next'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { RotateCcw, Waypoints } from 'lucide-react'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { useUi } from '@/components/providers'
import { fetchScenarios, resetScenarios, setScenarioState, type Scenario } from '@/lib/api'
import { Button } from '@/components/ui/button'

export function ScenariosPage() {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const queryClient = useQueryClient()
  const { data, isLoading } = useQuery({ queryKey: ['scenarios', tenant], queryFn: () => fetchScenarios(tenant) })
  const refresh = () => void queryClient.invalidateQueries({ queryKey: ['scenarios', tenant] })

  const setState = useMutation({
    mutationFn: ({ name, state }: { name: string; state: string }) => setScenarioState(tenant, name, state),
    onSuccess: ({ mock }) => { toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('scenarios.stateSet')); refresh() },
  })
  const resetAll = useMutation({
    mutationFn: () => resetScenarios(tenant),
    onSuccess: ({ mock }) => { toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('scenarios.reset')); refresh() },
  })

  const scenarios = data?.scenarios ?? []

  return (
    <div className="mx-auto max-w-[1360px]">
      <header className="mb-6 flex items-start gap-4">
        <div>
          <h1 className="text-[22px] font-bold tracking-tight">{t('nav.scenarios')}</h1>
          <p className="mt-1 max-w-[62ch] text-sm text-muted-foreground">{t('scenarios.subtitle')}</p>
        </div>
        <Button variant="outline" className="ms-auto" onClick={() => resetAll.mutate()} disabled={resetAll.isPending}>
          <RotateCcw />{t('scenarios.resetAll')}
        </Button>
      </header>

      {data?.mock && (
        <div className="mb-4 inline-flex items-center gap-1.5 rounded-full border border-warning-border bg-warning-bg px-2.5 py-0.5 text-[11.5px] font-medium text-warning">
          {t('stubs.sample')}
        </div>
      )}

      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => <div key={i} className="h-40 animate-pulse rounded-2xl bg-muted" />)}
        </div>
      ) : scenarios.length === 0 ? (
        <div className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border bg-muted/40 px-6 py-20 text-center">
          <Waypoints className="size-8 text-faint" />
          <p className="text-sm text-muted-foreground">{t('scenarios.empty')}</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {scenarios.map((s) => (
            <ScenarioCard key={s.name} scenario={s} onSet={(state) => setState.mutate({ name: s.name, state })} />
          ))}
        </div>
      )}
    </div>
  )
}

function ScenarioCard({ scenario, onSet }: { scenario: Scenario; onSet: (state: string) => void }) {
  const { t } = useTranslation()
  return (
    <div className="rounded-2xl border border-border bg-background p-5 shadow-surface">
      <div className="flex items-center gap-2.5">
        <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-muted-foreground">
          <Waypoints className="size-4" />
        </span>
        <div className="min-w-0">
          <div className="truncate font-semibold">{scenario.name}</div>
          <div className="text-xs text-muted-foreground">{scenario.possibleStates.length} {t('scenarios.states')}</div>
        </div>
      </div>

      <div className="mt-4">
        <div className="mb-2 text-[11px] font-semibold uppercase tracking-wide text-faint">{t('scenarios.currentState')}</div>
        <div className="flex flex-wrap gap-1.5">
          {scenario.possibleStates.map((state) => {
            const active = state === scenario.state
            return (
              <button
                key={state}
                onClick={() => !active && onSet(state)}
                aria-pressed={active}
                className={cn(
                  'rounded-full border px-3 py-1 text-xs font-semibold transition-colors',
                  active
                    ? 'border-primary bg-primary text-primary-foreground'
                    : 'border-border bg-background text-muted-foreground hover:border-border-strong hover:text-foreground',
                )}
              >
                {state}
              </button>
            )
          })}
        </div>
      </div>
    </div>
  )
}
