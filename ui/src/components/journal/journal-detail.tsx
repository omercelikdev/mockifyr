import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fetchJournalDetail, type HeaderPair, type JournalWebhook } from '@/lib/api'
import { Sheet, SheetContent } from '@/components/ui/sheet'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { MethodChip } from '@/components/ui/badges'
import { cn } from '@/lib/utils'

// Pretty-print a body when it parses as JSON; otherwise show it verbatim.
function pretty(body: string): string {
  if (!body) return ''
  try { return JSON.stringify(JSON.parse(body), null, 2) } catch { return body }
}

function statusTone(status: number): string {
  if (status >= 500) return 'text-danger bg-danger-bg border-danger-border'
  if (status >= 400) return 'text-warning bg-warning-bg border-warning-border'
  return 'text-success bg-success-bg border-success-border'
}

function Headers({ headers, label }: { headers: HeaderPair[]; label: string }) {
  return (
    <div>
      <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-faint">{label}</h4>
      {headers.length === 0 ? (
        <p className="text-xs text-faint">—</p>
      ) : (
        <dl className="overflow-hidden rounded-lg border border-border">
          {headers.map((h, i) => (
            <div key={i} className={cn('grid grid-cols-[minmax(120px,220px)_1fr] gap-3 px-3 py-1.5 text-[12.5px]', i > 0 && 'border-t border-border')}>
              <dt className="truncate font-medium text-muted-foreground">{h.name}</dt>
              <dd className="break-all font-mono text-foreground">{h.value}</dd>
            </div>
          ))}
        </dl>
      )}
    </div>
  )
}

function Body({ body, label, empty }: { body: string; label: string; empty: string }) {
  return (
    <div>
      <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-faint">{label}</h4>
      {body ? (
        <pre className="scroll-area max-h-[340px] overflow-auto rounded-lg border border-border bg-muted/40 p-3 font-mono text-[12.5px] leading-relaxed text-foreground">{pretty(body)}</pre>
      ) : (
        <p className="text-xs text-faint">{empty}</p>
      )}
    </div>
  )
}

function WebhookCard({ webhook, t }: { webhook: JournalWebhook; t: (k: string) => string }) {
  return (
    <div className="space-y-4 rounded-xl border border-border p-4">
      <div className="flex items-center gap-2">
        <MethodChip method={webhook.method} />
        <span className="break-all font-mono text-[12.5px] text-foreground">{webhook.url}</span>
      </div>
      <Headers headers={webhook.headers} label={t('journal.headers')} />
      <Body body={webhook.body ?? ''} label={t('journal.body')} empty={t('journal.noBody')} />
    </div>
  )
}

/**
 * Slide-over detail for one journal entry: Request / Response / Callback tabs with headers + bodies
 * (#122). Opens when `id` is set; the detail is fetched on demand so the list stays lean.
 */
export function JournalDetailSheet({ id, tenant, onClose }: { id: string | null; tenant: string; onClose: () => void }) {
  const { t } = useTranslation()
  const { data, isLoading } = useQuery({
    queryKey: ['journal-detail', tenant, id],
    queryFn: () => fetchJournalDetail(tenant, id!),
    enabled: !!id,
  })

  return (
    <Sheet open={!!id} onOpenChange={(o) => { if (!o) onClose() }}>
      <SheetContent className="max-w-[720px]">
        {isLoading || !data ? (
          <div className="space-y-3 p-6">{Array.from({ length: 6 }).map((_, i) => <div key={i} className="h-6 animate-pulse rounded bg-muted" />)}</div>
        ) : (
          <>
            <div className="flex items-center gap-2.5 border-b border-border px-6 py-4 pe-14">
              <MethodChip method={data.request.method} />
              <span className="min-w-0 flex-1 truncate font-mono text-[13px] font-medium">{data.request.url}</span>
              {data.response && (
                <span className={cn('inline-flex shrink-0 rounded-md border px-2 py-0.5 font-mono text-[11px] font-bold', statusTone(data.response.status))}>{data.response.status}</span>
              )}
            </div>
            <Tabs defaultValue="request" className="flex min-h-0 flex-1 flex-col">
              <div className="px-6 pt-4">
                <TabsList>
                  <TabsTrigger value="request">{t('journal.tabRequest')}</TabsTrigger>
                  <TabsTrigger value="response">{t('journal.tabResponse')}</TabsTrigger>
                  <TabsTrigger value="webhook">{t('journal.tabCallback')}{data.webhooks.length > 0 ? ` (${data.webhooks.length})` : ''}</TabsTrigger>
                </TabsList>
              </div>

              <TabsContent value="request" className="scroll-area min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-5">
                <Headers headers={data.request.headers} label={t('journal.headers')} />
                <Body body={data.request.body} label={t('journal.body')} empty={t('journal.noBody')} />
              </TabsContent>

              <TabsContent value="response" className="scroll-area min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-5">
                {data.response ? (
                  <>
                    <Headers headers={data.response.headers} label={t('journal.headers')} />
                    <Body body={data.response.body} label={t('journal.body')} empty={t('journal.noBody')} />
                  </>
                ) : (
                  <p className="text-sm text-muted-foreground">{t('journal.noResponse')}</p>
                )}
              </TabsContent>

              <TabsContent value="webhook" className="scroll-area min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-5">
                {data.webhooks.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t('journal.noCallback')}</p>
                ) : (
                  data.webhooks.map((w, i) => <WebhookCard key={i} webhook={w} t={t} />)
                )}
              </TabsContent>
            </Tabs>
          </>
        )}
      </SheetContent>
    </Sheet>
  )
}
