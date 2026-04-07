# Daily Log - 2026-04-07

## Summary

Project kickoff. Complete design specification written based on deep research from 8 parallel research teammates covering VRR safety, anti-cheat compatibility, Windows telemetry APIs, Xbox Game Bar SDK, MangoHud presets, Reddit user preferences, overlay complaints, and ROG Ally/reviewer setups.

## Design Specification Completed

### Architecture Decided
- **Helper Service** (C# .NET 8, runs as SYSTEM) — PresentMon ETW, PawnIO, ADLX/IGCL, battery APIs
- **Overlay UI** (C# WinUI 3, runs as user) — top bar + side panel, hotkey handler
- **Game Bar Widget** (C# UWP XAML, free) — Game Bar Widget Store funnel
- **Named Pipes IPC** — binary/MessagePack protocol, explicit DACL security

### Overlay Design Finalized (Data-Driven)
4 presets based on real community data (MangoHud levels, Reddit surveys, reviewer setups):
- **Minimal:** FPS + rolling graph + Battery (%, time, W)
- **Standard:** + CPU/GPU % and temps (color-coded)
- **Tuner:** + AVG/1%/0.1% low, FrameGen detection, Bottleneck indicator, power W
- **Diagnostic:** Side panel with frametime table, graphs, latency, full hardware detail

Metric Picker: user toggles individual metrics ON/OFF on any preset. Custom mode: user builds own layout (top bar / side panel / both).

### 6 Killer Features Identified
1. FrameGen Detection (native vs generated FPS for ALL games)
2. Bottleneck Indicator (CPU-bound vs GPU-bound via PresentMon GPU Busy)
3. Game-Aware Battery Prediction (rolling avg, not Windows estimate)
4. Metric Picker (no all-or-nothing presets)
5. Zero Setup (one install vs RTSS + HWiNFO + Afterburner)
6. Real-Time Input Latency (click-to-photon via PresentMon 2.2)

### Key Research Findings
- AMD/Intel handhelds tolerate overlay windows for VRR (unlike NVIDIA G-Sync)
- WPF is BANNED — has known VRR bug (dotnet/wpf#2294)
- Game Bar widget is 100% anti-cheat safe (DWM composited)
- PresentMon ETW is anti-cheat safe (passive event tracing)
- MangoHud Level 2 (horizontal bar) is the most popular overlay config
- Battery % is "more important than FPS" for handheld users
- #1 complaint: all-or-nothing overlay levels (Metric Picker solves this)
- #1 missing feature: FrameGen detection (Steam added beta, we do it for all games)

### Security Requirements Added
- EV code signing, WHQL driver signing
- Named Pipes DACL (SYSTEM + local user + Package SID only)
- Zero telemetry, zero network access, all data local
- PawnIO (not WinRing0) for ring-0 access

## Files Created
- `docs/superpowers/specs/2026-04-07-hha-pulse-design.md` — full design spec (700+ lines)
- `CLAUDE.md` — project rules and guidelines
- `.gitignore` — build artifacts, IDE files, secrets
- `memory-bank/` — project knowledge base structure

## Current Truth
- Design spec is approved by user
- No code written yet — next step is implementation plan
- Target: AMD + Intel handhelds from v1
- Price: ~2 EUR desktop app, free Game Bar widget
