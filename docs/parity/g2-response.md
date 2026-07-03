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

### Data helpers (G2c)

- **Group / item:** G2c — validated against the oracle.
- **Helpers:** `jsonPath`, `xPath`, `regexExtract`, `formData`, `parseJson`. Value-returning helpers
  (`jsonPath`, `xPath`) emit the extracted value inline; the others *assign* a variable into the
  root scope and render nothing.
- **`jsonPath request.body '$.x'`** returns: the **raw scalar** for strings/numbers/booleans
  (`neo`, `42`, `true`), an **empty string** for a missing path or a JSON `null`, and an empty
  string when the body is not valid JSON.
  - An **array** result is emitted **compact** (`[1,2,3]`, `[{"a":1},{"b":2}]`).
  - An **object** result is **pretty-printed with Jackson's `DefaultPrettyPrinter`** — multi-line
    objects with a two-space indent and a `" : "` separator, but **single-line `[ 2, 3 ]` arrays**
    (Jackson's `FixedSpaceIndenter`). Exact bytes:
    `{\n  "a" : 1,\n  "b" : [ 2, 3 ],\n  "c" : "x"\n}`. Reproduced by a hand-written writer over the
    Newtonsoft `JToken`; the top-level-array vs nested-array asymmetry (compact vs spaced) is a real
    WireMock quirk, not ours.
- **`xPath request.body 'expr'`** returns:
  - a **text node / attribute** value (`/r/a/text()` → `one`, `/r/a/@id` → `7`);
  - an **element** node serialized as **XML** — a leaf inline (`<a>one</a>`), a subtree indented two
    spaces with `\n` newlines (`<r>\n  <a>one</a>\n</r>`). .NET's `XElement.ToString()` matches Java's
    transformer output byte-for-byte here (verified `\n`, not `\r\n`);
  - a **string()/number** function result as its string form (`count(...)` → `2`, integral doubles
    printed without a decimal point);
  - an **empty string** for an empty node-set or unparseable XML.
  - When **multiple nodes** match, only the **first** is emitted.
- **`regexExtract request.body 'regex'`** returns the **whole first match**; with a **variable name**
  third argument it assigns the **capture groups** (group 1..n) to an indexable variable
  (`{{p.0}}` = group 1) and renders nothing. On **no match**: `default='…'` is emitted if present,
  otherwise WireMock's literal error string `[ERROR: Nothing matched <regex>]`.
- **`formData request.body 'form'`** parses an `x-www-form-urlencoded` body into a variable;
  `{{form.field}}` is the **first** value, a missing field renders empty, and values are **not**
  decoded unless `urlDecode=true` (which decodes `%XX` **and** `+` → space).
- **`parseJson request.body 'obj'`** parses a JSON string into a navigable variable
  (`{{obj.a.b}}`, `{{obj.arr.0}}`). Backed by a Newtonsoft `JToken`, which Handlebars.Net navigates
  natively (`JObject` is an `IDictionary`, `JArray` an `IList`).
- **Deferred (documented, no oracle claim yet):**
  - **Multi-value `formData` indexing** (`{{form.key.0}}`, `{{form.key.1}}` for a repeated key).
    Handlebars.Net renders any `IList` bound to a bare `{{form.key}}` as a comma-joined string
    (`1,2`) instead of the first value, so WireMock's `ListOrSingle` dual render/index type needs a
    custom member resolver — the same class of problem as the deferred `request.path` object.
  - **`parseJson` block/inline form** (`{{#parseJson}}…{{/parseJson}}`) and **`parseJson` on scalar
    JSON** (booleans render `True` via Newtonsoft, not `true`).
  - **`jsonPath` on a container that nests objects inside arrays** (only scalar-array and
    flat/array-valued objects are pinned).
- **Regression case:** `G2StaticResponseTests.Templating_DataHelpers`.

### Date helpers (G2d)

- **Group / item:** G2d — validated against the oracle over **fixed** input instants (so the diff is
  clock-independent; `now`-based rendering is racy against a second clock and is excluded).
- **Helpers:** `parseDate` (string → instant) composed into `date` (instant → string), the WireMock
  form `{{date (parseDate '…') …}}`. Handlebars.Net passes the parsed value through the subexpression
  as a real object, so the composition needs no stringify/re-parse.
- **`parseDate`** reads **ISO-8601** by default (`2021-05-15T10:30:00Z`), or a **Java
  `SimpleDateFormat`** input pattern via `format=` (`parseDate '15/05/2021' format='dd/MM/yyyy'`).
- **`date` format pattern is Java `SimpleDateFormat`.** Most letters are shared with .NET
  (`y M d H h m s`) but three differ and are rewritten: `E`→day name (`ddd`/`dddd`), `a`→AM/PM
  (`tt`), `S`→fractional second (`f`). Verified: `EEE, dd MMM yyyy HH:mm:ss` → `Sat, 15 May 2021
  10:30:00`; `hh:mm a` → `10:30 PM`; `…ss.SSS` → `….000`. Names use the invariant (English) culture.
- **Default format** (no `format=`) renders ISO-8601 UTC with a literal `Z`: `2021-05-15T10:30:00Z`.
- **`format='epoch'`** → **milliseconds** since the epoch; **`format='unix'`** → **seconds**.
- **`offset=`** is `"<n> <unit>"` where **unit is plural** — `seconds`/`minutes`/`hours`/`days`/
  `months`/`years`, forwards or backwards (`-1 hours`). A **singular** unit throws on the oracle
  (`No enum constant DateTimeUnit.DAY`), so only plural is supported.
- **`timezone=` is ignored on a parsed instant.** The oracle applies **no shift** for
  `Australia/Sydney` or `America/New_York` (a parsed instant is already absolute); we match by
  ignoring the option, pinned by the `date-timezone-ignored` case.
- **Deferred (racy / documented, no oracle claim):** the `now` helper and any `now`-relative
  rendering; the **unparseable-date fallback** (WireMock falls back to *now*); Java pattern letters
  outside the shared/rewritten set (zone `Z`/`X`, era `G`, week/day-of-year…), which are emitted as
  literals rather than throwing.
- **Regression case:** `G2StaticResponseTests.Templating_DateHelpers`.
