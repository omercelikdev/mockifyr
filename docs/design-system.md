# Mockifyr Design System — Reference

The complete design language of the Mockifyr dashboard, extracted so it can be reused verbatim in
other apps/dashboards. Everything is **token-driven** (one CSS file re-skins the whole app),
**theme-aware** (light/dark, class-driven), **RTL-ready**, and built on **Tailwind CSS v4** + a small
set of headless primitives (Radix). Copy the tokens + the primitives and you have the same look.

> Stack: React 19 · Vite · **Tailwind v4** (`@theme inline`) · Radix UI (dropdown, dialog, tabs,
> tooltip, switch) · cmdk (command palette) · CodeMirror 6 (code editor) · lucide-react (icons).

---

## 1. Design principles

1. **A quiet frame, lively content.** The chrome (frame, sidebar) is near-white/near-black and calm;
   colour only appears in *status* (badges, chips) and one *accent* motif. The accent is intentionally
   **near-black** (`--primary: #18181b`), not a brand hue — swap one token to re-skin.
2. **The "oval".** Content lives on a single **large rounded surface** (`radius-2xl` = 20px) that never
   scrolls the page — only the surface scrolls. Rounded corners everywhere, consistent radius scale.
3. **Three tones of grey stack depth:** `--app` (frame) → `--surface` (the rounded body, subtly greyer)
   → `--background` (white cards/panels lifted above the surface). This is how panels read without
   heavy borders/shadows.
4. **Semantic status is separate from accent.** success/warning/danger/info/violet each have a
   `fg / bg / border` triplet, used only for meaning (methods, status codes, states).
5. **Density with air.** Compact rows (13px) but always small breathing room (`space-y-0.5`), never
   cramped. Hover reveals affordances (delete, add, resize) instead of always showing them.
6. **Progressive disclosure.** Collapsible sidebar, collapsible tree, tabs, slide-overs, ⌘K palette —
   surface complexity only on demand.

---

## 2. Foundations

### 2.1 Design tokens (the single source of truth)

One file (`index.css`). Tailwind v4 maps them via `@theme inline`, so `bg-primary`,
`text-muted-foreground`, `border-border`, `rounded-2xl` all resolve from these.

```css
@import "tailwindcss";
@custom-variant dark (&:where(.dark, .dark *));   /* dark mode = .dark on <html> */

:root {
  /* Neutral depth stack */
  --app: #ffffff;          /* outer frame — same tone as the sidebar */
  --surface: #f5f5f7;      /* the rounded body area — subtle grey, separates from white sidebar/cards */
  --background: #ffffff;   /* cards / panels — lifted above the surface */
  --foreground: #18181b;   /* primary text */
  --muted: #ececef;        /* segmented control / hover / chips (a touch darker than surface) */
  --muted-foreground: #71717a;
  --faint: #a1a1aa;        /* tertiary text, counts, placeholders */
  --border: #e6e6e9;
  --border-strong: #d7d7db;
  --input: #e6e6e9;
  --ring: #18181b;

  /* Accent — swap --primary to re-skin the whole app */
  --primary: #18181b;      --primary-foreground: #ffffff;
  --accent: #f1f1f3;       --accent-foreground: #18181b;   /* soft fill: nav hover/active */

  --sidebar: #ffffff; --sidebar-foreground: #18181b;
  --sidebar-accent: #f1f1f3; --sidebar-accent-foreground: #18181b;

  /* Semantic status ramp (fg / bg / border) */
  --success: #15803d; --success-bg: #f0fdf4; --success-border: #bbf7d0;
  --warning: #b45309; --warning-bg: #fffbeb; --warning-border: #fde68a;
  --danger:  #b91c1c; --danger-bg:  #fef2f2; --danger-border:  #fecaca;
  --info:    #1d4ed8; --info-bg:    #eff6ff; --info-border:    #bfdbfe;
  --violet:  #6d28d9; --violet-bg:  #f5f3ff; --violet-border:  #ddd6fe;

  --radius: 0.625rem;      /* 10px — base radius */
  --shadow-surface: 0 1px 2px 0 rgb(24 24 27 / 0.04), 0 3px 14px 0 rgb(24 24 27 / 0.05);
}

.dark {
  --app: #0a0a0c; --surface: #101014; --background: #17171c; --foreground: #f4f4f5;
  --muted: #202027; --muted-foreground: #a1a1aa; --faint: #71717a;
  --border: #26262b; --border-strong: #37373e; --input: #26262b; --ring: #d4d4d8;
  --primary: #fafafa; --primary-foreground: #18181b; --accent: #202026; --accent-foreground: #fafafa;
  --sidebar: #0f0f13; --sidebar-foreground: #f4f4f5; --sidebar-accent: #202026; --sidebar-accent-foreground: #fafafa;
  --success: #4ade80; --success-bg: #0c1f14; --success-border: #14532d;
  --warning: #fbbf24; --warning-bg: #211803; --warning-border: #78350f;
  --danger:  #f87171; --danger-bg:  #260d0d; --danger-border:  #7f1d1d;
  --info:    #60a5fa; --info-bg:    #0b1830; --info-border:    #1e3a8a;
  --violet:  #a78bfa; --violet-bg:  #17102b; --violet-border:  #4c1d95;
  --shadow-surface: 0 1px 2px 0 rgb(0 0 0 / 0.4);
}
```

