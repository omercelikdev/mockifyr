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
