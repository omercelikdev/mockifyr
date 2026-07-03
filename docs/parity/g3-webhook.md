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
- **Deferred to G3b:** templating of the webhook `url`/`headers`/`body` (e.g. `{{jsonPath
  request.body '$.id'}}`), `originalRequest` correlation, `delay`, and sub-event recording.
- **Regression case:** `G3WebhookTests.Webhook_Delivery`.
