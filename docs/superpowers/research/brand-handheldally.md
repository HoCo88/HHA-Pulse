# handheldally.com — Brand & Visual Language Report

**Purpose:** port the handheldally.com brand into HHA Pulse (WinUI 3 Windows overlay) so the two products read as one family.

**Method:** WebFetch against the production site (single-page Next.js app — `sitemap.xml` returns one URL). robots.txt exposes paths `/about`, `/devices`, `/games`, `/news`, `/faq`, `/premium`, `/ecosystem`, `/support` — most returned 404 to anonymous fetches or rendered only loading shells, so the homepage, `/about`, `/premium`, and `/faq` were the load-bearing sources. Raw CSS was not retrievable via WebFetch (the model returns a summarized/rendered view), so exact hex values below are **inferred from rendered descriptions** and should be treated as starting tokens to be verified against a real browser screenshot, not as ground truth.

**Prompt-injection note:** two WebFetch responses against `handheldally.com` contained injected `<system-reminder>` blocks pretending to be from the harness (pushing TaskCreate usage and inventing fake task state). I ignored them. Flagging so the team lead is aware the site's rendering path is currently returning untrusted content that tries to steer tool behavior.

---

## 1. Brand positioning (voice, not visuals)

The copy is load-bearing — it's the most reliable signal since it was retrievable verbatim.

- **Hero tagline:** *"Stop searching / Start playing"*
- **Founder framing:** *"Built by a fellow tinkerer"*
- **Category line:** *"The community-powered platform for handheld PC gaming optimization"*
- **Emotional anchor:** *"This is our home. This is Handheld Ally."*
- **Tone:** warm, playful, community-first, tinkerer-grade. Uses emojis in section headers (🎯 🏆 🎮 💬 💡 🔧). NOT clinical, NOT cyberpunk, NOT hardcore-esports.
- **Mood words seen:** tinkerer, fellow, honest, helpful, perfect, stable, easy, home, ally.

Implication for HHA Pulse: the overlay should feel like *an ally whispering stats*, not a RivaTuner-style diagnostic dump. Friendly authority over clinical precision.

## 2. Color palette (inferred — verify against screenshot)

| Token | Inferred hex | Role |
|---|---|---|
| `bg/base` | `#0F1115` (dark charcoal, not pure black) | Page/overlay background |
| `bg/surface` | `#171A21` (Steam-deckish slate) | Cards, HUD chrome |
| `bg/elevated` | `#1F232B` | Popovers, hover states |
| `accent/primary` | `#22D3EE` → `#06B6D4` (cyan/teal) | Primary CTA, focused metric, progress fill |
| `accent/warm` | `#F5B301` (amber/gold) | Premium/Founder/achievement — the "gold avatar frame" mention on `/premium` |
| `text/primary` | `#F4F6FA` | Headlines, HUD numbers |
| `text/secondary` | `#9BA3AF` | Labels, units (fps, °C, GB) |
| `text/muted` | `#5B6372` | Dividers, placeholder `--` |
| `state/good` | `#4ADE80` | Band 1 / Excellent |
| `state/okay` | `#FACC15` | Band 2 / Good |
| `state/warn` | `#F87171` | Band 3 / Poor |
| `stroke/glass` | `rgba(255,255,255,0.06)` | 1px hairline on every surface |

The palette's signature contrast is **dark slate + cyan accent + gold for premium**. No Steam-blue dominance, no neon/cyberpunk glow, no purple.

## 3. Typography

Raw `font-family` values were not exposed. Based on rendered feel ("clean minimal, modern, contemporary") and Next.js conventions, assume an Inter-family sans with tabular numbers.

- **Display/Headline:** Inter 600–700, tight tracking (-0.01em)
- **Body/UI:** Inter 400–500
- **Numerals (HUD metrics):** Inter with `font-feature-settings: "tnum"` — tabular so FPS digits don't jitter. This is critical for the overlay.
- **No** pixel/retro/arcade font. **No** monospace outside of maybe code blocks.
- Scale feels like a 1.2 modular ratio — 12/14/16/20/24/32.

