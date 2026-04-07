# HHA Pulse — Distribution Strategy

## Three Sales Channels

### 1. Microsoft Store (MSIX)
- **Widget:** Free — auto-appears in Game Bar Widget Store
- **Desktop App:** ~2 EUR / 49 CZK / $1.99 USD
- **Revenue:** Microsoft takes 15% (~$0.30 per sale)
- **Benefits:** Discovery, auto-update, user trust, reviews
- **One MSIX package:** Widget + Desktop Service bundled together

### 2. handheldally.com (Direct Download)
- **Desktop App:** EXE installer, same price or direct purchase via Stripe
- **Revenue:** 0% platform cut (only payment processor fee ~2.9%)
- **Benefits:** Full control, no Store restrictions, driver included
- **SEO:** Target "windows handheld overlay", "rog ally fps overlay", "steam deck overlay windows"

### 3. Game Bar Widget Store (Organic Funnel)
- **Auto-listed** from Microsoft Store submission
- **Free widget** = discovery tool
- **Cross-sell:** "Unlock full features" → ms-windows-store:// deep link (100% policy-compliant)
- **Branding:** "Powered by Handheld Ally" with logo

## Cross-Sell Flow

```
Google/SEO → handheldally.com → Store link OR direct download
Xbox button → Game Bar Widget Store → free widget → "Unlock" → Store purchase
Microsoft Store search → HHA Pulse listing → purchase
handheldally.com visitors → HHA Pulse promo section → cross-sell
```

## Store Policies (Key Rules)

- Free widget CAN promote paid app IF paid app is also in Store AND link uses ms-windows-store:// URI
- "Powered by Handheld Ally" branding = allowed
- Link to handheldally.com for info/support = OK
- Link to handheldally.com for purchase = RISKY (avoid)
- Non-game apps CAN use own commerce (Stripe) = Microsoft takes 0%

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

PawnIO driver CANNOT be in MSIX Store package. Solutions:
1. **Auto-download on first run** — app detects missing driver, downloads from handheldally.com
2. **Included in EXE installer** (handheldally.com direct download)
3. **Without driver:** Battery, CPU/GPU %, RAM, FPS, VRAM all work. Missing: CPU temp, fan speed, RAPL power.
