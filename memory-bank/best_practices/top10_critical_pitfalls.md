# Critical Pitfalls

- Do not show user-facing diagnostic panels, dependency prompts, or install messages in the in-game HUD.
- Do not use DWM or D3DKMT as FPS sources.
- Do not add stub collectors to the runtime collector list.
- Do not expose temperature, fan, power, latency, or frame generation until real data exists.
- Do not test stale binaries: close the running overlay and build Release with build servers disabled when validating.