**Re-skinning:** override `--primary` (+ `--primary-foreground`), optionally the neutral ramp — nothing
else changes. To brand it, set `--primary` to your hue; to keep the calm look, leave it near-black.

### 2.2 Radius scale — the "oval" system

Every corner derives from `--radius` (10px). Bigger surfaces get bigger radii:

| Token | Value | Use |
|-------|-------|-----|
| `rounded-sm` | 6px | tiny chips, inline code |
| `rounded-md` | 8px | badges, method chips, small buttons |
| `rounded-lg` | **10px** | **default** — buttons, inputs, list rows, menu items |
| `rounded-xl` | 14px | segmented controls (`TabsList`), tenant/profile cards |
| `rounded-2xl` | **20px** | **the content oval**, modals, cards, the main surface |
| `rounded-full` | 9999px | count pills, dots, avatars |

```css
@theme inline {
  --radius-sm: calc(var(--radius) - 4px);   /* 6  */
  --radius-md: calc(var(--radius) - 2px);   /* 8  */
  --radius-lg: var(--radius);               /* 10 */
  --radius-xl: calc(var(--radius) + 4px);   /* 14 */
  --radius-2xl: calc(var(--radius) + 10px); /* 20 */
}
```

Rule of thumb: **the larger the surface, the larger the radius.** Page surface = `2xl`; controls =
`lg`; chips = `md`.

### 2.3 Typography

```css
--font-sans: ui-sans-serif, -apple-system, "Segoe UI", Roboto, "Helvetica Neue",
  "PingFang SC", "Hiragino Sans", "Noto Sans", "Noto Sans Arabic", Arial, sans-serif;
--font-mono: ui-monospace, "SF Mono", "JetBrains Mono", "Cascadia Code", Menlo, Consolas, monospace;
```

Base body = **14px** (`font-size: 14px` on `body`), antialiased. The type scale in practice:

| Size | Where |
|------|-------|
| `text-[22px] font-bold tracking-tight` | page H1 |
| `text-base font-semibold` (16px) | section/empty-state titles |
| `text-sm` (14px) | body, inputs, buttons, menu items |
| `text-[13px]` / `text-[12.5px]` | tree rows, tabs, table cells, small buttons |
| `text-xs` (12px) | labels, hints, tooltips |
| `text-[11px]` / `text-[11.5px]` | badges, count pills, method/status chips |
| `text-[10.5px] font-semibold uppercase tracking-wider` | sidebar section headers |

Mono (`font-mono`) is used for URLs, code, method/status chips, header values, JSON.

### 2.4 Shadows & depth

Only **one** shadow (`shadow-surface`) — a whisper, for the main surface and floating cards. Depth is
carried mostly by the grey stack (`app`→`surface`→`background`), not by shadows. Overlays
(sheets/dialogs) use `shadow-2xl`.

### 2.5 Theming, RTL, motion, scrollbars

- **Dark mode:** class-driven (`.dark` on `<html>`) — toggle by adding/removing the class; all tokens
  flip. Never hard-code colours; always use token utilities so both themes work for free.
- **RTL:** use **logical** Tailwind properties everywhere — `ms-*`/`me-*`, `ps-*`/`pe-*`, `start`/`end`,
  `rtl:rotate-180` on directional icons. The app ships an Arabic (RTL) locale.
