# Mockifyr

[![CI](https://github.com/omercelikdev/mockifyr/actions/workflows/ci.yml/badge.svg)](https://github.com/omercelikdev/mockifyr/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/omercelikdev/mockifyr?sort=semver)](https://github.com/omercelikdev/mockifyr/releases)
[![Image](https://img.shields.io/badge/ghcr.io-mockifyr-2496ED?logo=docker&logoColor=white)](https://github.com/omercelikdev/mockifyr/pkgs/container/mockifyr)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)

**An independent, .NET-based API mock engine + platform.** A transport-agnostic request-matching and
response engine with first-class multi-tenancy, pluggable persistence, and thin facades — in-process
library · HTTP server · admin REST · gRPC · GraphQL · WebSocket. Clean-room codebase with its own IP
and no third-party mock-engine dependencies.

## Quick start

### Docker — one image (engine + admin API + dashboard)

Just run it — in-memory, zero config, **the same one line on macOS, Linux and Windows**:

```bash
docker run -p 8080:8080 ghcr.io/omercelikdev/mockifyr
```

- Mock surface — `http://localhost:8080`
- Admin API — `http://localhost:8080/__admin`
- Dashboard — `http://localhost:8080/__mockifyr`

Create stubs in the dashboard, or import a WireMock bundle. Runs on `linux/amd64` and `linux/arm64`
(Apple Silicon included).

**Keep your stubs across restarts** — `docker compose up`, or a named volume (both identical on every OS):

```bash
docker compose up                                        # stubs live in ./mappings, next to you
docker run -p 8080:8080 -v mockifyr-data:/work/mappings ghcr.io/omercelikdev/mockifyr   # named volume
```

**Preload / edit stub files on your host** (advanced) — bind-mount a folder of WireMock `*.json`. Only
the path syntax differs per shell; nothing else changes:

```bash
docker run -p 8080:8080 -v "$PWD/mappings:/work/mappings" ghcr.io/omercelikdev/mockifyr   # macOS / Linux
#   PowerShell:  -v "${PWD}/mappings:/work/mappings"       CMD:  -v "%cd%/mappings:/work/mappings"
```

Files load into the **default tenant**; for a named tenant (e.g. `maestro`) use the dashboard **Import**,
or POST to `/__admin/mappings/import` with an `X-Mockifyr-Tenant` header. Durable datastores:

```bash
docker compose -f docker-compose.postgres.yml up    # PostgreSQL persistence
docker compose -f docker-compose.redis.yml up       # Redis persistence
```

### Local (.NET 10 SDK)

```bash
dotnet run --project src/Mockifyr.Server -- --port 8080 --root-dir .   # stubs load from ./mappings
```

### Engine only (no dashboard)

The dashboard is opt-in via `--dashboard`; omit it to serve just the mock surface + admin API.

```bash
# Local
dotnet run --project src/Mockifyr.Server -- --port 8080 --root-dir .   # stubs load from ./mappings

# From the image (override the entrypoint to drop the built-in --dashboard)
docker run -p 8080:8080 -v "$PWD/mappings:/work/mappings" --entrypoint dotnet \
  ghcr.io/omercelikdev/mockifyr:latest Mockifyr.Server.dll --port 8080 --root-dir /work
```

Or embed the engine directly in-process with the `Mockifyr.Facade.Library` package — no HTTP at all.

## Configuration

Everything is a CLI flag. The common ones:

| Flag | Effect |
|------|--------|
| `--port <n>` | mock-serving HTTP port (default 8080) |
| `--https-port <n>` | enable HTTPS / HTTP2 |
| `--root-dir <dir>` | load and persist stubs as JSON files |
| `--dashboard <dir>` | serve the built dashboard under `/__mockifyr` |
| `--admin-user <u>` · `--admin-pass <p>` | require HTTP Basic auth on the admin API (`/__admin/*`); the dashboard shows a login screen |
| `--postgres <connstr>` · `--redis <connstr>` · `--litedb <path>` | durable persistence backend |
| `--change-feed` | keep multiple instances coherent |
| `--webhook-host-fallback false` | deliver callbacks to exactly the address written, never retrying via the host gateway |
| `--trust-proxy-target <host>` | trust that host's certificate on outbound calls (repeatable) |
| `--trust-all-proxy-targets` | trust every outbound certificate |

The hot path is always in-memory; a durable backend is opt-in and writes through.

### Callbacks to your own machine

Running in Docker, `localhost` inside the container means *the container* — so a callback aimed at
`http://localhost:5004` cannot reach a service on your machine, even though the same URL works from
Postman. Mockifyr handles this: a loopback callback whose connection is **refused** is retried once
via `host.docker.internal`, and both attempts appear in the request journal. Targeting
`host.docker.internal` yourself works too, and `--webhook-host-fallback false` turns the retry off.
On Linux, `host.docker.internal` only exists if the container is started with
`--add-host=host.docker.internal:host-gateway`.

### Callbacks and proxies to an internal HTTPS endpoint

An endpoint served by your organisation's internal CA is trusted by your machine's keychain but not
by the container, so an outbound call to it fails where Postman succeeds. The journal names the
reason (`RemoteCertificateChainErrors`, a name mismatch, an expiry). To allow it, trust that endpoint
by name — the same flags WireMock uses, applied to callbacks and proxying alike:

```bash
docker run … mockifyr --trust-proxy-target api.dev.mycorp.intra
```

Trusting one host grants nothing to any other. `--trust-all-proxy-targets` disables verification for
every target; the host prints a warning at startup when either flag is in effect. Without them,
certificates are verified normally.

You can also manage trusted hosts from **Settings → Outbound certificate trust** in the dashboard,
which takes effect on the next call with no restart and survives one. Passing a `--trust-*` flag
pins the configuration instead and the dashboard shows it read-only — the same two-mode design as
Git sync. `--trust-all-proxy-targets` stays flag-only: the dashboard can trust individual hosts but
cannot turn verification off.

## Documentation

- Architecture & design — [ARCHITECTURE.md](ARCHITECTURE.md)
- Roadmap — [docs/roadmap.md](docs/roadmap.md) · decisions — [docs/decisions/](docs/decisions/)
- This is an AI-driven repository; how work is done here — [CLAUDE.md](CLAUDE.md)

## Contributing

Contributions are welcome. Read [CLAUDE.md](CLAUDE.md) for the development workflow and conventions,
then open a PR against `main`. Builds must stay green — `dotnet build` and `dotnet test`, plus the
dashboard's `pnpm build`.

## License

Licensed under the **Apache License, Version 2.0** — see [LICENSE](LICENSE) and [NOTICE](NOTICE).
