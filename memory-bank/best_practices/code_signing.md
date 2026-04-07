# Code Signing & Distribution Best Practices

> HHA Pulse v1 is Microsoft Store-only. Store MSIX signing is handled by Microsoft. EV/MSI guidance below is historical reference for non-Store experiments and does not apply to v1 public distribution.

## EV Code Signing Certificate

- **Get EV cert early** — standard certs need 2-8 weeks to build SmartScreen reputation
- EV certs provide IMMEDIATE SmartScreen trust
- EV is REQUIRED for WHQL driver submission
- EV certs use hardware security token — private key cannot be exported
- When renewing standard certs, reputation does NOT transfer — EV avoids this problem

## Signing Commands

```bash
# Sign with EV cert (SHA256 + RFC3161 timestamp)
signtool sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 /a HHAPulse.exe
```

- Always timestamp signatures (`/tr` with RFC 3161) — without timestamp, signature expires with cert
- Sign everything: EXE, DLL, MSI installer, driver .sys file

## WHQL Driver Signing

- Kernel-mode drivers on Windows 10+ REQUIRE either:
  1. **WHQL Certification** — full HLK test suite, Microsoft logo (highest trust)
  2. **Attestation Signing** — Microsoft countersigns without testing (lower trust)
- Submit via Windows Hardware Dev Center (partner.microsoft.com)
- PawnIO driver must be signed before it can load on Secure Boot systems

## Windows Defender False Positives

- Before release: submit signed binary to Microsoft Defender Security Intelligence portal
- Each new version may need resubmission
- Avoid: packers (UPX), obfuscation, self-modifying code, suspicious memory patterns
- If flagged: submit to Defender portal — expect 1-3 day turnaround
- Consider Microsoft's Trusted Signing service — maintains reputation across renewals

## MSI Installer

- Sign the MSI itself (not just contained binaries)
- Include proper version info in all binaries (FileVersion, ProductVersion)
- Pre-configure service recovery options during install
- Use WiX or Advanced Installer (well-known frameworks, less suspicious to AV)
- Test on clean VMs before every release
