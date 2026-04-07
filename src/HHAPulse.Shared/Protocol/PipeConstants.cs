namespace HHAPulse.Shared.Protocol;

public static class PipeConstants
{
    public const string PipeName = @"\\.\pipe\LOCAL\HHAPulse";
    public const string PipeLocalName = @"LOCAL\HHAPulse";
    public const ushort ProtocolVersion = 1;
    public const int LengthPrefixBytes = 4;
    public const int MaxPayloadBytes = 1024 * 1024;
}
