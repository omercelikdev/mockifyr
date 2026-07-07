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

Six locales — English, Türkçe, Français, العربية (RTL), 中文, 日本語 — in `src/lib/i18n.ts`.
Switching to Arabic flips the whole layout to RTL via logical CSS properties.
