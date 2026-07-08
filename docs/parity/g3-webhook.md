# Parity notes — G3 Webhook / correlation

Verified WireMock webhook behaviors discovered while building the outbound vertical against the
oracle (`wiremock/wiremock:3.10.0`). See [README](README.md) for the format.

## How webhooks are validated differentially

Webhooks are an **outbound side effect**, not a response transform, so they can't be diffed from the
served response. The harness runs a host-side **`WebhookReceiver`** (an `HttpListener` on an
ephemeral port) that captures the delivery each side fires; the differential test then diffs the two
captures. Reachability: the oracle container reaches the host receiver via `host.docker.internal`
(the oracle container is started with `--add-host host.docker.internal:host-gateway` for Linux CI);
Mockifyr, running in-process, reaches it over `127.0.0.1`. The mapping carries a `__WEBHOOK_HOST__`
token the harness rewrites per side. Driven by `WebhookScenarios` + `G3WebhookTests.Webhook_Delivery`.

## Webhook post-serve action (G3a)

- **Group / item:** G3a — validated against the oracle.
- **Shape.** A webhook is a `postServeActions` entry: `{"name":"webhook","parameters":{"method",
  "url","headers","body"}}`. Only the `webhook` action name is handled; others are ignored.
- **Fires only on a match.** A webhook is delivered **only when its stub matches**; an unmatched
  request (404) fires nothing (verified — the receiver captured nothing for a no-match).
- **What is sent** — the declared `method`, `url`, `headers`, and `body`, verbatim. Verified for a
  `POST` with a JSON body + declared headers, a bodyless `GET`, and a `PUT` with a text body to a
  distinct path. **`method` defaults to `GET`** when omitted.
- **Auto-added transport headers differ by client and are ignored in the diff.** The oracle (Apache
  HttpClient) adds `Host`, `Content-Length`, `Connection`, `User-Agent: Apache-HttpClient/…`, and on
  a bodyless request an `Upgrade`/`Connection: Upgrade`; .NET's `HttpClient` adds its own set. Only
  the **declared** headers (plus method, path, body) are compared — those match byte-for-byte.
- **Delivery is asynchronous / best-effort.** WireMock fires the webhook after serving; Mockifyr
  mirrors this via the fire-and-forget `IServeEventListener` dispatch in `StubEngine`, so an
  unreachable target never affects request serving. The harness polls the receiver with a timeout.
- **Architecture.** The pure engine only records the `WebhookDefinition` on the matched stub; the
  outbound HTTP call lives in `Mockifyr.ServeEvents.Webhook.WebhookServeEventListener` at the facade
  edge — Core stays I/O-free (see docs/decisions/0001).
- **Deferred to G3b:** templating of the webhook `url`/`headers`/`body`, `originalRequest`
  correlation, `delay`, and sub-event recording.
- **Regression case:** `G3WebhookTests.Webhook_Delivery`.

## Templated webhook + originalRequest (G3b)

- **Group / item:** G3b — validated against the oracle.
- **Templating is automatic** for webhook fields — **no `response-template` transformer is needed**
  on the mapping (verified: a mapping with no `transformers` still rendered the webhook). The **URL**
  (path **and** query string), **header values**, and **body** are all rendered.
- **The model root is `originalRequest`** (the triggering request), not `request`. Its sub-structure
  is identical to the response templating model: `{{originalRequest.method}}`, `url`, `path`,
  `pathSegments.[n]`, `query.name`, `headers.Name`, `body`, and the built-in helpers work against it
  (`{{jsonPath originalRequest.body '$.id'}}`). Verified end-to-end: a webhook URL
  `…/cb/{{jsonPath originalRequest.body '$.id'}}?q={{originalRequest.query.tenant}}`, a header
  `X-Echo: {{originalRequest.headers.X-In}}`, and a templated body all rendered identically on both
  sides.
- **Implementation reuse.** Response templating and webhook templating share one Handlebars engine
  (`HandlebarsFactory`) and one request-model builder (`RequestModel`); the webhook path only swaps
  the root key to `originalRequest`. The listener depends on the Core contract
  `IServeEventTemplateRenderer` (implemented by `WebhookTemplateRenderer` in the templating edge), so
  outbound I/O stays decoupled from templating.
- **Harness note.** The receiver captures `Url.PathAndQuery` (not just the path) so a templated URL
  **query string** is part of the diff.
- **Deferred:** **sub-event recording** (WireMock records the webhook request/response as correlated
  sub-events on the serve event) — these are only observable through the admin/verify surface, which
  arrives with **G6/G7**, so there is nothing to diff yet.
- **Regression case:** `G3WebhookTests.Webhook_Delivery` (the `webhook[templated]` scenario).

## Webhook `delay` (structural)

- **Group / item:** webhook `delay` — **self-tested** (delay timing is racy against a live oracle, so
  no differential claim). The reader parses `parameters.delay = {"type":"fixed","milliseconds":N}` onto
  `WebhookDefinition.DelayMilliseconds`; `WebhookServeEventListener` waits that long before firing (the
  wait honours the cancellation token, so shutting down mid-delay cancels the delivery).
- **Regression case:** `G3WebhookDelayTests.Webhook_WaitsTheConfiguredDelayBeforeFiring` — an injected
  handler records when the outbound call actually lands and asserts it is `>= ~delay`.
- **Still deferred:** **sub-event recording** — the only remaining webhook gap.
