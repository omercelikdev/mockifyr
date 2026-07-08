import { z } from 'zod'

// The stub editor's form model — a friendly projection of a WireMock mapping. It intentionally covers
// the common surface (URL/method/header/query/body matchers, response + templating, delay, fault,
// scenario, priority, proxy); the raw-JSON tab is the escape hatch for anything beyond it.

export const MATCH_OPS = ['equalTo', 'contains', 'matches', 'doesNotMatch'] as const
export const BODY_OPS = ['equalTo', 'equalToJson', 'matchesJsonPath', 'matchesXPath', 'contains', 'matches'] as const
export const URL_MATCH = ['urlPath', 'url', 'urlPathPattern', 'urlPattern'] as const
export const FAULTS = ['', 'EMPTY_RESPONSE', 'MALFORMED_RESPONSE_CHUNK', 'RANDOM_DATA_THEN_CLOSE', 'CONNECTION_RESET_BY_PEER'] as const

const kvMatcher = z.object({
  name: z.string(),
  operator: z.enum(MATCH_OPS),
  value: z.string(),
})

export const stubSchema = z.object({
  method: z.string().min(1),
  urlMatchType: z.enum(URL_MATCH),
  urlValue: z.string().min(1, 'URL is required'),
  headers: z.array(kvMatcher),
  queryParams: z.array(kvMatcher),
  bodyPatterns: z.array(z.object({ operator: z.enum(BODY_OPS), value: z.string() })),
  priority: z.coerce.number().int().min(1).max(255),
  scenarioName: z.string(),
  requiredScenarioState: z.string(),
  newScenarioState: z.string(),
  responseStatus: z.coerce.number().int().min(100).max(599),
  responseHeaders: z.array(z.object({ name: z.string(), value: z.string() })),
  responseBody: z.string(),
  useTemplating: z.boolean(),
  fixedDelayMs: z.string(),
  fault: z.enum(FAULTS),
  proxyBaseUrl: z.string(),
  webhookMethod: z.string(),
  webhookUrl: z.string(),
  webhookBody: z.string(),
  webhookHeaders: z.array(z.object({ name: z.string(), value: z.string() })),
  webhookDelayMs: z.string(),
})

export type StubForm = z.infer<typeof stubSchema>

export const emptyStub: StubForm = {
  method: 'GET',
  urlMatchType: 'urlPath',
  urlValue: '/',
  headers: [],
  queryParams: [],
  bodyPatterns: [],
  priority: 5,
  scenarioName: '',
  requiredScenarioState: '',
  newScenarioState: '',
  responseStatus: 200,
  responseHeaders: [{ name: 'Content-Type', value: 'application/json' }],
  responseBody: '',
  useTemplating: false,
  fixedDelayMs: '',
  fault: '',
  proxyBaseUrl: '',
  webhookMethod: 'POST',
  webhookUrl: '',
  webhookBody: '',
  webhookHeaders: [],
  webhookDelayMs: '',
}

/** Build the WireMock mapping object from the form (unset fields are omitted). */
export function toWireMock(f: StubForm): Record<string, unknown> {
  const request: Record<string, unknown> = { method: f.method, [f.urlMatchType]: f.urlValue }
  const headerMap = matcherMap(f.headers)
  if (headerMap) request.headers = headerMap
  const queryMap = matcherMap(f.queryParams)
  if (queryMap) request.queryParameters = queryMap
  if (f.bodyPatterns.length) request.bodyPatterns = f.bodyPatterns.map((b) => ({ [b.operator]: b.operator === 'equalToJson' ? tryJson(b.value) : b.value }))

  const response: Record<string, unknown> = {}
  if (f.proxyBaseUrl.trim()) {
    response.proxyBaseUrl = f.proxyBaseUrl.trim()
  } else {
    response.status = f.responseStatus
    if (f.responseHeaders.length) response.headers = Object.fromEntries(f.responseHeaders.filter((h) => h.name).map((h) => [h.name, h.value]))
    if (f.responseBody) response.body = f.responseBody
    if (f.useTemplating) response.transformers = ['response-template']
  }
  if (f.fixedDelayMs.trim()) response.fixedDelayMilliseconds = Number(f.fixedDelayMs)
  if (f.fault) response.fault = f.fault

  const mapping: Record<string, unknown> = { request, response, priority: f.priority }
  if (f.scenarioName.trim()) {
    mapping.scenarioName = f.scenarioName.trim()
    if (f.requiredScenarioState.trim()) mapping.requiredScenarioState = f.requiredScenarioState.trim()
    if (f.newScenarioState.trim()) mapping.newScenarioState = f.newScenarioState.trim()
  }
  // Webhook / callback: fire an outbound request after a match (WireMock postServeActions). The JSON tab
  // remains the escape hatch for advanced options (headers, delay, multiple webhooks).
  if (f.webhookUrl.trim()) {
    const parameters: Record<string, unknown> = { method: f.webhookMethod || 'POST', url: f.webhookUrl.trim() }
    const webhookHeaders = Object.fromEntries(f.webhookHeaders.filter((h) => h.name.trim()).map((h) => [h.name, h.value]))
    if (Object.keys(webhookHeaders).length) parameters.headers = webhookHeaders
    if (f.webhookBody.trim()) parameters.body = f.webhookBody
    if (f.webhookDelayMs.trim()) parameters.delay = { type: 'fixed', milliseconds: Number(f.webhookDelayMs) }
    mapping.postServeActions = [{ name: 'webhook', parameters }]
  }
  return mapping
}

