# HHA Pulse — Distribution Strategy (April 2026)

## One MSIX Package, One Store Listing

**Jeden balík obsahuje:**
- HHA Pulse Overlay (FullTrustProcess, WinUI 3) — paid features
- HHA Pulse Widget (UWP XAML, Game Bar SDK) — always free
- Free download from Microsoft Store
- IAP unlocks overlay features ($1.99 / 49 CZK / 1.99 EUR)

**Proč jeden MSIX:**
- Named Pipe IPC uvnitř jednoho package = žádné Store policy exceptions
- Jeden submission, jedna certifikace
- Jednodušší pro uživatele (jeden install)
- Widget a overlay sdílí Package SID

## Business Model: Freemium

| Feature | Free | Paid (IAP) |
|---------|------|------------|
| Game Bar Widget (všechny modes) | Yes | — |
| Overlay: Minimal preset | Yes | — |
| Battery monitoring | Yes | — |
| FPS display (pokud PresentMon installed) | Yes | — |
| Standard/Tuner/Diagnostic presety | — | $1.99 |
| Custom mode + Metric Picker | — | $1.99 |
| FrameGen + Bottleneck + Latency | — | $1.99 |
| Side panel + grafy | — | $1.99 |
| Per-game profily + hotkeys | — | $1.99 |

## Store Account

- **Company account** (povinné pro OSVČ komerční prodej)
- Jednorázový poplatek: **1,720 CZK** (~75 EUR)
- Microsoft Store podepisuje MSIX automaticky — žádný EV certifikát potřeba

## Pricing

- Base: $1.99 USD (IAP add-on)
- Override CZK: 49 CZK
- Override EUR: 1.99 EUR
- Scale to 2.99 EUR po přidání FPS/profiles features

## Cross-Sell Flow

```
Google/SEO → handheldally.com → Store deep link
Xbox button → Game Bar → free widget → "Unlock" → IAP purchase
Microsoft Store search → HHA Pulse → free download → IAP
handheldally.com → promo section → Store deep link
```

## External Dependencies (NOT bundled)

| Dependency | Required? | What it enables |
|-----------|-----------|----------------|
| PresentMon | Recommended | FPS, frametime, GPU Busy, latency, FrameGen |
| PawnIO | Optional | CPU temp (MSR), fan speed, RAPL power |

App NEVER installs, downloads, or bundles these. Certification notes: "Optionally detects PresentMon/PawnIO if installed. Works without them."

## Store Checklist

- [ ] Company account (1,720 CZK)
- [ ] App name: "HHA Pulse"
- [ ] IAP add-on: "HHA Pulse Pro" ($1.99/49 CZK/1.99 EUR)
- [ ] Privacy policy URL (handheldally.com)
- [ ] Screenshots: overlay on game, widget in Game Bar, settings
- [ ] IARC age rating
- [ ] MSIX built with `msbuild /p:UapAppxPackageBuildMode=StoreUpload`
- [ ] runFullTrust justified in certification notes
- [ ] External dependency disclosure
- [ ] Startup task declared
- [ ] Widget registered via microsoft.gameBarUIExtension
- [ ] ProxyStub COM interfaces in manifest
