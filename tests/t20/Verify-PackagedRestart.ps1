<# T20: restart the actual unpublished, paired WPF application without publishing it. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$PackageRoot,
    [Parameter(Mandatory = $true)] [string]$Archive,
    [Parameter(Mandatory = $true)] [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'Windows x64 PowerShell 7 required.' }

$package = (Resolve-Path -LiteralPath $PackageRoot).Path
$zip = (Resolve-Path -LiteralPath $Archive).Path
$exe = Join-Path $package 'Dk.PhpCompatibilityChecker.exe'
$manifest = Get-Content -Raw -LiteralPath (Join-Path $package 'tools/runtime/manifest.json') | ConvertFrom-Json
$targets = @($manifest.targets | ForEach-Object { $_.version })
if (($targets -join ',') -ne '7.4,8.0,8.1,8.2,8.3,8.4,8.5') {
    throw 'The paired WPF candidate does not contain the seven verified PHP targets.'
}
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Preserve prior restart evidence; use a new output directory.' }
$before = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
$attempts = @()
for ($n = 1; $n -le 2; $n++) {
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = [Diagnostics.ProcessStartInfo]::new($exe)
    $process.StartInfo.WorkingDirectory = $package
    $process.StartInfo.UseShellExecute = $false
    try {
        if (-not $process.Start()) { throw "Packaged WPF process $n did not start." }
        $deadline = [DateTime]::UtcNow.AddSeconds(45)
        $shown = $false
        do {
            if ($process.HasExited) { throw "Packaged WPF process $n exited before displaying its window: $($process.ExitCode)" }
            $process.Refresh()
            if ($process.MainWindowHandle -ne [IntPtr]::Zero -and $process.Responding -and
                $process.MainWindowTitle -eq 'DK PHP Version Compatibility Checker') {
                $shown = $true
                break
            }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $deadline)
        if (-not $shown) { throw "Packaged WPF process $n did not show a responsive main window." }
        $pidObserved = $process.Id
        if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(20000) -or $process.ExitCode -ne 0) {
            throw "Packaged WPF process $n did not close normally."
        }
        $attempts += [ordered]@{ attempt = $n; processId = $pidObserved; windowShown = $true; normalExit = $true }
    } finally {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
}
$after = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash.ToLowerInvariant()
if ($after -ne $before) { throw 'The packaged application EXE changed across two process launches.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
python tests/t09/check_package.py --stage "$package" --zip "$zip" --output "$OutputDirectory" --post-launch
if ($LASTEXITCODE -ne 0) { throw 'Packaged bytes changed or the source-independent package verification failed.' }

$summary = [ordered]@{
    scopePassed = $true
    windowsServerCiOnly = $true
    packagedApplicationExeRestarted = $true
    appProcesses = $attempts
    bundledTargetsInVerifiedManifest = $targets
    mainWindowResponsiveOnBothLaunches = $true
    normalExitOnBothLaunches = $true
    exeSha256 = $before
    packageBytesUnchanged = $true
    managedEngineUpdateAfterAppRestartVerified = $false
    cleanWindows11Verified = $false
    publicReleaseVerified = $false
}
[IO.File]::WriteAllText(
    (Join-Path $OutputDirectory 'summary.json'),
    (($summary | ConvertTo-Json -Depth 8) + "`n"),
    [Text.UTF8Encoding]::new($false)
)
Write-Output ('T20_PACKAGED_RESTART_JSON=' + ($summary | ConvertTo-Json -Compress -Depth 8))
