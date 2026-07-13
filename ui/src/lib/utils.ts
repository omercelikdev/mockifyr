import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

/** Merge conditional class names, de-duplicating conflicting Tailwind utilities. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/** Full local date-time for an ISO timestamp (e.g. "2026-07-13 14:32:05") — the journal's date column. */
export function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—'
  const then = new Date(iso)
  if (Number.isNaN(then.getTime())) return '—'
  const p = (n: number, w = 2) => String(n).padStart(w, '0')
  return `${then.getFullYear()}-${p(then.getMonth() + 1)}-${p(then.getDate())} ${p(then.getHours())}:${p(then.getMinutes())}:${p(then.getSeconds())}`
}

/** Compact human-readable "time ago" for an ISO timestamp (e.g. "just now", "5m ago", "2h ago", "3d ago"). */
export function timeAgo(iso: string | null | undefined): string {
  if (!iso) return '—'
  const then = Date.parse(iso)
  if (Number.isNaN(then)) return '—'
  const s = Math.max(0, Math.round((Date.now() - then) / 1000))
  if (s < 5) return 'just now'
  if (s < 60) return `${s}s ago`
  const m = Math.floor(s / 60)
  if (m < 60) return `${m}m ago`
  const h = Math.floor(m / 60)
  if (h < 24) return `${h}h ago`
  const d = Math.floor(h / 24)
  if (d < 7) return `${d}d ago`
  const w = Math.floor(d / 7)
  if (w < 5) return `${w}w ago`
  const mo = Math.floor(d / 30)
  if (mo < 12) return `${mo}mo ago`
  return `${Math.floor(d / 365)}y ago`
}