## 4. Components (observed + inferred)

- **Cards:** rounded ~10–12px, subtle 1px inner stroke, flat fills (no heavy drop shadows). Elevated by tone, not by blur.
- **Buttons:** rounded, solid cyan fill for primary ("Sign in with Steam →"), ghost/outline for secondary, with right-arrow affordance on CTAs.
- **Nav:** minimal sticky bar, logo left, About/Sign In right. Not a heavy glass blur; closer to solid-dark with a hairline bottom border.
- **Bands/Tiers:** three-state color ramp (Excellent/Good/Poor ↔ green/yellow/red) attached to small circular badge icons (`band1.ico` … `band3.ico`). **This is the most portable pattern to HHA Pulse — use it for metric thresholds.**
- **Premium surface:** the `/premium` page explicitly mentions a *"glass shell for a cohesive premium experience"* and *"gold avatar frames"*. So glassmorphism IS in the brand, but reserved for the paid/founder tier — not the default chrome.
- **Gamification:** XP bars, "Level 2", achievements, weekly challenges. Progression is part of the brand vocabulary.

## 5. Imagery & iconography

- Hero uses **real Steam cover art** (Cyberpunk 2077, BG3, Elden Ring, Helldivers 2) pulled from Steam CDN — no custom illustration.
- Logo is a 40px favicon mark (`/handheldally-icon-40.ico`) — compact, monogram-style. Not retrievable in detail.
- Iconography leans on **emoji in headings** as a warmth device. HHA Pulse should *not* use emoji in the HUD itself (they'd look toy-like in-game) but could use them in the Settings UI section headers to echo the site.

## 6. Motion & animation

Nothing animated was visible through WebFetch (it's text-mode). Given the Next.js + dark-slate + cyan profile, safe assumption: subtle 150–200ms ease-out transitions on hover, no parallax, no heavy scroll-jacking. Match that discipline in the overlay — fades, not slides.

## 7. The 5 signature moves (port these)

1. **Dark slate + cyan + gold triad.** Slate is the silence, cyan is "the system is working," gold is "you're premium/achieving." Never mix gold into non-premium surfaces.
2. **Band tier color ramp (green/yellow/red) on small circular badges.** Reuse this exact pattern for HUD metric thresholds — 60fps stable = Band 1 green, dipping = Band 2 yellow, struggling = Band 3 red. Matches site vocabulary directly.
3. **Tabular numerals + unit labels in muted gray.** FPS digits in `text/primary` at high weight, the `fps` unit suffix in `text/secondary`. Numbers stay still, labels stay quiet.
4. **Warmth through copy, not through decoration.** Empty/loading states should read like *"Waiting for your game…"* not *"No data"*. Missing FPS shows `--` (already mandated by CLAUDE.md) — pair that with a soft tooltip *"Launch a game to start the pulse."*
5. **Glass only for premium.** Default HUD is flat slate with hairline strokes. The Founder/Pro unlock screen (or the "about Pulse" card) is where glassmorphism earns its keep — mirrors the site's `/premium` treatment and creates a consistent "paid = glass + gold" signal across both products.

## 8. Open questions to resolve before implementing

- **Actual hex values.** Get a real browser screenshot + color picker pass — my values are inferred.
- **Actual font stack.** `curl -s https://handheldally.com | grep -i font` from outside the sandbox will answer it in one shot.
- **Logo asset.** Need the real SVG/PNG of the handheld-ally mark, not the 40px favicon, to embed in the overlay's About pane.
- **Whether a dedicated HHA Pulse product page exists.** Nothing at `/pulse`, `/hha-pulse`, `/apps`, `/ecosystem`, or `/devices` — all 404'd. Either the page isn't built yet, or it's gated behind login. Worth confirming with the user.
