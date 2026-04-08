namespace HHAPulse.Shared.Protocol;

public static class PipeConstants
{
    public const string PipeName = @"\\.\pipe\LOCAL\HHAPulse";
    public const string PipeLocalName = @"LOCAL\HHAPulse";
    public const string CaptureOutputPipeName = @"\\.\pipe\LOCAL\HHAPulse.Capture.Out";
    public const string CaptureOutputPipeLocalName = @"LOCAL\HHAPulse.Capture.Out";
    public const string CaptureControlPipeName = @"\\.\pipe\LOCAL\HHAPulse.Capture.Control";
    public const string CaptureControlPipeLocalName = @"LOCAL\HHAPulse.Capture.Control";
    public const ushort ProtocolVersion = 1;
    public const int LengthPrefixBytes = 4;
    public const int MaxPayloadBytes = 1024 * 1024;
}
