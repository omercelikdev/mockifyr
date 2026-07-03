# Parity notes — G2 Response + templating

Verified WireMock response behaviors discovered while building the response vertical against the
oracle (`wiremock/wiremock:3.10.0`). See [README](README.md) for the format.

### Static response bodies (G2a)

- **Group / item:** G2a — fuzz-validated against the oracle.
- **`jsonBody`** renders the inline JSON as the response body, **emitted compact**
  (`{"a":1,"b":[2,3],"c":"x"}`) with **no** automatic `Content-Type` header in 3.10.0. Our adapter
  re-serializes the parsed `jsonBody` element compactly (`System.Text.Json`), matching byte-for-byte.
- **`base64Body`** renders the base64-decoded **bytes** verbatim, including non-text bytes
  (validated with `hi\0!`).
- **Body precedence.** A literal `body` string wins over `jsonBody`, which wins over `base64Body`;
  only one is emitted.
- **Multi-value response headers** (`"headers": { "X": ["a","b"] }`) are emitted as repeated header
  lines and diff green.
- **Custom status codes** (e.g. `418`) pass through.
- **`statusMessage` (reason phrase) is parsed but not yet differentially asserted** — the harness
  response snapshot captures only the numeric status, not the HTTP reason phrase. Tracked for a
  harness extension.
- **Deferred:** `bodyFileName` (needs the `__files` store + templating, arrives with G2b), and
  gzip/`Content-Encoding` (transport-level; the harness client auto-decompresses).
- **Regression case:** `G2StaticResponseTests.StaticResponse_Bodies`.

### Response templating (G2b)

- **Group / item:** G2b — fuzz-validated against the oracle.
- **Library:** WireMock uses jknack Handlebars; we use **Handlebars.Net** (confined to
  `Mockifyr.Templating`).
- **Activation.** Templating runs only when the response declares the `response-template`
  transformer (`"transformers": ["response-template"]`); otherwise the body/headers are emitted
  verbatim (a `{{...}}` in a non-transformed body is left literal — verified).
- **Request model verified:** `{{request.method}}`, `{{request.url}}` (path + query),
  `{{request.path}}` (path only), `{{request.pathSegments.[n]}}` (0-indexed), `{{request.query.name}}`
  (first value), `{{request.headers.Name}}`, and `{{request.body}}`.
- **No HTML escaping.** WireMock emits `{{ }}` output raw (`<a>&"` stays as-is); we disable the
  Handlebars.Net text encoder (`TextEncoder = null`) to match.
- **Response headers are templated too**, not just the body.
- **Missing model values render empty** (`{{request.query.none}}` → ``).
- **Deferred:** `request.path.<name>` **named path variables** from `urlPathTemplate` — WireMock's
  `request.path` is a rich object (string form + named vars + indexed segments) whose dual nature
  needs a custom Handlebars.Net member resolver; and the built-in **helpers** (jsonPath, xPath, date,
  random, etc.), which are their own roadmap items (G2c–G2h).
- **Regression case:** `G2StaticResponseTests.Templating_ResponseTemplate`.
