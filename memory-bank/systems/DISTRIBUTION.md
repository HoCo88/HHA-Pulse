# HHA Pulse — Distribution Strategy

## Sales Channel: Microsoft Store Only

No direct public downloads, no EXE/MSI sales, no Stripe checkout. handheldally.com is for promotion, support, SEO, privacy policy, and Microsoft Store deep links.

## Store Products

| Product | Price | Purpose |
| --- | --- | --- |
| HHA Pulse | $1.99 USD / 49 CZK / 1.99 EUR | Paid WinUI 3 overlay app |
| HHA Pulse Widget | Free | Xbox Game Bar funnel and companion |

Use a **Company Partner Center account** for OSVC/commercial publishing. Reserve both app names in Partner Center.

## Cross-Sell Flow

```
Google/SEO -> handheldally.com -> paid Store listing
Xbox Game Bar -> free widget -> Store upsell -> paid HHA Pulse
Microsoft Store search -> HHA Pulse paid app
```

Widget upsell links use:

```
ms-windows-store://pdp/?ProductId=<paid-app-product-id>
```

## External Dependencies

HHA Pulse must not bundle, download, or install external NT services or drivers.

| Dependency | Use | Distribution rule |
| --- | --- | --- |
| PresentMon Service | FPS, frametime, GPU Busy, latency, FrameGen | User-installed external dependency; disclose in Store certification notes |
| PawnIO | CPU temp, fan, RAPL power | Optional only; user-installed; disclose if detected/used |

Without PresentMon, the app still shows battery, CPU%, GPU%, RAM, VRAM, display, settings, profiles, and widget upsell. Without PawnIO, only CPU MSR temp/fan/RAPL are missing.

## Store Checklist

- Company account
- Paid app name: `HHA Pulse`
- Free widget name: `HHA Pulse Widget`
- Privacy policy URL on handheldally.com
- Product screenshots per listing
- IARC rating per listing
- `runFullTrust` justified for paid overlay app
- External dependency disclosure in certification notes
- Cross-package pipe IPC certification notes if enhanced widget mode ships

## Personal Development Build

An unpackaged EXE build may exist for local development and hardware testing:

```
dotnet publish src/HHAPulse.Overlay/HHAPulse.Overlay.csproj -c Release -r win-x64 --self-contained
```

This EXE is not a public distribution channel.
