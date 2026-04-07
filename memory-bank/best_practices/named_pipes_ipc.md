# Named Pipes IPC Best Practices

## Security (NON-NEGOTIABLE)

- **ALWAYS create explicit PipeSecurity** — default is too permissive
- Grant: SYSTEM (FullControl) + Local User (ReadWrite) + UWP Package SID (ReadWrite)
- **Pipe Squatting Prevention:** Use `FILE_FLAG_FIRST_PIPE_INSTANCE` — fails if pipe already exists
- Set `nMaxInstances` to specific number, NOT `PIPE_UNLIMITED_INSTANCES`
- **Client impersonation risk:** Clients connect with `SECURITY_SQOS_PRESENT | SecurityIdentification`

## UWP Widget Connection (CRITICAL)

- UWP apps run in AppContainer sandbox — .NET NamedPipeClientStream may throw IOException
- Pipe server MUST grant access to widget's **Package SID**
- Find Package SID: `HKLM\Software\Microsoft\SecurityManager\CapAuthz\ApplicationEx\<package>\PackageSid`
- UWP can only access pipes via `\\.\pipe\LOCAL\` prefix
- Fallback: P/Invoke `CreateFileW` directly if .NET pipe classes fail

## Reconnection

- Widget may connect before service starts — retry with exponential backoff (100ms→200ms→400ms→5s max)
- Service restart breaks existing connections — detect broken pipe, dispose, reconnect
- On pipe disconnect: dispose old stream, create new `NamedPipeClientStream`

## Message Protocol

- **Length-prefix framing:** 4-byte LE length + serialized payload
- Or use `PipeTransmissionMode.Message` (handles framing automatically, Windows-only)
- Use **MessagePack** for serialization — binary, fast, low overhead
- NEVER use BinaryFormatter (security risk, deprecated)

## Performance

- Buffer size: 4096-8192 bytes for telemetry messages
- `PipeOptions.Asynchronous` for non-blocking I/O
- For high-frequency data: batch multiple readings per message
- Multi-client: `NamedPipeServerStream.MaxAllowedServerInstances` + per-client async handler
