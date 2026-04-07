# Daily Log - 2026-04-07

## Summary

Full design + research + scaffold review day. Design spec written (700+ lines), project scaffolded by GPT on feature/hha-pulse-v1, scaffold reviewed by 2 teammates (6.5/10 — good base, needs fixes), then 6 research teammates found proven solutions for all blockers.

## Key Decisions Made

- **One MSIX package** — widget + overlay v jednom, ne dva Store produkty
- **Free download + IAP $1.99/49 CZK** — widget free, overlay features za IAP
- **No Windows Service** — Store policy 10.1.5 zakazuje. Overlay app (FullTrustProcess) dělá vše.
- **Company Partner Center account** — povinné pro OSVČ (1,720 CZK jednorázově)
- **MessagePack 3.1.4 + netstandard2.0** — funguje, false alarm o nekompatibilitě
- **Named Pipes IPC** uvnitř jednoho MSIX — nepotřebuje World SID, stačí Package SID
- **PresentMon = external required dependency** pro FPS (user nainstaluje zvlášť)
- **PawnIO = optional** pro CPU temp/fan (žádné bundling/auto-install)
- **msbuild** pro build (ne dotnet build — UWP + wapproj)

## Architecture (Final, April 2026)

```
One MSIX Package (Microsoft Store):
├── HHA Pulse Overlay (FullTrustProcess, WinUI 3)
│   ├── Collects all metrics directly (no service)
│   ├── Renders overlay (top bar + side panel)
│   ├── Named Pipe server → feeds widget
│   └── Autostart via startup task
├── HHA Pulse Widget (UWP XAML, Game Bar SDK v7.3)
│   ├── Standalone: battery + basic metrics
│   ├── Enhanced: connects to overlay pipe
│   └── Upsell: IAP deep link
└── Free download, IAP unlocks overlay features
```

## Scaffold Review Results (GPT's feature/hha-pulse-v1)

**Score: 6.5/10** — architektonicky solidní, funkčně prázdné

### Co je hotové a kvalitní:
- TelemetrySnapshot + MessagePack modely (kompletní)
- PipeFrameCodec, MessageSerializer, PipeConstants (production-ready)
- CollectorOrchestrator s fault isolation
- BatteryPredictor, BottleneckDetector, FpsPercentileCalculator, RingBuffer (reálné algoritmy)
- SettingsService, ProfileService (JSON persistence)
- Testy (7 souborů, reálné asserty)
- CI pipeline (msbuild + setup-msbuild)
- CLAUDE.md, ARCHITECTURE.md, DISTRIBUTION.md (aktualizované)

### Co je stub:
- Všechny collectors (Battery, CPU, GPU, Display, RAM) — jen MetricFlags, nečtou data
- PresentMon, ADLX, IGCL, PawnIO — stuby
- Všechny UI controls — statický text
- Widget activation — chybí OnActivated + XboxGameBarWidgetActivatedEventArgs
- MainWindow — žádný transparent overlay interop
- App.xaml.cs — nespouští AppHost/collectors/pipe

### Blockery (identifikované a vyřešené):
- ~~B1 MessagePack~~ — FALSE ALARM, 3.x podporuje netstandard2.0
- B3 Widget activation — vyřešeno (XboxGameBarSamples kód)
- F2 Transparent overlay — vyřešeno (WinUIEx nebo DWM interop)
- F8 Collectors — vyřešeno (proven P/Invoke kódy)

## Research Findings Archive

### Transparent WinUI 3 Overlay (proven approach)
- WinUIEx NuGet: `TransparentTintBackdrop` zjednodušuje vše
- Alternativa: DwmExtendFrameIntoClientArea + DwmEnableBlurBehindWindow + compositor brush Alpha=0
- `SetWindowPos(HWND_TOPMOST)` — NE `IsAlwaysOnTop` (bug s click-through)
- Pro top bar: malý strip ~40px, nepotřebuje click-through
- `WS_EX_TOOLWINDOW` + `WS_EX_NOACTIVATE` — skryje z taskbaru, nekrade focus
- Reference: GuildOfCalamity/Transparency, castorix/WinUI3_SwapChainPanel_Layered

### Game Bar Widget Activation (from XboxGameBarSamples)
- OnActivated musí handlovat `XboxGameBarWidgetActivatedEventArgs`
- Protocol: `ms-gamebarwidget`
- Každá aktivace = nový `XboxGameBarWidget(widgetArgs, CoreWindow, Frame)`
- Manifest MUSÍ mít ProxyStub section s COM interface IDs
- Source: github.com/microsoft/XboxGameBarSamples

### Named Pipes IPC (same package = easy)
- Same MSIX = žádné Store policy exceptions
- DACL: Package SID stačí (same package)
- Pipe name: `\\.\pipe\LOCAL\HHAPulse`
- UWP: standardní NamedPipeClientStream funguje
- Source: learn.microsoft.com/gaming/game-bar/guide/communicating-apps

### Collectors (copy-paste ready P/Invoke)
- Battery: `CallNtPowerInformation(SystemBatteryState)` — Rate je SIGNED int
- Battery health: IOCTL_BATTERY_QUERY_INFORMATION via SetupAPI + DeviceIoControl
- CPU %: `GetSystemTimes` + delta (kernel INCLUDES idle)
- RAM: `GlobalMemoryStatusEx`
- GPU %: **PDH API přímo** (NE .NET PerformanceCounter — memory leak + exceptions)
- VRAM: **Vortice.DXGI** NuGet — `IDXGIAdapter3.QueryVideoMemoryInfo`
- Display: `EnumDisplaySettings` + `IDXGIFactory5.CheckFeatureSupport` pro VRR
- Žádný z collectors nepotřebuje admin

### Store & Distribution
- Developer registration: Company account 1,720 CZK (povinné pro OSVČ)
- Store podepisuje MSIX automaticky — žádný EV certifikát potřeba
- Pricing: $1.99 base, override CZK=49, EUR=1.99
- Certification: 1-3 business days
- runFullTrust capability = nutné zdůvodnit v certification notes

### Anti-Cheat Safety (verified April 2026)
- Game Bar widget: DWM composited = neviditelný pro anti-cheat
- PresentMon ETW: pasivní event tracing = safe
- External topmost window: low risk na AMD/Intel (tolerantní k overlay)
- RTSS/DLL injection: ZAKÁZÁNO — triggeruje anti-cheat

### VRR Safety (verified April 2026)
- AMD FreeSync + Intel VRR tolerují overlay okna (na rozdíl od NVIDIA G-Sync)
- WPF = ZAKÁZÁNO (dotnet/wpf#2294 VRR bug)
- WinUI 3 + malé okno = nejlepší šance na MPO hardware plane
- Update interval 1-2s = minimální VRR dopad

## Files Of Record
- Design spec: `/docs/superpowers/specs/2026-04-07-hha-pulse-design.md`
- Scaffold: branch `feature/hha-pulse-v1`, commit `2b5623e`
- GitHub: `git@github.com:HoCo88/HHA-Pulse.git`
- PR: `https://github.com/HoCo88/HHA-Pulse/pull/new/feature/hha-pulse-v1`
