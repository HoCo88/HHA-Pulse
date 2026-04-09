param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("Overlay", "CaptureService", "Shared", "Solution")]
    [string]$Target = "Overlay",

    [switch]$StopRunning
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

function Stop-DotNetBuildServers {
    try {
        dotnet build-server shutdown
    }
    catch {
        Write-Warning "dotnet build-server shutdown failed: $($_.Exception.Message)"
    }
}

Stop-DotNetBuildServers

$running = Get-Process |
    Where-Object { $_.ProcessName -like "HHAPulse*" } |
    Select-Object ProcessName, Id, Path

if ($running) {
    if (-not $StopRunning) {
        $running | Format-Table -AutoSize | Out-String | Write-Host
        throw "HHAPulse processes are running. Close them or rerun with -StopRunning to avoid stale locked DLLs."
    }

    foreach ($process in $running) {
        Write-Host "Stopping $($process.ProcessName) ($($process.Id))..."
        Stop-Process -Id $process.Id -Force
    }
}

$outputRoots = @(
    "src\HHAPulse.Overlay\bin",
    "src\HHAPulse.Overlay\obj",
    "src\HHAPulse.CaptureService\bin",
    "src\HHAPulse.CaptureService\obj",
    "src\HHAPulse.Shared\bin",
    "src\HHAPulse.Shared\obj",
    "tests\HHAPulse.Overlay.Tests\bin",
    "tests\HHAPulse.Overlay.Tests\obj",
    "tests\HHAPulse.Shared.Tests\bin",
    "tests\HHAPulse.Shared.Tests\obj"
)

foreach ($relativePath in $outputRoots) {
    $fullPath = Join-Path $repoRoot $relativePath
    $resolvedParent = Resolve-Path (Split-Path $fullPath -Parent)
    if (-not $resolvedParent.Path.StartsWith($repoRoot.Path, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean outside repo root: $fullPath"
    }

    if (Test-Path $fullPath) {
        Write-Host "Removing $relativePath..."
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
}

$buildTarget = switch ($Target) {
    "Overlay" { "src\HHAPulse.Overlay\HHAPulse.Overlay.csproj" }
    "CaptureService" { "src\HHAPulse.CaptureService\HHAPulse.CaptureService.csproj" }
    "Shared" { "src\HHAPulse.Shared\HHAPulse.Shared.csproj" }
    "Solution" { "HHAPulse.sln" }
}

Write-Host "Restoring $buildTarget..."
dotnet restore $buildTarget

try {
    Write-Host "Building $buildTarget ($Configuration, x64)..."
    dotnet build $buildTarget -c $Configuration -p:Platform=x64 -p:UseSharedCompilation=false -p:MSBuildNodeReuse=false --disable-build-servers --no-restore
}
finally {
    Stop-DotNetBuildServers
}
