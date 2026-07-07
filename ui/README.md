# Mockifyr Dashboard (`ui/`)

The admin dashboard for Mockifyr — a decoupled single-page app that talks **only** to the mock
server's `/__admin/*` REST API. It cannot reach the engine directly, so it can never affect
matching/serving; the .NET side is untouched by this project.

## Stack

- **React 19 + TypeScript + Vite**
- **Tailwind CSS v4** with a token-first design system (`src/index.css`)
- **shadcn/ui**-style components on **Radix** primitives (`src/components/ui/`)
- **React Router**, **react-i18next** (6 locales incl. RTL), **lucide-react** icons

## Design system

All colors/radii are CSS variables in `src/index.css`. Re-skinning a tenant (or lifting this into
the omercelik.dev starter pack) is a one-file change — override `--primary` and, if wanted, the
neutral ramp. The accent default is near-black; dark mode is class-driven (`.dark`). Semantic status
colors (success/warning/danger/info/violet) are deliberately separate from the accent. The shell,
sidebar collapse/tooltip behavior, and rounded scroll-surface mirror the Praxis design language.

## Develop

```bash
pnpm install
pnpm dev        # http://localhost:5173, proxies /__admin/* to a Mockifyr host on :8080
pnpm build      # type-check + production build to dist/
```

Run a Mockifyr host alongside (`dotnet run --project src/Mockifyr.Server -- --port 8080`) for live
admin data. In production the built `dist/` is served as static assets by the host.

## Internationalization

Six locales — English, Türkçe, Français, العربية (RTL), 中文, 日本語 — in `src/lib/i18n.ts`,
all fully translated. Switching to Arabic flips the whole layout to RTL via logical CSS properties.
Press **⌘K / Ctrl-K** anywhere for the command palette.

## Deploy

Two options:

- **Embedded in the host** — `pnpm build:embedded` builds the dashboard under the `/__mockifyr/` base;
  run the host with `--dashboard <path-to-dist>` and it is served at `/__mockifyr` (static assets + SPA
  fallback), scoped so the mock-serving surface on every other path is untouched. The repo `Dockerfile`
  does this end-to-end — one image serving the mock engine, admin API, and dashboard.

  ```bash
  docker build -t mockifyr .
  docker run -p 8080:8080 mockifyr           # dashboard at http://localhost:8080/__mockifyr
  ```

- **Standalone** — `pnpm build` (base `/`) and serve `dist/` from any static host / CDN, pointed at a
  Mockifyr host's `/__admin/*` (set up a proxy or CORS as needed).
