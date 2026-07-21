// The Mockifyr mark: two chevrons facing each other — a request going out, a response coming back —
// joined by a wing, with the eye as the single accent. Drawn on the brand's tight 125x63 viewBox, where
// the ink (round stroke caps included) touches all four edges, so it centres in any box without nudging.
//
// It sits directly on the surface rather than inside a filled tile: on a light background the mark is
// near-black, on a dark one near-white. That is what the stroke being currentColor buys — one element
// follows the theme, with no flash and no second request from swapping a black and a white file. The
// filled tile is the favicon's job, where an icon has to hold its own against a browser chrome it does
// not control; in the app the mark can simply be the mark.

export function BrandMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 125 63" fill="none" xmlns="http://www.w3.org/2000/svg" className={className} role="presentation">
      <g stroke="currentColor" strokeWidth="13" strokeLinecap="round" strokeLinejoin="round">
        <path d="M6.5 6.5 L46.5 31.5 L6.5 56.5" />
        <path d="M118.5 6.5 L78.5 31.5 L118.5 56.5" />
        <path d="M46.5 31.5 L62.5 42.5 L78.5 31.5" />
      </g>
      <circle cx="62.5" cy="16.5" r="7.5" fill="var(--brand)" />
    </svg>
  )
}
