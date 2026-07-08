import { TENANT_HEADER } from '@/lib/tenants'

// A stub row as the dashboard needs it — a flat projection of a WireMock-format mapping.
export type Protocol = 'http' | 'grpc' | 'graphql' | 'websocket'
export type StubStatus = 'live' | 'proxy' | 'draft'

export interface Stub {
  id: string
  method: string
  url: string
  protocol: Protocol
  priority: number
  scenario: string | null
  persistence: string
  lastMatched: string | null
  status: StubStatus
  /** The full WireMock mapping (when a host returned it), so the editor can round-trip an edit. */
  raw?: Record<string, unknown>
}

// Admin Basic-auth credentials (base64 of user:pass), stored locally when the host requires auth. The
// host only enforces this when started with --admin-user/--admin-pass; otherwise there is no login.
const AUTH_KEY = 'ui.adminAuth'
export const hasAdminAuth = () => !!localStorage.getItem(AUTH_KEY)
export const setAdminAuth = (user: string, pass: string) => localStorage.setItem(AUTH_KEY, btoa(`${user}:${pass}`))
export const clearAdminAuth = () => localStorage.removeItem(AUTH_KEY)

/**
 * Probes the admin surface with the given credentials and, when accepted, stores them. Used by the
 * login screen so it can show an inline error instead of silently re-triggering the gate. A network
 * error (no host reachable) is treated as "not an auth failure" — the credentials are stored and the
 * regular fetch path will surface any real 401 later.
 */
export async function verifyAdminAuth(user: string, pass: string): Promise<boolean> {
  const token = btoa(`${user}:${pass}`)
  try {
    const res = await fetch('/__admin/mappings', { headers: { Authorization: `Basic ${token}`, [TENANT_HEADER]: 'default' } })
    if (res.status === 401) return false
  } catch {
    // Unreachable host is not an authentication failure; fall through and store optimistically.
  }
  localStorage.setItem(AUTH_KEY, token)
  return true
}

