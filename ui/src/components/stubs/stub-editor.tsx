import { useEffect, useState } from 'react'
import { useForm, useFieldArray, type Resolver } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import { Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { useUi } from '@/components/providers'
import { saveStub, type Stub } from '@/lib/api'
import { BODY_OPS, emptyStub, FAULTS, fromWireMock, MATCH_OPS, stubSchema, toJson, URL_MATCH, type StubForm } from '@/lib/stub-schema'
import { Sheet, SheetContent, SheetHeader } from '@/components/ui/sheet'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Input, Label, NativeSelect, Textarea } from '@/components/ui/field'
import { Switch } from '@/components/ui/switch'
import { Button } from '@/components/ui/button'

function seedFrom(stub: Stub | null): StubForm {
  if (!stub) return emptyStub
  // Prefer a full reverse-map of the mapping the host returned (no field is lost on edit); fall back to
  // the projected fields when only those are available (e.g. sample mode).
  if (stub.raw) return fromWireMock(stub.raw)
  return { ...emptyStub, method: stub.method === 'ANY' ? 'GET' : stub.method, urlValue: stub.url, priority: stub.priority, scenarioName: stub.scenario ?? '' }
}

export function StubEditor({ open, onOpenChange, editing, onSaved, initialTab = 'form' }: {
  open: boolean
  onOpenChange: (o: boolean) => void
  editing: Stub | null
  onSaved: () => void
  initialTab?: 'form' | 'json'
}) {
  const { t } = useTranslation()
  const { tenant } = useUi()
  const [tab, setTab] = useState('form')
  const [rawJson, setRawJson] = useState('')
  const [saving, setSaving] = useState(false)

  // zodResolver's inferred generic clashes with the coerce()'d number fields (and pnpm's duplicate RHF
  // types), so cast the resolver — the schema still validates at runtime.
  const form = useForm<StubForm>({ resolver: zodResolver(stubSchema) as unknown as Resolver<StubForm>, defaultValues: emptyStub })
  const { register, control, reset, getValues, watch, handleSubmit } = form

  useEffect(() => {
    if (open) {
      const seed = seedFrom(editing)
      reset(seed)
      // When editing, the JSON tab shows the exact mapping the host returned (so nothing is lost even
      // if the form doesn't surface a field); for a new stub it mirrors the form.
      setRawJson(editing?.raw ? JSON.stringify(editing.raw, null, 2) : toJson(seed))
      setTab(initialTab)
    }
  }, [open, editing, reset, initialTab])

  // Keep the JSON preview live while editing the form (form is the source of truth on the Form tab).
  useEffect(() => {
    const sub = watch((values) => { if (tab === 'form') setRawJson(toJson(values as StubForm)) })
    return () => sub.unsubscribe()
  }, [watch, tab])

  const headers = useFieldArray({ control, name: 'headers' })
  const bodyPatterns = useFieldArray({ control, name: 'bodyPatterns' })
  const responseHeaders = useFieldArray({ control, name: 'responseHeaders' })
  const webhookHeaders = useFieldArray({ control, name: 'webhookHeaders' })

  async function persist() {
    let json = rawJson
    if (tab === 'form') json = toJson(getValues())
    try { JSON.parse(json) } catch { toast.error(t('editor.invalidJson')); return }
    setSaving(true)
    const { mock } = await saveStub(tenant, json, editing?.id)
    setSaving(false)
    toast[mock ? 'message' : 'success'](mock ? t('editor.savedSample') : t('editor.saved'))
    onSaved()
    onOpenChange(false)
  }

  const importing = !editing && initialTab === 'json'
  const title = editing ? t('editor.editTitle') : importing ? t('editor.importTitle') : t('editor.newTitle')
  const description = importing ? t('editor.importDesc') : t('stubs.subtitle')

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent>
        <SheetHeader title={title} description={description} />

        <Tabs value={tab} onValueChange={setTab} className="flex min-h-0 flex-1 flex-col">
          <div className="px-6 pt-4">
            <TabsList>
              <TabsTrigger value="form">{t('editor.form')}</TabsTrigger>
              <TabsTrigger value="json">JSON</TabsTrigger>
            </TabsList>
          </div>

          <TabsContent value="form" className="scroll-area min-h-0 flex-1 space-y-6 overflow-y-auto px-6 py-5">
            {/* Request */}
            <Section title={t('editor.request')}>
              <div className="grid grid-cols-[110px_1fr] gap-3">
                <div><Label>{t('stubs.method')}</Label><NativeSelect {...register('method')}>{['GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS', 'ANY'].map((m) => <option key={m}>{m}</option>)}</NativeSelect></div>
                <div><Label>{t('editor.urlMatch')}</Label>
                  <div className="grid grid-cols-[130px_1fr] gap-2">
                    <NativeSelect {...register('urlMatchType')}>{URL_MATCH.map((u) => <option key={u} value={u}>{u}</option>)}</NativeSelect>
                    <Input {...register('urlValue')} placeholder="/api/v2/…" className="font-mono" />
                  </div>
                </div>
              </div>
              <Rows label={t('editor.headerMatchers')} fields={headers.fields} onAdd={() => headers.append({ name: '', operator: 'equalTo', value: '' })} onRemove={headers.remove}
                render={(i) => (<>
                  <Input {...register(`headers.${i}.name`)} placeholder="Header" />
                  <NativeSelect {...register(`headers.${i}.operator`)}>{MATCH_OPS.map((o) => <option key={o}>{o}</option>)}</NativeSelect>
                  <Input {...register(`headers.${i}.value`)} placeholder={t('editor.value')} />
                </>)} />
              <Rows label={t('editor.bodyMatchers')} fields={bodyPatterns.fields} onAdd={() => bodyPatterns.append({ operator: 'equalToJson', value: '' })} onRemove={bodyPatterns.remove}
                render={(i) => (<>
                  <NativeSelect {...register(`bodyPatterns.${i}.operator`)}>{BODY_OPS.map((o) => <option key={o}>{o}</option>)}</NativeSelect>
                  <Input {...register(`bodyPatterns.${i}.value`)} placeholder={t('editor.value')} className="font-mono" />
                </>)} twoCol />
            </Section>

            {/* Response */}
            <Section title={t('editor.response')}>
              <div className="grid grid-cols-2 gap-3">
                <div><Label>{t('editor.statusCode')}</Label><Input type="number" {...register('responseStatus')} /></div>
                <div><Label>{t('editor.priority')}</Label><Input type="number" {...register('priority')} /></div>
              </div>
              <Rows label={t('editor.responseHeaders')} fields={responseHeaders.fields} onAdd={() => responseHeaders.append({ name: '', value: '' })} onRemove={responseHeaders.remove}
                render={(i) => (<>
                  <Input {...register(`responseHeaders.${i}.name`)} placeholder="Header" />
                  <Input {...register(`responseHeaders.${i}.value`)} placeholder={t('editor.value')} />
                </>)} twoCol />
              <div><Label>{t('editor.body')}</Label><Textarea rows={5} {...register('responseBody')} className="font-mono text-[12.5px]" placeholder='{"ok": true}' /></div>
              <label className="flex items-center gap-2.5 text-sm">
                <Switch checked={watch('useTemplating')} onCheckedChange={(v) => form.setValue('useTemplating', v)} />
                {t('editor.templating')}
              </label>
            </Section>

            {/* Behavior */}
            <Section title={t('editor.behavior')}>
              <div className="grid grid-cols-2 gap-3">
                <div><Label>{t('editor.delay')}</Label><Input type="number" {...register('fixedDelayMs')} placeholder="0" /></div>
                <div><Label>{t('editor.fault')}</Label><NativeSelect {...register('fault')}>{FAULTS.map((f) => <option key={f} value={f}>{f || t('editor.none')}</option>)}</NativeSelect></div>
              </div>
              <div><Label>{t('editor.proxy')}</Label><Input {...register('proxyBaseUrl')} placeholder="https://upstream.example.com" className="font-mono" /></div>
              <div className="grid grid-cols-3 gap-3">
                <div><Label>{t('stubs.scenario')}</Label><Input {...register('scenarioName')} placeholder="Checkout" /></div>
                <div><Label>{t('editor.requiredState')}</Label><Input {...register('requiredScenarioState')} placeholder="Started" /></div>
                <div><Label>{t('editor.newState')}</Label><Input {...register('newScenarioState')} placeholder="Paid" /></div>
              </div>
            </Section>

            {/* Webhook / callback */}
            <Section title={t('editor.webhook')}>
              <p className="-mt-1 text-xs text-muted-foreground">{t('editor.webhookHint')}</p>
              <div className="grid grid-cols-[110px_1fr_120px] gap-3">
                <div><Label>{t('stubs.method')}</Label><NativeSelect {...register('webhookMethod')}>{['POST', 'PUT', 'GET', 'DELETE', 'PATCH'].map((m) => <option key={m}>{m}</option>)}</NativeSelect></div>
                <div><Label>{t('editor.webhookUrl')}</Label><Input {...register('webhookUrl')} placeholder="https://callback.example.com/hook" className="font-mono" /></div>
                <div><Label>{t('editor.delay')}</Label><Input type="number" {...register('webhookDelayMs')} placeholder="0" /></div>
              </div>
              <Rows label={t('editor.webhookHeaders')} fields={webhookHeaders.fields} onAdd={() => webhookHeaders.append({ name: '', value: '' })} onRemove={webhookHeaders.remove}
                render={(i) => (<>
                  <Input {...register(`webhookHeaders.${i}.name`)} placeholder="Header" />
                  <Input {...register(`webhookHeaders.${i}.value`)} placeholder={t('editor.value')} />
                </>)} twoCol />
              <div><Label>{t('editor.webhookBody')}</Label><Textarea rows={3} {...register('webhookBody')} className="font-mono text-[12.5px]" placeholder='{"event": "matched"}' /></div>
            </Section>
          </TabsContent>

          <TabsContent value="json" className="min-h-0 flex-1 px-6 py-5">
            <Textarea value={rawJson} onChange={(e) => setRawJson(e.target.value)} className="h-full min-h-[420px] font-mono text-[12.5px]" spellCheck={false} />
          </TabsContent>
        </Tabs>

        <div className="flex items-center justify-end gap-2 border-t border-border px-6 py-4">
          <Button variant="ghost" onClick={() => onOpenChange(false)}>{t('editor.cancel')}</Button>
          <Button variant="primary" onClick={handleSubmit(persist, () => setTab('form'))} disabled={saving}>{t('editor.save')}</Button>
        </div>
      </SheetContent>
    </Sheet>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-3">
      <h3 className="text-xs font-semibold uppercase tracking-wide text-faint">{title}</h3>
      {children}
    </section>
  )
}

function Rows({ label, fields, onAdd, onRemove, render, twoCol }: {
  label: string
  fields: { id: string }[]
  onAdd: () => void
  onRemove: (i: number) => void
  render: (i: number) => React.ReactNode
  twoCol?: boolean
}) {
  return (
    <div>
      <div className="mb-1.5 flex items-center justify-between">
        <Label className="mb-0">{label}</Label>
        <Button type="button" variant="ghost" size="iconSm" onClick={onAdd} aria-label="Add"><Plus /></Button>
      </div>
      <div className="space-y-2">
        {fields.map((f, i) => (
          <div key={f.id} className={`grid ${twoCol ? 'grid-cols-[minmax(0,1fr)_minmax(0,2fr)_auto]' : 'grid-cols-[minmax(0,1fr)_130px_minmax(0,1fr)_auto]'} items-center gap-2`}>
            {render(i)}
            <Button type="button" variant="ghost" size="iconSm" onClick={() => onRemove(i)} className="text-muted-foreground"><Trash2 /></Button>
          </div>
        ))}
        {fields.length === 0 && <p className="text-xs text-faint">—</p>}
      </div>
    </div>
  )
}
