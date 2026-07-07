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
  return mapping
}

export function toJson(f: StubForm): string {
  return JSON.stringify(toWireMock(f), null, 2)
}

function matcherMap(rows: StubForm['headers']): Record<string, unknown> | null {
  const entries = rows.filter((r) => r.name.trim())
  if (!entries.length) return null
  return Object.fromEntries(entries.map((r) => [r.name, { [r.operator]: r.value }]))
}

function tryJson(v: string): unknown {
  try { return JSON.parse(v) } catch { return v }
}
