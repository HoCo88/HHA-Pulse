param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("Overlay", "Shared", "All")]
    [string]$Target = "Overlay",

    [switch]$NoRestore
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

$testTargets = switch ($Target) {
    "Overlay" { @("tests\HHAPulse.Overlay.Tests\HHAPulse.Overlay.Tests.csproj") }
    "Shared" { @("tests\HHAPulse.Shared.Tests\HHAPulse.Shared.Tests.csproj") }
    "All" {
        @(
            "tests\HHAPulse.Shared.Tests\HHAPulse.Shared.Tests.csproj",
            "tests\HHAPulse.Overlay.Tests\HHAPulse.Overlay.Tests.csproj"
        )
    }
}

Stop-DotNetBuildServers

try {
    foreach ($testTarget in $testTargets) {
        Write-Host "Testing $testTarget ($Configuration, x64)..."
        $testArgs = @(
            $testTarget,
            "-c", $Configuration,
            "-p:Platform=x64",
            "-p:UseSharedCompilation=false",
            "-p:MSBuildNodeReuse=false",
            "--disable-build-servers"
        )

        if ($NoRestore) {
            $testArgs += "--no-restore"
        }

        dotnet test @testArgs
    }
}
finally {
    Stop-DotNetBuildServers
}
