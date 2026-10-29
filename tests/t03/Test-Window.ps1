$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowProbe {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
}
'@
$exe = Resolve-Path 'src/Dk.PhpCompatibilityChecker/bin/Release/net10.0-windows/Dk.PhpCompatibilityChecker.exe'
$process = Start-Process -FilePath $exe -PassThru
try {
    if (-not $process.WaitForInputIdle(15000)) { throw 'Window did not become idle' }
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $process.Refresh()
        if ($process.HasExited) { throw "Premature exit: $($process.ExitCode)" }
        if ($process.MainWindowHandle -ne 0) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    $handle = $process.MainWindowHandle
    $title = $process.MainWindowTitle
    $visible = [WindowProbe]::IsWindowVisible($handle)
    $rect = [WindowProbe+Rect]::new()
    if (-not [WindowProbe]::GetWindowRect($handle, [ref]$rect)) { throw 'No window rectangle' }
    if ($title -ne 'DK PHP Version Compatibility Checker' -or -not $visible -or -not $process.Responding) { throw 'Window not visible/responding or incorrect title' }
    if (($rect.Right - $rect.Left) -lt 640 -or ($rect.Bottom - $rect.Top) -lt 400) { throw 'Unexpected window dimensions' }
    if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(5000)) { throw 'Normal close failed' }
    if ($process.ExitCode -ne 0) { throw "Close exit: $($process.ExitCode)" }
    $summary = [ordered]@{
        os = [Environment]::OSVersion.VersionString
        sdk = (dotnet --version)
        sourceSha = $env:GITHUB_SHA
        runId = $env:GITHUB_RUN_ID
        executableSha256 = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
        title = $title
        visible = $visible
        responding = $true
        width = $rect.Right - $rect.Left
        height = $rect.Bottom - $rect.Top
        closeExitCode = $process.ExitCode
    }
    $summary | ConvertTo-Json | Set-Content artifacts/t03/summary.json -Encoding utf8
    Write-Output ('T03_SUMMARY_JSON=' + ($summary | ConvertTo-Json -Compress))
} finally {
    if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
    $process.Dispose()
}
