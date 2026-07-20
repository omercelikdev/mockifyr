# G11 — HTTPS/TLS + HTTP/2

Transport-security parity: Mockifyr serves over TLS (and, later in this group, HTTP/2) exactly as
WireMock does. TLS is transport encryption, so the HTTP response is byte-identical to the plaintext
one — the differential here proves the handshake succeeds and nothing diverges *because* the request
came over TLS. Validated over a real socket against the oracle's own HTTPS listener.

## HTTPS/TLS serving (G11a)

- **Group / item:** G11a — validated over the wire against the oracle.
- **`--https-port`.** `MockifyrHost.Build` binds an HTTPS listener when `--https-port` is given
  (config key `https-port`), alongside the plain `--port`. Both listeners are configured directly on
  Kestrel (`ConfigureKestrel` + `ListenAnyIP`, the HTTPS one via `UseHttps`) rather than through
  `app.Urls`, since the cert must be attached to the listener. Port `0` = ephemeral (tests). This is
  the `--https-port` hook deferred from G12f.
- **Self-signed certificate.** Like WireMock — which serves HTTPS with a bundled self-signed cert by
  default — Mockifyr mints an ephemeral RSA-2048 self-signed cert at startup (`SelfSignedCertificate`),
  valid for `localhost` and the loopback addresses (SAN). It is round-tripped through PKCS#12 so
  Kestrel accepts the (exportable) private key on every platform. A configured keystore (PFX
  path/password) is a later refinement, not needed for parity.
- **Validation.** The same stub is loaded into both sides and served over a **real TLS connection** —
  the oracle over its `--https-port 8443` listener, Mockifyr over its `--https-port` Kestrel listener —
  and the responses (status, body, declared headers) are diffed. Both clients accept the self-signed
  certificates (parity is about the HTTP response served over TLS, not the certificate identity). The
  shared oracle container now also enables `--https-port 8443`; the ~88 plaintext differentials are
  unaffected (HTTP 8080 still serves).
- **Deferred to G11b (explicitly tracked — not a silent gap):** **HTTP/2** (ALPN negotiation over TLS,
  and h2c). Also deferred: a configured (non-self-signed) keystore, and client-certificate/mTLS.
- **Regression case:** `G11aHttpsTests.Serves_OverTls_MatchingTheOracle`.

## HTTP/2 (G11b)

- **Group / item:** G11b — validated over the wire against the oracle. Closes the **G11** group.
- **Configuration.** Both Kestrel listeners are set to `Http1AndHttp2`: the TLS listener negotiates
  h2 via ALPN (HTTP/1.1 still available), and the plaintext listener is h2c-capable — matching
  WireMock, which enables HTTP/2 on both ports by default.
- **Validation (h2 over TLS).** An HTTP/2-forcing client (`Version = 2.0`,
  `VersionPolicy = RequestVersionExact`, so it fails rather than silently downgrading) fetches the
  same stub from the oracle's `--https-port` listener and Mockifyr's — both **ALPN-negotiate HTTP/2**
  (`response.Version` == 2.0) and return the same body. Forcing the exact version makes a green
  assertion a real negotiation, not a 1.1 fallback.
- **Learned WireMock behavior — plaintext prior-knowledge h2c is nondeterministic.** The oracle's
  *plaintext* port answers a prior-knowledge h2c request **inconsistently**: sometimes it serves
  HTTP/2 (`response.Version` == 2.0), sometimes it refuses with the HTTP/2 error `HTTP_1_1_REQUIRED`
  (0xd) — observed flipping between otherwise-identical runs against `wiremock/wiremock:3.10.0`. So
  plaintext h2c is **not asserted** as a parity behavior (there is no stable oracle truth to diff
  against). Mockifyr's plaintext listener is left `Http1AndHttp2` (h2c-capable, deterministic via
  Kestrel) so it matches WireMock whenever WireMock does serve h2c, and HTTP/1.1 always works. TLS is
  the deterministic, asserted HTTP/2 path.
- **Regression case:** `G11bHttp2Tests.Http2_OverTls_NegotiatedByBothSides`.

## Configured keystore + mutual TLS (G11c)

- **Group / item:** G11c — **self-tested** (no differential). Closes the last G11 TLS edges.
- **Configured keystore.** `--https-keystore` (+ `--https-keystore-password`) loads the HTTPS listener's
  server certificate from a PFX/PKCS#12 file instead of the ephemeral self-signed cert — WireMock's
  `--https-keystore`/`--keystore-password`. Absent, the self-signed default (G11a) still applies.
