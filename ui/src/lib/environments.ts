import { useSyncExternalStore } from 'react'

// Postman-style environments (#157): named base URLs usable as {{name}} in the app's URL fields
// (webhook/callback URL, proxy base URL, recordings target). Resolution happens in the UI at the
// moment a URL is used (stub save, recording start) — deliberately NOT server-side and NOT stored
// in the mapping JSON: a {{name}} left in a mapping would collide with the Handlebars templating
// namespace (the server would render it as empty) and make the mapping non-portable. The stored
// mapping always carries the resolved URL; the variable is an authoring convenience.

export interface MockEnvironment {
  name: string
  baseUrl: string
}

export interface EnvironmentState {
  environments: MockEnvironment[]
  /** Name of the active environment ({{baseUrl}} resolves against it), or null. */
  active: string | null
}

const KEY = 'ui.environments'
const EMPTY: EnvironmentState = { environments: [], active: null }

// Environment names are bare identifiers so {{name}} stays distinguishable from Handlebars
// expressions ({{jsonPath …}}, {{request.path}} etc. contain spaces or dots and are never touched).
export const ENV_NAME_PATTERN = /^[A-Za-z_][A-Za-z0-9_-]*$/

let cache: EnvironmentState | null = null
const listeners = new Set<() => void>()

function read(): EnvironmentState {
  if (cache) return cache
  try {
    const raw = JSON.parse(localStorage.getItem(KEY) ?? 'null') as EnvironmentState | null
    cache = raw && Array.isArray(raw.environments) ? raw : EMPTY
  } catch {
    cache = EMPTY
  }
  return cache
}

function write(state: EnvironmentState) {
  cache = state
  localStorage.setItem(KEY, JSON.stringify(state))
  listeners.forEach((l) => l())
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/** Reactive view of the environment store — updates every consumer on any change, no reload needed. */
export function useEnvironments(): EnvironmentState {
  return useSyncExternalStore(subscribe, read)
}

export function saveEnvironment(env: MockEnvironment, previousName?: string) {
  const state = read()
  const rest = state.environments.filter((e) => e.name !== env.name && e.name !== previousName)
  write({
    environments: [...rest, env].sort((a, b) => a.name.localeCompare(b.name)),
    // Renaming the active environment keeps it active under its new name.
    active: state.active === previousName ? env.name : state.active,
  })
}

export function removeEnvironment(name: string) {
  const state = read()
  write({
    environments: state.environments.filter((e) => e.name !== name),
    active: state.active === name ? null : state.active,
  })
}

export function setActiveEnvironment(name: string | null) {
  write({ ...read(), active: name })
}

const VARIABLE = /\{\{\s*([A-Za-z_][A-Za-z0-9_-]*)\s*\}\}/g

export interface ResolvedUrl {
  resolved: string
  /** Environment-style variables that matched nothing — surfaced as a warning, never sent silently. */
  unknown: string[]
  /** True when at least one variable was substituted. */
  changed: boolean
}

/**
 * Substitutes {{name}} references against the defined environments. A name addresses that
 * environment's base URL directly; the special {{baseUrl}} resolves to the ACTIVE environment.
 * Handlebars expressions (anything that is not a bare identifier) pass through untouched, so a
 * webhook URL can mix both: `{{local}}/callback?id={{jsonPath originalRequest.body '$.id'}}`.
 */
export function resolveUrl(url: string, state: EnvironmentState): ResolvedUrl {
  const unknown = new Set<string>()
  let changed = false
  const resolved = url.replace(VARIABLE, (whole, name: string) => {
    if (name === 'baseUrl') {
      const active = state.environments.find((e) => e.name === state.active)
      if (active) { changed = true; return active.baseUrl }
      unknown.add(name)
      return whole
    }
    const env = state.environments.find((e) => e.name === name)
    if (env) { changed = true; return env.baseUrl }
    unknown.add(name)
    return whole
  })
  return { resolved, unknown: [...unknown], changed }
}
