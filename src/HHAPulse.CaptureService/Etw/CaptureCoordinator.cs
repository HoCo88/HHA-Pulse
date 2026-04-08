using HHAPulse.Shared.Models;

namespace HHAPulse.CaptureService.Etw;

public sealed class CaptureCoordinator
{
    private readonly object syncRoot = new();
    private CaptureTarget target = new();

    public CaptureTarget Target
    {
        get
        {
            lock (syncRoot)
            {
                return new CaptureTarget
                {
                    ProcessId = target.ProcessId,
                    ProcessName = target.ProcessName,
                    TimestampUnixMilliseconds = target.TimestampUnixMilliseconds
                };
            }
        }
    }

    public void SetTarget(CaptureTarget nextTarget)
    {
        lock (syncRoot)
        {
            target = nextTarget;
        }
    }
}
