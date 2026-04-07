namespace HHAPulse.Shared.Protocol;

public enum IpcMessageType : ushort
{
    Unknown = 0,
    TelemetrySnapshot = 1,
    Heartbeat = 2
}
