# Parity notes — G8 Proxying

Verified WireMock proxy behaviors against the oracle (`wiremock/wiremock:3.10.0`). See
[README](README.md) for the format.

## Proxy (G8)

- **Group / item:** G8 — validated against the oracle.
- **Shape.** A response with **`proxyBaseUrl`** forwards the matched request to that upstream and
  returns the upstream's response. Recorded as a `ProxyDirective` on the response (like delay/fault);
  the pure engine only records it — a **facade** performs the outbound call. Mockifyr's
  `ProxyResponder` (a facade edge, reused by the G12 transport facade) does the forwarding.
- **What is forwarded.** The **method**, the **path + query** appended to `proxyBaseUrl` (verified:
  `/proxied?x=1&y=2` reaches the upstream intact), the **body** (verified for a proxied `POST`), and
  the request headers (except `Host`, which must track the upstream URL). The **upstream's response**
  — status, headers, body — is returned to the client verbatim.
- **How it's validated.** Both sides proxy to one shared host-side `UpstreamServer` (an `HttpListener`
  that echoes the received path and returns an `X-Upstream` marker header): the oracle reaches it via
  `host.docker.internal`, Mockifyr via `127.0.0.1` (the stub's `__PROXY_HOST__` token is rewritten per
  side). The proxied responses are diffed on **status + body + the `X-Upstream` header** (transport
  headers like `Date`/`Server`/`Transfer-Encoding` are masked). Because the upstream echoes the path,
  path/query forwarding is part of the diff.
- **`additionalProxyRequestHeaders` (feature-audit backfill).** The stub's
  `additionalProxyRequestHeaders` are added to the forwarded request (`ProxyDirective.AdditionalHeaders`,
  injected by `ProxyResponder`). Validated by having the upstream echo an `X-Proxy-Added` request header
  back as an `X-Echoed-Added` response header — both sides' proxied responses carry it identically.
- **`proxyUrlPrefixToRemove` (feature-audit backfill).** A leading URL-path prefix is stripped before
  forwarding (`ProxyDirective.UrlPrefixToRemove`, applied by `ProxyResponder`). Validated by the
  `url-prefix-to-remove` scenario: a request to `/api/widgets` on a stub with
  `proxyUrlPrefixToRemove: "/api"` reaches the upstream as `/widgets` on both sides (the upstream
  echoes the received path in the body, so it is part of the diff).
- **Deferred:** `removeProxyRequestHeaders`; response-header rewriting; and proxy combined with
  record & playback (G9).
- **`removeProxyRequestHeaders` — NOT a response-level field in the oracle (differential finding).**
  A stub with `response.removeProxyRequestHeaders: ["X-Drop-Me"]` was driven with that request header,
  against an upstream that echoes what it receives: **WireMock 3.10 still forwarded the header** (the
  upstream echoed it back on the oracle side). So header stripping is not a per-stub response directive
  in this WireMock version — implementing it on our side would **diverge** from the oracle, which is why
  it stays unimplemented. (WireMock does this via a global proxy setting / transformer, not the stub
  response.) Kept as a documented negative result rather than a silent divergence.
- **Regression case:** `G8ProxyTests.Proxy_ReturnsUpstreamResponse` (now incl. the additional-headers case).