- **Reduced motion:** `@media (prefers-reduced-motion: reduce) { *{ transition:none; animation:none } }`.
- **Auto-hiding scrollbar** (`.scroll-area`): invisible until the region is hovered/scrolled. Apply it
  to any scroll container.
  ```css
  .scroll-area { scrollbar-width: thin; scrollbar-color: transparent transparent; }
  .scroll-area:hover, .scroll-area:focus-within { scrollbar-color: var(--border-strong) transparent; }
  .scroll-area::-webkit-scrollbar { width: 11px; height: 11px; }
  .scroll-area::-webkit-scrollbar-thumb { background: transparent; border-radius: 9999px; border: 3px solid var(--background); }
  .scroll-area:hover::-webkit-scrollbar-thumb { background: var(--border-strong); }
  ```
- **Focus:** global `:focus-visible { outline: 2px solid var(--ring); outline-offset: 2px }` for
  buttons/links; inputs opt out of the ring and use a **border tint** on focus instead
  (`focus:border-border-strong`) so they don't get an ugly box.

---

## 3. Layout architecture

### 3.1 The app-shell frame (the page never scrolls)

```
┌───────────────────────────────────────────────┐  bg-app, h-dvh, overflow-hidden
│ sidebar │  ┌─────────────────────────────────┐ │
│ (fixed) │  │  main = the rounded "oval"       │ │  ← only this scrolls (.scroll-area)
│         │  │  bg-surface, rounded-2xl, border │ │
│         │  │  shadow-surface, p-6/p-7         │ │
│         │  └─────────────────────────────────┘ │
└───────────────────────────────────────────────┘
```

```tsx
<div className="flex h-dvh overflow-hidden bg-app">
  <aside className={cn('shrink-0 bg-sidebar transition-[width] duration-300 ease-[cubic-bezier(.4,0,.2,1)]',
                       collapsed ? 'w-[74px]' : 'w-[252px]')}>
    <Sidebar />
  </aside>
  <div className="min-w-0 flex-1 p-3 pe-3 ps-0">
    <main className="scroll-area h-full overflow-y-auto rounded-2xl border border-border bg-surface p-6 shadow-surface md:p-7">
      <Outlet />
    </main>
  </div>
</div>
```

- The **page** is fixed (`h-dvh overflow-hidden`); scrolling belongs to `main` only.
- The sidebar sits flush-left; the content oval floats with a small gutter (`p-3 ps-0`).
- **Full-bleed variant** (for a workspace that manages its own scroll, e.g. a tree + tabs screen):
  drop the padding + inner scroll and let the screen fill the oval edge-to-edge:
  ```tsx
  <main className={cn('h-full rounded-2xl border border-border bg-surface shadow-surface',
                      bleed ? 'overflow-hidden' : 'scroll-area overflow-y-auto p-6 md:p-7')}>
  ```

### 3.2 Collapsible sidebar

- **Widths:** expanded `252px`, collapsed `74px` (icon-rail). Animate `transition-[width] duration-300
  ease-[cubic-bezier(.4,0,.2,1)]`.
- **Structure (top→bottom):** logo/brand (row `h-11`, collapses to a 32px square) · search button
  (opens ⌘K) · nav groups · footer (tenant switcher + profile menu). Each pinned; the nav list is the
  only scroll region (`.scroll-area`).
- **Nav groups:** a `text-[10.5px] font-semibold uppercase tracking-wider text-faint` header, then rows.
  When collapsed the header becomes a thin `h-px bg-border` divider.
- **Nav row:** `group relative mb-0.5 flex h-9 items-center rounded-lg text-sm font-medium
  transition-colors`; active = `bg-muted text-foreground`, idle = `text-muted-foreground
  hover:bg-muted/60`. Optional trailing count badge.
- **Collapsed = icon + tooltip.** Every collapsed control is a centered `size-9`/`w-10` icon button with
  a Radix tooltip (label on hover) so nothing loses its name:
  ```tsx
  <TooltipProvider delayDuration={0}>
    <Tooltip><TooltipTrigger asChild><IconButton .../></TooltipTrigger>
      <TooltipContent side="right">{label}</TooltipContent></Tooltip>
  </TooltipProvider>
  ```
- **Toggle:** a chevron button flips `collapsed` (persist to `localStorage`); icon uses `rtl:rotate-0`.

### 3.3 Two-zone workspace (list | splitter | content)

For master–detail screens, split the oval into a **grey list panel** and a **white content panel** with
a **draggable hairline splitter** — inside one oval, no nested cards.