/** Low-level admin fetch: scopes every call to the active tenant, and attaches admin auth when present. */
async function adminFetch(path: string, tenant: string, init?: RequestInit): Promise<Response> {
  const auth = localStorage.getItem(AUTH_KEY)
  const res = await fetch(`/__admin${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      [TENANT_HEADER]: tenant,
      ...(auth ? { Authorization: `Basic ${auth}` } : {}),
      ...init?.headers,
    },
  })
  // A 401 means the host requires admin auth; let the app surface the login screen.
  if (res.status === 401) window.dispatchEvent(new Event('mockifyr-auth-required'))
  return res
}

/**
 * Loads the tenant's stubs. Talks to GET /__admin/mappings when a host is reachable; when it isn't
 * (design-time / no server), it falls back to representative sample data so the dashboard is always
 * explorable. The `mock` flag lets the UI show a "sample data" hint.
 */
export async function fetchStubs(tenant: string): Promise<{ stubs: Stub[]; mock: boolean }> {
  try {
    const res = await adminFetch('/mappings', tenant)
    if (!res.ok) throw new Error(String(res.status))
    const body = (await res.json()) as { mappings?: WireMockMapping[] }
    return { stubs: (body.mappings ?? []).map(projectMapping), mock: false }
  } catch {
    return { stubs: sampleStubs(tenant), mock: true }
  }
}

interface WireMockMapping {
  id?: string
  uuid?: string
  priority?: number
  scenarioName?: string
  request?: { method?: string; url?: string; urlPath?: string; urlPattern?: string; urlPathPattern?: string }
  response?: { proxyBaseUrl?: string }
  metadata?: { 'mockifyr:persistence'?: string }
}

function projectMapping(m: WireMockMapping): Stub {
  const req = m.request ?? {}
  const url = req.url ?? req.urlPath ?? req.urlPattern ?? req.urlPathPattern ?? '/'
  return {
    id: m.id ?? m.uuid ?? crypto.randomUUID(),
    method: (typeof req.method === 'string' ? req.method : 'ANY').toUpperCase(),
    url,
    protocol: url.includes('/grpc') ? 'grpc' : url.includes('graphql') ? 'graphql' : 'http',
    priority: m.priority ?? 5,
    scenario: m.scenarioName ?? null,
    persistence: m.metadata?.['mockifyr:persistence'] ?? 'In-memory',
    lastMatched: null,
    status: m.response?.proxyBaseUrl ? 'proxy' : 'live',
    raw: m as unknown as Record<string, unknown>,
  }
}

/**
 * Persists a stub (WireMock mapping JSON). Create = POST, update = PUT /__admin/mappings/{id}.
 * Returns `mock: true` when no host answered, so the caller can toast "sample mode" instead of failing.
 */
export async function saveStub(tenant: string, mappingJson: string, id?: string): Promise<{ mock: boolean }> {
  try {
    const res = await adminFetch(id ? `/mappings/${id}` : '/mappings', tenant, {
      method: id ? 'PUT' : 'POST',
      body: mappingJson,
    })
    if (!res.ok) throw new Error(String(res.status))
    return { mock: false }
  } catch {
    return { mock: true }
  }
}

/** Tenants that exist server-side (materialized once they have stubs). Empty in sample mode. */
export async function fetchTenants(): Promise<{ tenants: string[]; mock: boolean }> {
  try {
    const res = await adminFetch('/tenants', 'default')
    if (!res.ok) throw new Error(String(res.status))
    const body = (await res.json()) as { tenants?: string[] }
    return { tenants: body.tenants ?? [], mock: false }
  } catch {
    return { tenants: [], mock: true }
  }
}

// Host status for the Settings/Status screen.
export interface Health {
  name: string
  version: string
  persistence: string
  tenants: number
  totalStubs: number
}

const PERSISTENCE_LABEL: Record<string, string> = {
  NullStubPersistence: 'In-memory (ephemeral)',
  FileSystemStubPersistence: 'File-based (JSON)',
  LiteDbStubPersistence: 'LiteDB (embedded)',
  PostgresStubPersistence: 'PostgreSQL',
  RedisStubPersistence: 'Redis',
}

/** Friendly label for a persistence provider type name. */
export const persistenceLabel = (name: string) => PERSISTENCE_LABEL[name] ?? name

export async function fetchHealth(tenant: string): Promise<{ health: Health; mock: boolean }> {
  try {
    const res = await adminFetch('/health', tenant)
    if (!res.ok) throw new Error(String(res.status))
    return { health: (await res.json()) as Health, mock: false }
  } catch {
    return { health: { name: 'Mockifyr', version: '1.0', persistence: 'NullStubPersistence', tenants: 3, totalStubs: 248 }, mock: true }
  }
}

// Record-through-proxy session (G8/G9). Not tenant-scoped — the recording session is global.
export type RecordingStatus = 'Recording' | 'Stopped'
export interface CapturedStub { method: string; url: string; raw: string }

export async function fetchRecordingStatus(tenant: string): Promise<{ status: RecordingStatus; mock: boolean }> {
  try {
    const res = await adminFetch('/recordings/status', tenant)
    if (!res.ok) throw new Error(String(res.status))
    const body = (await res.json()) as { status?: string }
    return { status: body.status === 'Recording' ? 'Recording' : 'Stopped', mock: false }
  } catch {
    return { status: 'Stopped', mock: true }
  }
}

export async function startRecording(tenant: string, targetBaseUrl: string): Promise<{ mock: boolean }> {
  try {
    const res = await adminFetch('/recordings/start', tenant, { method: 'POST', body: JSON.stringify({ targetBaseUrl }) })
    if (!res.ok) throw new Error(String(res.status))
    return { mock: false }
  } catch {
    return { mock: true }
  }
}

async function captureVia(tenant: string, path: string): Promise<{ stubs: CapturedStub[]; mock: boolean }> {
  try {
    const res = await adminFetch(path, tenant, { method: 'POST' })
    if (!res.ok) throw new Error(String(res.status))
    const body = (await res.json()) as { mappings?: WireMockMapping[] }
    return { stubs: (body.mappings ?? []).map(projectCaptured), mock: false }
  } catch {
    return { stubs: [], mock: true }
  }
}

export const snapshotRecording = (tenant: string) => captureVia(tenant, '/recordings/snapshot')
export const stopRecording = (tenant: string) => captureVia(tenant, '/recordings/stop')

function projectCaptured(m: WireMockMapping): CapturedStub {
  const req = m.request ?? {}
  return {
    method: (req.method ?? 'ANY').toUpperCase(),
    url: req.url ?? req.urlPath ?? req.urlPattern ?? req.urlPathPattern ?? '/',
    raw: JSON.stringify(m, null, 2),
  }
}

// A scenario (stateful stub group) and its state machine.
export interface Scenario {
  name: string
  state: string
  possibleStates: string[]
}

/** Loads the tenant's scenarios (GET /__admin/scenarios), with a sample fallback when no host. */
export async function fetchScenarios(tenant: string): Promise<{ scenarios: Scenario[]; mock: boolean }> {
  try {
    const res = await adminFetch('/scenarios', tenant)
    if (!res.ok) throw new Error(String(res.status))
    const body = (await res.json()) as { scenarios?: Scenario[] }
    return { scenarios: body.scenarios ?? [], mock: false }
  } catch {
    return { scenarios: sampleScenarios(tenant), mock: true }
  }
}

/** Moves a scenario to a state (PUT /__admin/scenarios/{name}/state). */
export async function setScenarioState(tenant: string, name: string, state: string): Promise<{ mock: boolean }> {
  try {
    const res = await adminFetch(`/scenarios/${encodeURIComponent(name)}/state`, tenant, { method: 'PUT', body: JSON.stringify({ state }) })
    if (!res.ok) throw new Error(String(res.status))
    return { mock: false }
  } catch {
    return { mock: true }
  }
}

/** Resets every scenario to its start state (POST /__admin/scenarios/reset). */
export async function resetScenarios(tenant: string): Promise<{ mock: boolean }> {
  try {
    const res = await adminFetch('/scenarios/reset', tenant, { method: 'POST' })
    if (!res.ok) throw new Error(String(res.status))
    return { mock: false }
  } catch {
    return { mock: true }
  }
}

// The demo tenants that ship with representative sample data. A tenant the operator adds is not one of
// these, so in sample mode (no host) it reads as empty — the same way a brand-new tenant is empty on a
// real host. This avoids the illusion that adding a tenant "copied" every stub into it.
const DEMO_TENANTS = new Set(['default', 'globex', 'acme-pay'])

function sampleScenarios(tenant: string): Scenario[] {
  if (!DEMO_TENANTS.has(tenant)) return []
  const base: Scenario[] = [
    { name: 'Checkout', state: 'Started', possibleStates: ['Started', 'PaymentAuthorized', 'Captured'] },
    { name: 'AccountOnboarding', state: 'KycPending', possibleStates: ['Started', 'KycPending', 'Active'] },
    { name: 'MandateSetup', state: 'Active', possibleStates: ['Started', 'Active', 'Cancelled'] },
  ]
  return tenant === 'globex' ? base.slice(0, 1) : base
}

// A request-journal entry as the dashboard needs it — a flat projection of a serve event.
export interface JournalEntry {
  id: string
  method: string
  url: string
  status: number | null
  wasMatched: boolean
}

// The journal is an inspection view of recent traffic. A long-running host can accumulate tens of
// thousands of serve events; handing all of them to the client-side table (which builds a full row
// model for sorting/filtering on every render and poll) blocks the main thread and freezes the tab.
// Cap to the most recent slice so the grid stays responsive, and report the true total separately.
const JOURNAL_CAP = 500

/** Loads the tenant's request journal (GET /__admin/requests), with a sample fallback when no host. */
export async function fetchJournal(tenant: string, unmatchedOnly: boolean): Promise<{ entries: JournalEntry[]; total: number; mock: boolean }> {
  try {
    const res = await adminFetch(`/requests${unmatchedOnly ? '?unmatched=true' : ''}`, tenant)
    if (!res.ok) throw new Error(String(res.status))
    const body = (await res.json()) as { requests?: JournalEntry[] }
    const all = body.requests ?? []
    return { entries: all.slice(0, JOURNAL_CAP), total: all.length, mock: false }
  } catch {
    const all = sampleJournal(tenant)
    const filtered = unmatchedOnly ? all.filter((e) => !e.wasMatched) : all
    return { entries: filtered.slice(0, JOURNAL_CAP), total: filtered.length, mock: true }
  }
}

function sampleJournal(tenant: string): JournalEntry[] {
  if (!DEMO_TENANTS.has(tenant)) return []
  const rows: JournalEntry[] = [
    { id: 'r1', method: 'POST', url: '/api/v2/payments', status: 200, wasMatched: true },
    { id: 'r2', method: 'GET', url: '/api/v2/accounts/8891', status: 200, wasMatched: true },
    { id: 'r3', method: 'GET', url: '/api/v2/accounts/8891/statements', status: 404, wasMatched: false },
    { id: 'r4', method: 'POST', url: '/api/v2/payments/9/capture', status: 200, wasMatched: true },
    { id: 'r5', method: 'DELETE', url: '/api/v2/mandates/44', status: 404, wasMatched: false },
    { id: 'r6', method: 'GET', url: '/api/v2/rates?from=EUR&to=TRY', status: 200, wasMatched: true },
  ]
  return tenant === 'globex' ? rows.slice(0, 3) : rows
}

/** Deletes a stub by id. Returns `mock: true` when no host answered. */
export async function deleteStub(tenant: string, id: string): Promise<{ mock: boolean }> {
  try {
    const res = await adminFetch(`/mappings/${id}`, tenant, { method: 'DELETE' })
    if (!res.ok) throw new Error(String(res.status))
    return { mock: false }
  } catch {
    return { mock: true }
  }
}

// Representative sample data (used only when no host answers). Varies a little by tenant so switching
// tenants visibly re-scopes the grid.
function sampleStubs(tenant: string): Stub[] {
  if (!DEMO_TENANTS.has(tenant)) return []
  const base: Stub[] = [
    { id: '1', method: 'GET', url: '/api/v2/accounts/{id}', protocol: 'http', priority: 5, scenario: null, persistence: 'Postgres', lastMatched: '12s', status: 'live' },
    { id: '2', method: 'POST', url: '/api/v2/payments', protocol: 'http', priority: 10, scenario: 'Checkout', persistence: 'Postgres', lastMatched: '3s', status: 'live' },
    { id: '3', method: 'POST', url: '/api/v2/payments/{id}/capture', protocol: 'http', priority: 10, scenario: 'Checkout', persistence: 'Postgres', lastMatched: '7s', status: 'live' },
    { id: '4', method: 'GET', url: '/api/v2/rates?from={a}&to={b}', protocol: 'http', priority: 3, scenario: null, persistence: 'Redis', lastMatched: '1m', status: 'proxy' },
    { id: '5', method: 'PUT', url: '/api/v2/accounts/{id}/limits', protocol: 'http', priority: 5, scenario: null, persistence: 'Postgres', lastMatched: '18m', status: 'live' },
    { id: '6', method: 'DELETE', url: '/api/v2/mandates/{id}', protocol: 'http', priority: 5, scenario: null, persistence: 'Postgres', lastMatched: '2h', status: 'draft' },
    { id: '7', method: 'PATCH', url: '/api/v2/webhooks/{id}', protocol: 'http', priority: 1, scenario: null, persistence: 'LiteDB', lastMatched: '1d', status: 'live' },
    { id: '8', method: 'POST', url: 'mockifyr.grpc.Greeter/SayHello', protocol: 'grpc', priority: 8, scenario: null, persistence: 'Postgres', lastMatched: '41s', status: 'live' },
    { id: '9', method: 'POST', url: '/graphql · query Balance', protocol: 'graphql', priority: 8, scenario: null, persistence: 'Postgres', lastMatched: '55s', status: 'live' },
    { id: '10', method: 'GET', url: '/ws/notifications', protocol: 'websocket', priority: 5, scenario: null, persistence: 'In-memory', lastMatched: '2m', status: 'live' },
  ]
  if (tenant === 'globex') return base.slice(0, 6).map((s) => ({ ...s, url: s.url.replace('/api/v2', '/retail/v1') }))
  if (tenant === 'default') return base.slice(0, 3)
  return base
}
