# Research Archive

Research conducted 2026-04-07 by parallel research teams. Full findings are in the design spec and summarized in the daily log. Key sources preserved here for reference.

## Research Areas Covered

### Anti-Cheat Safety
- EAC, BattlEye, Vanguard, GameGuard detection methods
- Game Bar widget = invisible to anti-cheat (DWM composited)
- PresentMon ETW = safe (passive event tracing)
- External topmost window = low risk on AMD/Intel, breaks G-Sync on NVIDIA
- **Result:** Architecture is anti-cheat safe by design

### VRR Safety
- Any overlay can break VRR via DWM composition
- AMD FreeSync and Intel VRR tolerate overlay windows (unlike NVIDIA G-Sync)
- WPF has fatal VRR bug — BANNED
- Small overlay maximizes MPO (hardware plane) chance
- **Result:** WinUI 3 + small window + AMD/Intel = acceptable VRR behavior

### Windows Telemetry APIs
- Battery: GetSystemPowerStatus, CallNtPowerInformation (no admin)
- CPU temp/power: MSR via PawnIO (admin + driver)
- GPU: ADLX (AMD), IGCL (Intel) — no admin for monitoring
- FPS: PresentMon Service API (MIT, ETW-based)
- Fan: PawnIO + Super I/O chip
- **Result:** Helper Service as SYSTEM collects privileged data, passes via Named Pipe

### Xbox Game Bar SDK
- SDK v7.3 (Feb 2026), actively maintained
- Compact Mode for handhelds, pinning, transparency, click-through
- Named Pipes IPC documented and recommended
- All OEMs (ASUS, MSI, Lenovo) now build Game Bar widgets
- GoTweaks proves hardware access from Game Bar widget is possible
- **Result:** Game Bar widget is viable as free companion / funnel

### User Preferences (Community Research)
- MangoHud Level 2 (horizontal bar) most popular
- Top metrics: FPS, Battery %, GPU %, CPU %, temps, watts
- #1 complaint: all-or-nothing overlay levels
- #1 missing feature: FrameGen detection
- Battery metrics are #1 handheld differentiator
- Frametime graph preferred over FPS graph by experts
- **Result:** Metric Picker + preset-based design with battery focus

## Key Sources
- PresentMon GitHub: https://github.com/GameTechDev/PresentMon
- MangoHud GitHub: https://github.com/flightlessmango/MangoHud
- GoTweaks GitHub: https://github.com/corando98/GoTweaks
- Xbox Game Bar SDK: https://learn.microsoft.com/en-us/gaming/game-bar/
- ADLX SDK: https://gpuopen.com/adlx/
- IGCL SDK: https://intel.github.io/drivers.gpu.control-library
- PawnIO: replacement for WinRing0 (signed, sandboxed)
