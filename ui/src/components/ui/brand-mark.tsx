// The Mockifyr mark: two chevrons facing each other — a request going out, a response coming back —
// joined by a wing, with the eye as the single accent. Drawn on the brand's tight 125x63 viewBox, where
// the ink (round stroke caps included) touches all four edges, so it centres in any box without nudging.
//
// It sits directly on the surface rather than inside a filled tile: on a light background the mark is
// near-black, on a dark one near-white. That is what the stroke being currentColor buys — one element
// follows the theme, with no flash and no second request from swapping a black and a white file. The
// filled tile is the favicon's job, where an icon has to hold its own against a browser chrome it does
// not control; in the app the mark can simply be the mark.
//
// Every stroke here is diagonal, so its edges antialias at any size — that softness is inherent, not a
// resolution problem, and the only real remedy is more pixels. Hence 50px in the header rather than the
// 40px that first looked "enough": at 50px the 10-unit stroke lands on exactly 4 device-independent
// pixels, and the counters have room to stay open.

export function BrandMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 125 63" fill="none" xmlns="http://www.w3.org/2000/svg" className={className} role="presentation">
      <g stroke="currentColor" strokeWidth="10" strokeLinecap="round" strokeLinejoin="round">
        <path d="M5 5 L42 31.5 L5 58" />
        <path d="M120 5 L83 31.5 L120 58" />
        <path d="M42 31.5 L62.5 46 L83 31.5" />
      </g>
      <circle cx="62.5" cy="15" r="7" fill="var(--brand)" />
    </svg>
  )
}
