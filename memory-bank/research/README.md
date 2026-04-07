# Research Archive

Research conducted 2026-04-07 informed the v1 architecture, but the current implementation direction is the v4 Store-safe plan:

- Paid **HHA Pulse** Store app: WinUI 3 FullTrustProcess, no custom Windows Service.
- Free **HHA Pulse Widget** Store app: UWP/Game Bar funnel with optional enhanced pipe mode.
- IPC path: `\\.\pipe\LOCAL\HHAPulse`.
- PresentMon Service: external user-installed dependency for advanced FPS metrics.
- PawnIO: optional user-installed dependency only.
- No public direct-download EXE/MSI distribution.

Historical research areas:

- Anti-cheat safety: no injection, no game memory reads, no DirectX/Vulkan hooks.
- VRR safety: WinUI 3 preferred, WPF banned.
- Windows telemetry APIs: battery, CPU%, RAM, VRAM, display, ADLX, IGCL.
- Xbox Game Bar SDK: viable companion and conversion funnel.
- User preference research: metric picker, battery focus, MangoHud-style top bar, FrameGen/bottleneck differentiation.

Key sources:
- PresentMon GitHub: https://github.com/GameTechDev/PresentMon
- MangoHud GitHub: https://github.com/flightlessmango/MangoHud
- GoTweaks GitHub: https://github.com/corando98/GoTweaks
- Xbox Game Bar SDK: https://learn.microsoft.com/en-us/gaming/game-bar/
- ADLX SDK: https://gpuopen.com/adlx/
- IGCL SDK: https://intel.github.io/drivers.gpu.control-library
