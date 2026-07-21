import { cn } from '@/lib/utils'

// The Mockifyr mark: two chevrons facing each other — a request going out, a response coming back —
// joined by a wing, with the eye as the single accent. Drawn on the brand's tight 125x63 viewBox, where
// the ink (round stroke caps included) touches all four edges, so it centres in any box without nudging.
//
// The stroke is currentColor: one element follows the theme instead of swapping a black and a white file
// on every toggle, which would flash and cost a second request. The eye uses --brand-on-primary because
// the mark's home is the --primary tile, so the accent has to hold against *that*, not against the page.

export function BrandMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 125 63" fill="none" xmlns="http://www.w3.org/2000/svg" className={className} role="presentation">
      <g stroke="currentColor" strokeWidth="13" strokeLinecap="round" strokeLinejoin="round">
        <path d="M6.5 6.5 L46.5 31.5 L6.5 56.5" />
        <path d="M118.5 6.5 L78.5 31.5 L118.5 56.5" />
        <path d="M46.5 31.5 L62.5 42.5 L78.5 31.5" />
      </g>
      <circle cx="62.5" cy="16.5" r="7.5" fill="var(--brand-on-primary)" />
    </svg>
  )
}

/** The mark on its rounded --primary tile — the app's 32px avatar, as used in the sidebar header. */
export function BrandTile({ className }: { className?: string }) {
  return (
    <span
      className={cn(
        'flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-sm',
        className,
      )}
    >
      <BrandMark className="w-[22px]" />
    </span>
  )
}
