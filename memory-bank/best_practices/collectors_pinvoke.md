# Metric Collectors P/Invoke Reference (April 2026)

All collectors work WITHOUT admin privileges. No Windows Service needed.

## Battery — CallNtPowerInformation

```csharp
[DllImport("powrprof.dll")]
static extern uint CallNtPowerInformation(int Level, IntPtr input, uint inputLen,
    out SYSTEM_BATTERY_STATE output, uint outputLen);

const int SystemBatteryState = 5;
```

Key fields:
- `Rate` (int, SIGNED!) — negative = discharging mW, positive = charging mW
- `RemainingCapacity` — mWh
- `MaxCapacity` — mWh (design capacity)
- `EstimatedTime` — seconds (0xFFFFFFFF = unknown)

**Gotcha:** Rate is SIGNED. Cast carefully. Divide by 1000 for watts.

For health/cycles: `IOCTL_BATTERY_QUERY_INFORMATION` via SetupAPI + DeviceIoControl.
`Health% = FullChargedCapacity / DesignedCapacity * 100`

## CPU Usage — GetSystemTimes

```csharp
[DllImport("kernel32.dll")]
static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
```

Formula: `CPU% = (kernel + user - idle) / (kernel + user) * 100`

**Gotcha:** Kernel time INCLUDES idle time. Must subtract idle. Min ~250ms between samples.

## RAM — GlobalMemoryStatusEx

```csharp
[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buffer);
```

**Gotcha:** MEMORYSTATUSEX must have `dwLength` set before calling. Use class (not struct) with constructor.

## GPU Usage — PDH API (NOT PerformanceCounter)

**Do NOT use .NET PerformanceCounter class** — memory leak (dotnet/runtime#31232), InvalidOperationException on Win11.

Use PDH API directly:
```csharp
[DllImport("pdh.dll", CharSet = CharSet.Unicode)]
static extern uint PdhOpenQuery(string src, IntPtr data, out IntPtr query);
[DllImport("pdh.dll", CharSet = CharSet.Unicode)]
static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr data, out IntPtr counter);
[DllImport("pdh.dll")]
static extern uint PdhCollectQueryData(IntPtr query);
[DllImport("pdh.dll")]
static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint fmt, out uint type, out PDH_FMT_COUNTERVALUE val);
```

Counter path: `\GPU Engine(*engtype_3D*)\Utilization Percentage`

**Gotcha:** Use `PdhAddEnglishCounter` (not `PdhAddCounter`) — works on non-English Windows. Must call `PdhCollectQueryData` twice (first = baseline).

## VRAM — Vortice.DXGI (NuGet, MIT)

```csharp
// NuGet: Vortice.DXGI
using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();
using var adapter = factory.EnumAdapters1(0);
using var adapter3 = adapter.QueryInterface<IDXGIAdapter3>();
var info = adapter3.QueryVideoMemoryInfo(0, MemorySegmentGroup.Local);
// info.CurrentUsage = used bytes, info.Budget = total budget bytes
```

**Gotcha:** Budget is OS-provided, may be less than physical VRAM. CurrentUsage is system-wide.

## Display — EnumDisplaySettings + VRR

Refresh rate:
```csharp
[DllImport("user32.dll", CharSet = CharSet.Ansi)]
static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);
// dm.dmDisplayFrequency = Hz (e.g., 60, 120, 144)
```

VRR detection via Vortice.DXGI:
```csharp
using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory5>();
bool vrrSupported = factory.PresentAllowTearing;
```

**Gotcha:** EnumDisplaySettings can return 0 or 1 for freq (= "hardware default"). VRR detection shows SUPPORT, not whether VRR is currently active.

## Required NuGet Packages
- `Vortice.DXGI` (MIT) — VRAM + VRR queries
- No other NuGet needed for collectors — all P/Invoke from system DLLs