```tsx
<div className="flex h-full min-h-0 overflow-hidden">
  <aside style={{ width: treeWidth }} className="flex shrink-0 flex-col overflow-hidden">…list…</aside>
  {/* splitter: a hairline that firms up subtly on hover/drag */}
  <div onPointerDown={onSplitterDown} className="group relative z-10 w-px shrink-0 cursor-col-resize bg-border">
    <div className="absolute inset-y-0 -inset-x-1 transition-colors group-hover:bg-border-strong/50" />
  </div>
  <section className="flex min-w-0 flex-1 flex-col overflow-hidden bg-background">…content…</section>
</div>
```

- List panel is transparent (shows `surface` grey); content panel is `bg-background` (white) — the two
  zones read from the **background tone**, not from borders.
- **Splitter drag** = pointer events with a clamp; persist width to `localStorage`:
  ```tsx
  const clamp = (n,lo,hi) => Math.min(hi, Math.max(lo, n))
  const onSplitterDown = (e) => {
    e.preventDefault(); const startX = e.clientX, startW = treeWidth
    const move = (ev) => setTreeWidth(clamp(startW + ev.clientX - startX, 220, 560))
    const up = () => { window.removeEventListener('pointermove', move); window.removeEventListener('pointerup', up); document.body.style.cursor='' }
    window.addEventListener('pointermove', move); window.addEventListener('pointerup', up)
    document.body.style.cursor = 'col-resize'
  }
  ```
- Hover highlight is **subtle** (`border-strong/50`), never a loud colour.

---

## 4. Components (exact recipes)

All use `cn()` = `twMerge(clsx(...))` so callers can override any class.

### 4.1 Button

```
base:    inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg
         text-sm font-semibold transition-colors focus-visible:ring-2 focus-visible:ring-ring
         disabled:opacity-50 [&_svg]:size-4 [&_svg]:shrink-0
variant: primary  = bg-primary text-primary-foreground hover:opacity-90
         outline  = border border-border bg-background hover:bg-muted
         ghost    = hover:bg-muted
         subtle   = bg-muted text-foreground hover:bg-muted/70
         danger   = bg-danger text-white hover:opacity-90
size:    default  = h-9 px-3.5
         sm       = h-8 px-3 text-[13px]
         icon     = size-9
         iconSm   = size-8
```
Primary is the near-black CTA; ghost for toolbar icons; outline for secondary. Use `forwardRef` so a
button can be a Radix `Tooltip`/`Dialog` trigger.

### 4.2 Inputs

- **Input / NativeSelect:** `h-9 w-full rounded-lg border border-input bg-background px-3 text-sm
  outline-none transition-colors placeholder:text-muted-foreground focus:border-border-strong`.
  `NativeSelect` replaces the OS chevron with a `lucide` `ChevronDown` (absolute `end-2.5`) for a
  consistent look across locales.
- **Textarea:** same, with `py-2`, no fixed height.
- **Label:** `mb-1.5 block text-xs font-semibold text-muted-foreground`.
- **SearchBox** (commit-on-Enter, not per-keystroke): a `label` wrapper `flex h-9 items-center gap-2
  rounded-lg border border-border bg-muted/50 px-3 focus-within:border-border-strong`, a leading
  `Search` icon, and a trailing clear `X`. Commits on Enter / clears on the button.

### 4.3 Badges & chips

- **MethodChip:** `inline-flex rounded-md border px-2 py-0.5 font-mono text-[11px] font-bold` + a tone
  per HTTP method: GET=`info`, POST=`success`, PUT=`warning`, DELETE=`danger`, PATCH=`violet`, else
  neutral (`text-muted-foreground bg-muted border-border`).
- **StatusCode chip:** `inline-flex min-w-[2.5rem] justify-center rounded-md border px-1.5 py-0.5
  font-mono text-[11px] font-bold tabular-nums`; tone by class: `<300` success, `<400` info, `<500`
  warning, else danger; null = neutral `—`.
- **StatusPill** (dot + label): `inline-flex items-center gap-1.5 rounded-full border px-2.5 py-0.5
  text-[11.5px] font-semibold` with a `size-1.5 rounded-full` dot.

General chip pattern = **the status triplet**: `text-{tone} bg-{tone}-bg border-{tone}-border`.

### 4.4 Tabs (segmented control)

