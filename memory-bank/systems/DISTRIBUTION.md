# HHA Pulse — Distribution Strategy

## Sales Channel: Microsoft Store ONLY

**Jediný prodejní kanál = Microsoft Store.** Žádný přímý prodej z webu.

### Microsoft Store (MSIX)
- **Widget:** Free — auto-appears in Game Bar Widget Store
- **Desktop App:** ~2 EUR / 49 CZK / $1.99 USD
- **Revenue:** Microsoft takes 15% (~$0.30 per sale)
- **Benefits:** Discovery, auto-update, user trust, reviews, one-click install
- **One MSIX package:** Widget + Desktop Service bundled together

### handheldally.com (Promo ONLY — no direct sales)
- **Promo stránka** s popisem HHA Pulse
- **Odkaz na Microsoft Store listing** (ms-windows-store:// deep link)
- **SEO:** Target "windows handheld overlay", "rog ally fps overlay", "steam deck overlay windows"
- **NO direct download, NO EXE installer, NO Stripe checkout**

### Game Bar Widget Store (Organic Funnel)
- **Auto-listed** from Microsoft Store submission
- **Free widget** = discovery tool
- **Cross-sell:** "Unlock full features" → ms-windows-store:// deep link (100% policy-compliant)
- **Branding:** "Powered by Handheld Ally" with logo

## Cross-Sell Flow

```
Google/SEO → handheldally.com → Store link
Xbox button → Game Bar Widget Store → free widget → "Unlock" → Store purchase
Microsoft Store search → HHA Pulse listing → purchase
handheldally.com visitors → HHA Pulse promo section → Store link
```

All roads lead to Microsoft Store.

## Store Policies (Key Rules)

- Free widget CAN promote paid app IF paid app is also in Store AND link uses ms-windows-store:// URI
- "Powered by Handheld Ally" branding = allowed
- Link to handheldally.com for info/support = OK
- Widget → Store deep link for purchase = 100% policy-compliant

## Developer Account

- Individual account: FREE (no annual fee)
- Company account: 1,720 CZK (~75 EUR) one-time
- Recommendation: Individual account sufficient for v1

## Pricing Per Market

- Base: $1.99 USD (Store tier)
- Override CZK: 49 CZK
- Override EUR: 1.99 EUR (scale to 2.99 EUR after FPS/profiles features)
- Auto-converted for all other markets (60+ currencies)

## Kernel Driver Distribution

PawnIO driver CANNOT be in MSIX Store package. Solution:
1. **Auto-download on first run** — app detects missing driver, prompts user, downloads from secure CDN
2. **Without driver:** Battery, CPU/GPU %, RAM, FPS (PresentMon), VRAM all work
3. **With driver:** + CPU temp, fan speed, RAPL power draw
4. App works great without driver — driver is "optional enhancement" for advanced metrics
