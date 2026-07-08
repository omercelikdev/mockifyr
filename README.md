# Mockifyr

**An independent, .NET-based API mock engine + platform.** A transport-agnostic request-matching and
response engine with first-class multi-tenancy, pluggable persistence, and thin facades — in-process
library · HTTP server · admin REST · gRPC · GraphQL · WebSocket. Clean-room codebase with its own IP
and no third-party mock-engine dependencies.

## Quick start

### Docker — one image (engine + admin API + dashboard)

```bash
docker run -p 8080:8080 -v "$PWD/mappings:/work" \
  ghcr.io/omercelikdev/mockifyr:latest --root-dir /work
```

- Mock surface — `http://localhost:8080`
- Admin API — `http://localhost:8080/__admin`
- Dashboard — `http://localhost:8080/__mockifyr`

Or `docker compose up` for a batteries-included setup — see [docker-compose.yml](docker-compose.yml).

### Local (.NET 10 SDK)

```bash
dotnet run --project src/Mockifyr.Server -- --port 8080 --root-dir ./mappings
```

## Configuration

Everything is a CLI flag. The common ones:

| Flag | Effect |
|------|--------|
| `--port <n>` | mock-serving HTTP port (default 8080) |
| `--https-port <n>` | enable HTTPS / HTTP2 |
| `--root-dir <dir>` | load and persist stubs as JSON files |
| `--dashboard <dir>` | serve the built dashboard under `/__mockifyr` |
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