```tsx
<TabsList  className="inline-flex gap-1 rounded-xl bg-muted p-1" />
<TabsTrigger className="rounded-lg px-3.5 py-1.5 text-sm font-semibold text-muted-foreground
   hover:text-foreground data-[state=active]:bg-background data-[state=active]:text-foreground
   data-[state=active]:shadow-sm" />
```
A pill track (`bg-muted`) with the active tab lifted to `bg-background` + `shadow-sm`. (Radix Tabs.)

### 4.5 Tooltip

Radix tooltip; content = `rounded-md bg-foreground px-2.5 py-1.5 text-xs font-medium text-background
shadow-md` with a fade-in. Wrap a region in one `TooltipProvider` (set `delayDuration`). Used for every
icon-only control.

### 4.6 Slide-over sheet & centered dialog

- **Sheet** (right slide-over, e.g. detail/editor): overlay `bg-black/40`; content `fixed inset-y-0
  end-0 z-50 flex w-full max-w-[680px] flex-col border-s border-border bg-background shadow-2xl` with
  `slide-in-from-right`. Header `border-b px-6 py-4` (title `text-base font-semibold`). Close `X` at
  `end-4 top-4`.
- **Centered dialog** (e.g. reference popup): `fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2
  rounded-2xl border bg-background shadow-2xl` sized `h-[76vh] max-h-[680px] w-[92vw] max-w-[880px]`
  with `zoom-in-95 fade-in-0`. A search bar + a left category rail (`space-y-0.5`) + a scrollable body.

### 4.7 Dropdown menu

Radix dropdown; content `rounded-lg border bg-background p-1 shadow-md`; items `flex cursor-pointer
items-center gap-2.5 rounded-lg px-2.5 py-2 text-sm data-[highlighted]:bg-muted`. Multi-select facet
menus keep the menu open (`onSelect: preventDefault`) with a check-box square per row.

### 4.8 Empty states + line-art illustrations

Never leave a blank area — show a **line-art SVG** (drawn from tokens, so it flips with the theme) + a
title + one line + optional actions.

```tsx
<EmptyState art={<StubsArt/>} title="No stubs yet"
  body="Create your first stub or import a bundle to start mocking."
  action={<><Button variant="primary" size="sm">New</Button><Button variant="outline" size="sm">Import</Button></>} />
```

Illustration grammar: a neutral surface shape (`fill var(--muted)`, `stroke var(--border-strong)`,
`stroke-width 2.5`, rounded caps) + **one accent motif** in `var(--violet)` (a `+` badge, a highlighted
row + cursor, connected nodes, a pulse line, a record dot in `var(--danger)`, a dashed "add" tile).
Fixed size ~164×120, `role="presentation"`. Because it's pure tokens, it needs no dark-mode variant.

### 4.9 Code editor (CodeMirror 6)

CodeMirror themed to the tokens: transparent background, gutters in `--faint`, active line
`color-mix(var(--muted) 45%)`, selection `color-mix(var(--info) 22%)`. Syntax colours map to the status
ramp — keys=`--info`, strings=`--success`, numbers=`--warning`, bool/null=`--violet`,
punctuation=`--muted-foreground`. Wrap it in a **framed field** with a floating top-right toolbar
(Beautify / Copy / Upload) that appears on hover; **Copy flips to a check for ~1.2s** (inline, no toast).

### 4.10 Tree (grouped, collapsible, connector lines)

Path→group→leaf tree with hairline connectors that **align under the parent chevron**:

```tsx
// each nested level:
<div className="ms-[15px] mt-1 space-y-0.5 border-s border-border/70 ps-1.5">…children…</div>
```
- `ms-[15px]` = row padding (8) + half a chevron (7) → the connector drops straight under the chevron.
- Rows: `flex items-center gap-1.5 rounded-lg px-2 py-1 text-[13px]`; folders =
  `text-muted-foreground`, active leaf = `bg-muted font-medium text-foreground`, idle =
  `hover:bg-muted/60`.
- Chevron `size-3.5 transition-transform`, `rotate-90` when open.
- Trailing **count badge** `text-[11px] tabular-nums text-faint`; on row-hover it swaps for a `+`
  (add-here) via `group-hover:hidden` / `opacity-0 group-hover:opacity-100`.
- Delete/add actions are `opacity-0 group-hover:opacity-100` (revealed on hover).

### 4.11 Table (list view)

