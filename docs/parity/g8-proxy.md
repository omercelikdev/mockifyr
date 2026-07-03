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
- **Deferred:** `additionalProxyRequestHeaders` / `removeProxyRequestHeaders` and proxy URL prefix
  rewriting (probed to exist; the `ProxyDirective` carries only the base URL for now); response-header
  rewriting; and proxy combined with record & playback (G9).
- **Regression case:** `G8ProxyTests.Proxy_ReturnsUpstreamResponse`.
