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

```bash
docker pull ghcr.io/omercelikdev/mockifyr:latest      # or a pinned tag, e.g. :0.1.1

docker run -p 8080:8080 -v "$PWD/mappings:/work" \
  ghcr.io/omercelikdev/mockifyr:latest --root-dir /work
```

- Mock surface — `http://localhost:8080`
- Admin API — `http://localhost:8080/__admin`
- Dashboard — `http://localhost:8080/__mockifyr`

Runs on `linux/amd64` and `linux/arm64` (Apple Silicon included).

Compose:

```bash
docker compose up                                   # ephemeral, file-backed mappings (./mappings)
docker compose -f docker-compose.postgres.yml up    # durable PostgreSQL persistence
docker compose -f docker-compose.redis.yml up       # durable Redis persistence
```

The Postgres/Redis variants write stubs through to a datastore, so they survive a restart (and
`--change-feed` keeps multiple instances coherent) — see
[docker-compose.postgres.yml](docker-compose.postgres.yml) and
[docker-compose.redis.yml](docker-compose.redis.yml).

### Local (.NET 10 SDK)

```bash
dotnet run --project src/Mockifyr.Server -- --port 8080 --root-dir ./mappings
```

### Engine only (no dashboard)

The dashboard is opt-in via `--dashboard`; omit it to serve just the mock surface + admin API.

```bash
# Local
dotnet run --project src/Mockifyr.Server -- --port 8080 --root-dir ./mappings

# From the image (override the entrypoint to drop the built-in --dashboard)
docker run -p 8080:8080 --entrypoint dotnet \
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

The hot path is always in-memory; a durable backend is opt-in and writes through.

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