`w-full border-collapse`; header cells `border-b bg-muted/40 px-4 py-2.5 text-[11px] font-semibold
uppercase tracking-wide text-muted-foreground` with a sort button + `ArrowUpDown` icon; body rows
`border-b hover:bg-muted/40`, cells `px-4 py-3` (or `py-2` in dense mode). A **density toggle**
(`Rows2`/`Rows3`) switches row padding. Footer bar `bg-muted/30 px-4 py-3 text-[12.5px]` with
"Showing N of M" + pager. Rows are clickable to open a detail sheet.

### 4.12 Command palette (⌘K)

`cmdk` dialog, centered `pt-[16vh]`, panel `w-[640px] rounded-2xl border bg-background`. Opened by
`⌘/Ctrl-K` or a global `window` event. Groups ("Go to", "Actions") of items `flex items-center gap-2.5
rounded-lg px-3 py-2 text-sm data-[selected=true]:bg-muted`. Any feature can register a command; a
global event (`window.dispatchEvent(new Event('open-x'))`) opens a single app-wide instance.

---

## 5. Interaction & motion patterns

| Pattern | How |
|---------|-----|
| **Collapse** | width transition 300ms `cubic-bezier(.4,0,.2,1)`; persist to `localStorage`; collapsed = icon + tooltip |
| **Resizable splitter** | pointer events + `clamp(min,max)`; `localStorage`; subtle hover firm-up |
| **Tabs persist** | open tabs kept in `localStorage` (per scope); all mounted, inactive `hidden`, so unsaved edits survive switching |
| **Inline confirm** | destructive actions arm a small in-place **✓ / ✗** (no browser popup); only ✓ commits |
| **Copy → tick** | Copy flips to a `Check` for ~1.2s inline, then reverts — **no toast** |
| **Hover-reveal** | delete/add/resize affordances are `opacity-0 group-hover:opacity-100` |
| **Commit-on-Enter search** | search commits on Enter, not per keystroke; clear resets |
| **Reset page on filter** | changing search/filters resets pagination to page 1 |
| **Motion vocabulary** | `transition-colors` on interactives; `fade-in-0`/`zoom-in-95`/`slide-in-from-right` for overlays; everything killed under reduced-motion |

Toasts (sonner) are for **async results** (saved/deleted/error) only — never for a local UI echo like
copy (use the inline tick).

---

## 6. Conventions (rules of thumb)

- **Radius:** bigger surface → bigger radius (`2xl` page/modal, `lg` controls, `md` chips).
- **List spacing:** `space-y-0.5` between rows — the standard "a hair of air", never flush.
- **Row height:** `py-1.5` comfortable, `py-1` dense; text `13px` in dense lists, `14px` for body.
- **Icons:** `size-4` (16px) in buttons/menus, `size-3.5` in dense chips/tree, `size-7` in illustration
  tiles. lucide, `stroke` icons only.
- **Colour discipline:** neutrals for chrome; a status triplet only for meaning; `--primary` for the
  single accent/CTA; `--violet` for the "friendly" empty-state motif.
- **Borders vs background:** prefer the grey stack (`app`/`surface`/`background`) to separate zones;
  use `border-border` hairlines sparingly; `border-border/60–70` for connectors.
- **Always logical properties** (`ms/me/ps/pe/start/end`) for RTL.
- **Every icon-only control gets a tooltip** (or `title`).
- **Never let the page scroll** — a fixed shell owns one scroll region (`.scroll-area`).

---

## 7. Reusing this in a new project

1. **Tailwind v4.** `@import "tailwindcss"` in your CSS.
2. **Drop in the tokens** from §2.1 (`:root`, `.dark`, `@theme inline`, the `body`/`.scroll-area`/focus
   rules from `index.css`). That alone gives you the palette, radius scale, fonts, dark mode, scrollbars.
3. **Copy the primitives** (Button, field inputs, SearchBox, badges, Tabs, Tooltip, Sheet, Dropdown,
   EmptyState + illustrations, the CodeMirror wrapper if needed) — they're ~15 small headless files over
   Radix; each is a thin styled wrapper using only token utilities.
4. **Copy the shell** (§3): the `bg-app` frame + collapsible sidebar + `rounded-2xl bg-surface` content
   oval + `.scroll-area`. This is the whole "look" in ~40 lines.
5. **Follow the conventions** (§6) and you'll get pixel-consistent screens.

Re-skin for a different brand: change `--primary` (+ `--primary-foreground`); optionally warm/cool the
neutral ramp. Everything else re-themes from the tokens.
