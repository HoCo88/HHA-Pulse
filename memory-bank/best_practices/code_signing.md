# Code Signing & Distribution Best Practices

Current operational build uses a normal desktop overlay plus an elevated local capture service. Microsoft Store-only guidance is historical and not the active blocker.

## Signing Guidance

- Sign all distributed EXE/DLL/MSI/service binaries.
- Timestamp every signature with RFC 3161.
- Avoid packers, obfuscators, self-modifying code, and suspicious memory patterns.
- Before public release, submit signed binaries to Microsoft Defender Security Intelligence if needed.

## Service Distribution Notes

- The capture service install requires one-time admin approval.
- Test install/uninstall on a clean VM before release.
- Configure service display name and description clearly.
- Keep the service local-only: no network listeners, no analytics, no telemetry.

## EV / Trusted Signing

- EV or Microsoft Trusted Signing helps SmartScreen reputation.
- EV is required for some kernel-driver workflows, but the current FPS capture service is user-mode ETW and does not install a driver.
- If future hardware telemetry requires a driver, driver signing becomes a separate project with its own threat model.