export function toJson(f: StubForm): string {
  return JSON.stringify(toWireMock(f), null, 2)
}

const obj = (v: unknown): Record<string, unknown> => (v && typeof v === 'object' && !Array.isArray(v) ? (v as Record<string, unknown>) : {})
const str = (v: unknown, fallback = ''): string => (typeof v === 'string' ? v : fallback)

/** Reverse of {@link toWireMock}: seed the editor form from an existing WireMock mapping (edit round-trip). */
export function fromWireMock(mapping: Record<string, unknown>): StubForm {
  const req = obj(mapping.request)
  const res = obj(mapping.response)

  let urlMatchType: StubForm['urlMatchType'] = 'urlPath'
  let urlValue = '/'
  for (const t of URL_MATCH) {
    if (typeof req[t] === 'string') { urlMatchType = t; urlValue = req[t] as string; break }
  }

  const parseMatchers = (source: unknown): StubForm['headers'] =>
    Object.entries(obj(source)).map(([name, m]) => {
      const entry = obj(m)
      const operator = MATCH_OPS.find((o) => o in entry) ?? 'equalTo'
      return { name, operator, value: str(entry[operator]) }
    })

  const bodyPatterns: StubForm['bodyPatterns'] = Array.isArray(req.bodyPatterns)
    ? (req.bodyPatterns as unknown[]).map((p) => {
        const entry = obj(p)
        const operator = BODY_OPS.find((o) => o in entry) ?? 'equalToJson'
        const v = entry[operator]
        return { operator, value: typeof v === 'string' ? v : JSON.stringify(v ?? '', null, 2) }
      })
    : []

  const wh = obj((Array.isArray(mapping.postServeActions) ? mapping.postServeActions : []).map(obj).find((a) => a.name === 'webhook'))
  const whParams = obj(wh.parameters)
  const whDelay = obj(whParams.delay)

  return {
    method: str(req.method, 'GET'),
    urlMatchType,
    urlValue,
    headers: parseMatchers(req.headers),
    queryParams: parseMatchers(req.queryParameters),
    bodyPatterns,
    priority: typeof mapping.priority === 'number' ? mapping.priority : 5,
    scenarioName: str(mapping.scenarioName),
    requiredScenarioState: str(mapping.requiredScenarioState),
    newScenarioState: str(mapping.newScenarioState),
    responseStatus: typeof res.status === 'number' ? res.status : 200,
    responseHeaders: Object.entries(obj(res.headers)).map(([name, value]) => ({ name, value: str(value) })),
    responseBody: typeof res.body === 'string' ? res.body : res.jsonBody !== undefined ? JSON.stringify(res.jsonBody, null, 2) : '',
    useTemplating: Array.isArray(res.transformers) && (res.transformers as unknown[]).includes('response-template'),
    fixedDelayMs: typeof res.fixedDelayMilliseconds === 'number' ? String(res.fixedDelayMilliseconds) : '',
    fault: (FAULTS as readonly string[]).includes(str(res.fault)) ? (str(res.fault) as StubForm['fault']) : '',
    proxyBaseUrl: str(res.proxyBaseUrl),
    webhookMethod: str(whParams.method, 'POST'),
    webhookUrl: str(whParams.url),
    webhookBody: str(whParams.body),
    webhookHeaders: Object.entries(obj(whParams.headers)).map(([name, value]) => ({ name, value: str(value) })),
    webhookDelayMs: typeof whDelay.milliseconds === 'number' ? String(whDelay.milliseconds) : '',
  }
}

function matcherMap(rows: StubForm['headers']): Record<string, unknown> | null {
  const entries = rows.filter((r) => r.name.trim())
  if (!entries.length) return null
  return Object.fromEntries(entries.map((r) => [r.name, { [r.operator]: r.value }]))
}

function tryJson(v: string): unknown {
  try { return JSON.parse(v) } catch { return v }
}
