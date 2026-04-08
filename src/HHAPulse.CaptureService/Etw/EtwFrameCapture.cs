using HHAPulse.Shared.Models;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;

namespace HHAPulse.CaptureService.Etw;

public sealed class EtwFrameCapture : IAsyncDisposable
{
    private const string SessionName = "HHAPulse_FrameCapture";
    private static readonly Guid DxgiProvider = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
    private static readonly Guid D3D9Provider = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");
    private static readonly Guid DxgKrnlProvider = new("802EC45A-1E99-4B83-9920-87C98277BA9D");
    private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromSeconds(60);

    private readonly CaptureCoordinator coordinator;
    private readonly ILogger<EtwFrameCapture> logger;
    private readonly object syncRoot = new();
    private readonly List<double> frameTimes = new();
    private readonly List<double> oneSecondFps = new();
    private TraceEventSession? session;
    private Task? sessionTask;
    private double lastPresentMs;
    private long lastPublishTicks;
    private uint activePid;
    private string activeProcessName = string.Empty;

    public EtwFrameCapture(CaptureCoordinator coordinator, ILogger<EtwFrameCapture> logger)
    {
        this.coordinator = coordinator;
        this.logger = logger;
    }

    public event Func<CaptureFrameMetrics, Task>? FrameMetricsAvailable;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        sessionTask = Task.Run(RunSession, CancellationToken.None);
        return Task.CompletedTask;
    }

    public void SetTarget(CaptureTarget target)
    {
        coordinator.SetTarget(target);
        lock (syncRoot)
        {
            if (activePid != target.ProcessId)
            {
                activePid = target.ProcessId;
                activeProcessName = target.ProcessName;
                frameTimes.Clear();
                oneSecondFps.Clear();
                lastPresentMs = 0;
                lastPublishTicks = 0;
            }
        }
    }

    private void RunSession()
    {
        try
        {
            TraceEventSession.GetActiveSessionNames()
                .Where(name => string.Equals(name, SessionName, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .ForEach(name => TraceEventSession.GetActiveSession(name)?.Stop());

            session = new TraceEventSession(SessionName) { StopOnDispose = true };
            session.EnableProvider(DxgiProvider, TraceEventLevel.Verbose, ulong.MaxValue);
            session.EnableProvider(D3D9Provider, TraceEventLevel.Verbose, ulong.MaxValue);
            session.EnableProvider(DxgKrnlProvider, TraceEventLevel.Verbose, ulong.MaxValue);
            session.Source.Dynamic.All += OnEtwEvent;
            logger.LogInformation("ETW frame capture session started.");
            session.Source.Process();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ETW frame capture session failed.");
        }
    }

    private void OnEtwEvent(TraceEvent data)
    {
        var target = coordinator.Target;
        if (target.ProcessId == 0 || data.ProcessID != target.ProcessId)
            return;

        if (!IsPresentEvent(data))
            return;

        var nowMs = data.TimeStampRelativeMSec;
        CaptureFrameMetrics? metrics = null;
        lock (syncRoot)
        {
            if (lastPresentMs > 0)
            {
                var frameTime = nowMs - lastPresentMs;
                if (frameTime is > 0.1 and < 1000)
                {
                    frameTimes.Add(frameTime);
                    TrimHistory(frameTimes, HistoryWindow.TotalMilliseconds);
                }
            }

            lastPresentMs = nowMs;
            var nowTicks = Environment.TickCount64;
            if (lastPublishTicks == 0)
            {
                lastPublishTicks = nowTicks;
                return;
            }

            if (TimeSpan.FromMilliseconds(nowTicks - lastPublishTicks) < PublishInterval || frameTimes.Count == 0)
                return;

            lastPublishTicks = nowTicks;
            var recent = frameTimes.ToArray();
            var avgFrameTime = recent.Average();
            var fps = 1000.0 / avgFrameTime;
            oneSecondFps.Add(fps);
            TrimCount(oneSecondFps, 60);

            metrics = new CaptureFrameMetrics
            {
                FramesPerSecond = fps,
                AverageFramesPerSecond = oneSecondFps.Count > 0 ? oneSecondFps.Average() : fps,
                OnePercentLowFramesPerSecond = PercentileLowFps(recent, 0.99),
                ZeroPointOnePercentLowFramesPerSecond = PercentileLowFps(recent, 0.999),
                FrameTimeMilliseconds = avgFrameTime,
                GameProcessId = target.ProcessId,
                GameProcessName = target.ProcessName,
                TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }

        if (metrics is not null)
        {
            _ = FrameMetricsAvailable?.Invoke(metrics);
        }
    }

    private static bool IsPresentEvent(TraceEvent data)
    {
        var name = data.EventName;
        return name.Contains("Present", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("History", StringComparison.OrdinalIgnoreCase);
    }

    private static double PercentileLowFps(double[] frameTimesMs, double percentile)
    {
        if (frameTimesMs.Length == 0)
            return 0;

        Array.Sort(frameTimesMs);
        var index = Math.Clamp((int)Math.Ceiling((frameTimesMs.Length - 1) * percentile), 0, frameTimesMs.Length - 1);
        return 1000.0 / frameTimesMs[index];
    }

    private static void TrimHistory(List<double> samples, double maxTotalMilliseconds)
    {
        double total = 0;
        for (var i = samples.Count - 1; i >= 0; i--)
        {
            total += samples[i];
            if (total > maxTotalMilliseconds)
            {
                samples.RemoveRange(0, i + 1);
                return;
            }
        }
    }

    private static void TrimCount<T>(List<T> samples, int maxCount)
    {
        if (samples.Count > maxCount)
        {
            samples.RemoveRange(0, samples.Count - maxCount);
        }
    }

    public async ValueTask DisposeAsync()
    {
        session?.Dispose();
        if (sessionTask is not null)
        {
            await Task.WhenAny(sessionTask, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        }
    }
}