- **Mutual TLS.** `--https-require-client-auth` sets Kestrel's `ClientCertificateMode.RequireCertificate`,
  and `--https-truststore` (+ optional `--https-truststore-password`) supplies the trust anchor the
  presented client certificate must chain to (validated via a custom-root-trust `X509Chain`, so a cert
  the OS happens to trust is not accepted unless it chains to the configured anchor). Mirrors WireMock's
  `--https-require-client-auth` + `--https-truststore`/`--truststore-password`. Only the HTTPS listener
  requires the client cert; the plaintext port is unaffected.
- **Why self-tested, not diffed.** Mutual TLS is standard transport authentication — reject a handshake
  with no client cert, accept one presenting a CA-signed cert — with **no WireMock-specific semantics**
  to compare. Like WebSocket serving and plaintext h2c, there is no oracle truth beyond "TLS does TLS",
  so it is validated structurally: a test CA signs a client cert, the host is started with the keystore +
  truststore + `--https-require-client-auth`, and a client **with** the cert is served the stub while a
  client **without** one fails the handshake.
- **Regression cases:** `G11cMutualTlsTests.ClientCertificate_PresentedAndTrusted_ServesTheStub` and
  `NoClientCertificate_IsRejected` (2 self-tests; no Docker).

## Outbound certificate trust — `--trust-proxy-target` / `--trust-all-proxy-targets` (#172)

- **Group / item:** post-G11 defect fix. **No oracle diff**: certificate trust is an environment
  property, not a wire semantic, so this is validated by reproduction against a self-signed HTTPS
  target from inside the published container image, plus unit coverage of the policy decisions.
- **The gap.** Every pre-existing TLS flag (`--https-port`, `--https-keystore`,
  `--https-truststore`, `--https-require-client-auth`) configures the **listener** — traffic coming
  in. Nothing configured the **client** side: both outbound paths built a plain `new HttpClient()`.
  So an endpoint served by an internal corporate CA was unreachable, and there was no supported way
  to reach it. The host machine trusts such a CA through its keychain; a Linux container does not —
  which is exactly why the same call succeeds from Postman and fails from a containerised Mockifyr.
- **The surface is WireMock's, deliberately.** `--trust-proxy-target <host>` (repeatable) and
  `--trust-all-proxy-targets` are real WireMock CLI options; adopting the names avoids inventing a
  parallel vocabulary. WireMock scopes them to browser proxying — Mockifyr applies them to **both**
  outbound paths (callbacks and `proxyBaseUrl`), because the identical wall is hit on each and a
  single concept split across two flag families would be worse than the small deviation.
- **Learned: trust is keyed on the address requested, not the certificate presented.** The validation
  callback reads the host from `request.RequestUri`, never from the certificate's subject. Keying on
  the certificate would invert the guarantee — anything able to present a certificate naming
  `dev.corp` would be trusted, rather than the endpoint the operator actually addressed.
- **Verified per-host isolation.** With `--trust-proxy-target some.other.host`, a call to the
  self-signed target still fails (`rejected by the provided RemoteCertificateValidationCallback`).
  Trusting one endpoint grants nothing to another, and no wildcard/suffix matching is implied —
  `sub.dev.corp` is not covered by `dev.corp`.
- **Learned: a repeated flag is not repeatable for free.** .NET's command-line configuration provider
  collapses a repeated key to its **last** value, so `--trust-proxy-target a --trust-proxy-target b`
  would have silently trusted only `b`. The raw `args` are scanned alongside the parsed configuration;
  a comma/semicolon-separated list is accepted too.
- **Default unchanged, and the deviation is loud.** With no flag the stock handler is used — not a
  custom callback that happens to return the same answer — so the default path is .NET's own
  validation. A host that does relax trust prints a line saying so at startup.

## Learned: a transport error's reason lives in the *inner* exception (#172)

The webhook journal recorded only `exception.Message`. For TLS that outer message is literally
`The SSL connection could not be established, see inner exception.` — a sentence referring to
information the operator was never shown. The inner exception carries the diagnosis:

```
The SSL connection could not be established, see inner exception.
  -> The remote certificate is invalid according to the validation procedure:
     RemoteCertificateNameMismatch, RemoteCertificateChainErrors
```

The chain is now flattened into the journal line (repeated messages collapsed). This is why the
original report could only say "SSL error" — the product had discarded its own explanation. Regression
cases live in `OutboundTlsTrustTests`.
