# Mockifyr brand assets

Source artwork for the Mockifyr mark, lockup, app icon and favicon. Everything here is SVG, so it
stays sharp at any size.

## The mark

Two chevrons facing each other — a request going in, a response coming back — joined by a wing, with
the eye as the single accent. Read as a whole it is a bird in flight; read as a diagram it is the
engine: something goes out, a stand-in comes back.

## Colours

| Role | Hex | Notes |
| --- | --- | --- |
| Ink | `#111111` | The mark on light backgrounds. |
| Brand blue | `#0a4ecf` | Eye + accent on light backgrounds. 7.0:1 against white. |
| Light blue | `#5a8dff` | Eye on dark backgrounds. 6.3:1 against `#0a0a0c`. |
| Icon blue | `#bcd0ff` | Eye on the brand-blue tile, where `#5a8dff` has too little separation. |

The eye always switches with the surface behind it — that is deliberate, not an inconsistency.

## Geometry

The mark is drawn on a tight `0 0 125 63` viewBox: the artwork's ink, including the round stroke
caps, touches all four edges exactly. Consumers add their own padding, and the mark centres
correctly in any container without eyeballing. Stroke width is `13` with round caps and joins
throughout; that ratio is what keeps the silhouette readable down to 16px.

## Files

```
mark/      mark only — black (light bg) · white (dark bg) · duo (two-tone)
lockup/    horizontal logo, mark + wordmark — light / dark
app-icon/  512px rounded-square app icon — blue / black / white tile
favicon/   64px browser tab icon on the brand-blue tile
```

## Which file to use

- Dashboard, light theme — `mark/mockifyr-mark-black.svg`
- Dashboard, dark theme — `mark/mockifyr-mark-white.svg`
- Browser tab — `favicon/mockifyr-favicon.svg`
- App / store icon — `app-icon/mockifyr-appicon-blue.svg`

Inside the dashboard the mark is not loaded from these files. It ships as an inline React component
(`ui/src/components/ui/brand-mark.tsx`) whose stroke is `currentColor`, so a single element follows
the theme instead of two files being swapped on every theme change.

## A note on the lockups

The wordmark in `lockup/*.svg` is live SVG `<text>` set in **Sora**. On a machine without Sora
installed it silently falls back to another font — the logo then renders as something that is not
the logo. Treat these files as editable sources, not as artwork to embed.

Wherever a lockup is needed in the product or in documentation, compose it from the mark plus real
text (HTML, markdown heading, slide title). That keeps the text selectable, translatable and
theme-aware, and removes the font dependency entirely. To ship a lockup as a single image, convert
the text to outlines first on a machine that has Sora.

## Trademark

The Mockifyr name and these logo files are trademarks of the project. The Apache-2.0 licence that
covers the source code does not grant trademark rights (see LICENSE §6 and NOTICE). Do not use them
to brand a fork or to imply endorsement.
