// Line-art empty-state illustrations, drawn entirely with our design tokens so they flip with light/dark
// automatically: neutral shapes use --muted / --border-strong, the accent motif uses --primary (and
// --danger for the record dot). Kept simple and cohesive — a soft surface plus one accent glyph each.

const NEUTRAL_FILL = 'var(--muted)'
const NEUTRAL_STROKE = 'var(--border-strong)'
// Accent uses --violet (not --primary, which is near-black): a lively pop that stays in our token ramp
// and echoes the friendly line-art empty states this pattern is modelled on.
const ACCENT = 'var(--violet)'

function Svg({ children }: { children: React.ReactNode }) {
  return (
    <svg width="164" height="120" viewBox="0 0 164 120" fill="none" xmlns="http://www.w3.org/2000/svg" className="shrink-0" role="presentation">
      {children}
    </svg>
  )
}

/** Create your first stub — a card with content lines and an accent "+" badge. */
export function StubsArt() {
  return (
    <Svg>
      <rect x="42" y="24" width="80" height="72" rx="11" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <rect x="56" y="40" width="38" height="6" rx="3" fill={NEUTRAL_STROKE} />
      <rect x="56" y="55" width="52" height="5" rx="2.5" fill={NEUTRAL_STROKE} opacity="0.55" />
      <rect x="56" y="67" width="34" height="5" rx="2.5" fill={NEUTRAL_STROKE} opacity="0.55" />
      <circle cx="116" cy="88" r="18" fill={ACCENT} fillOpacity="0.12" stroke={ACCENT} strokeWidth="2.5" />
      <path d="M116 81v14M109 88h14" stroke={ACCENT} strokeWidth="2.5" strokeLinecap="round" />
    </Svg>
  )
}

/** Nothing open — a list panel with one highlighted row and a cursor. */
export function PickArt() {
  return (
    <Svg>
      <rect x="36" y="27" width="70" height="66" rx="11" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <rect x="49" y="42" width="44" height="5" rx="2.5" fill={NEUTRAL_STROKE} opacity="0.55" />
      <rect x="49" y="54" width="44" height="7" rx="3.5" fill={ACCENT} fillOpacity="0.5" />
      <rect x="49" y="67" width="30" height="5" rx="2.5" fill={NEUTRAL_STROKE} opacity="0.55" />
      <path d="M100 66l30 13-13 3-3 13z" fill="var(--background)" stroke={ACCENT} strokeWidth="2.5" strokeLinejoin="round" />
    </Svg>
  )
}

/** Scenarios — a small state machine, middle node accented. */
export function ScenariosArt() {
  return (
    <Svg>
      <path d="M54 60h20M90 60h20" stroke={NEUTRAL_STROKE} strokeWidth="2.5" strokeLinecap="round" />
      <circle cx="40" cy="60" r="14" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <circle cx="82" cy="60" r="15" fill={ACCENT} fillOpacity="0.12" stroke={ACCENT} strokeWidth="2.5" />
      <circle cx="124" cy="60" r="14" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <path d="M76 60l4 4 8-9" stroke={ACCENT} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
    </Svg>
  )
}

/** Request journal — a panel with an activity pulse line. */
export function JournalArt() {
  return (
    <Svg>
      <rect x="34" y="30" width="96" height="60" rx="11" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <path d="M46 60h9l5-13 8 26 6-13h26" stroke={ACCENT} strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" />
    </Svg>
  )
}

/** Recordings — a panel with a red record dot. */
export function RecordingsArt() {
  return (
    <Svg>
      <rect x="40" y="30" width="84" height="60" rx="11" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <circle cx="82" cy="60" r="14" fill="var(--danger)" fillOpacity="0.14" stroke="var(--danger)" strokeWidth="2.5" />
      <circle cx="82" cy="60" r="4.5" fill="var(--danger)" />
    </Svg>
  )
}

/** Extensions — a 2×2 grid of blocks, the last an accented "add" tile. */
export function ExtensionsArt() {
  return (
    <Svg>
      <rect x="50" y="32" width="30" height="30" rx="8" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <rect x="86" y="32" width="30" height="30" rx="8" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <rect x="50" y="68" width="30" height="30" rx="8" fill={NEUTRAL_FILL} stroke={NEUTRAL_STROKE} strokeWidth="2.5" />
      <rect x="86" y="68" width="30" height="30" rx="8" fill={ACCENT} fillOpacity="0.12" stroke={ACCENT} strokeWidth="2.5" strokeDasharray="4 4" />
      <path d="M101 76v14M94 83h14" stroke={ACCENT} strokeWidth="2.5" strokeLinecap="round" />
    </Svg>
  )
}
