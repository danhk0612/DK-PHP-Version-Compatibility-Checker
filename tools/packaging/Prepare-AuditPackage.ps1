<# Maintainer-only local package rehearsal. No release or redistribution clearance. #>
[CmdletBinding()]
param(
    [string]$RepoRoot=(Join-Path $PSScriptRoot '../..'),
    [ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+$')] [string]$ReleaseVersion
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'Windows x64 PowerShell is required.' }
$repo=(Resolve-Path $RepoRoot).Path
if ((& dotnet --version).Trim() -ne '10.0.403') { throw 'Approved SDK 10.0.403 required.' }
if (-not (& dotnet --list-runtimes | Where-Object { $_ -match '^Microsoft\.WindowsDesktop\.App 10\.0\.\d+ \[' })) {
    throw 'Install .NET Desktop Runtime 10 x64 from https://dotnet.microsoft.com/en-us/download/dotnet/10.0.'
}
$election=Get-Content -Raw -Encoding utf8 (Join-Path $repo 'tools/license-audit/license-election.json') | ConvertFrom-Json
if (-not $election.electionMade -or $election.phpVersion -ne '4' -or $election.zendVersion -ne '3.0') { throw 'Approved PHP/Zend license election is required.' }
foreach ($row in @($election.selectedLicenseFiles) + @($election.provenanceFiles)) {
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $repo $row.path)).Hash.ToLowerInvariant() -ne $row.sha256) { throw 'Selected license/provenance bytes changed.' }
}
$out=Join-Path $repo 'artifacts/t09'
if (Test-Path -LiteralPath $out) { throw 'Preserve existing artifacts/t09; use a fresh checkout.' }
$pack=Join-Path $repo 'tools/runtime'
& python (Join-Path $repo 'tools/engine-pack/verify_pack.py') $pack
if ($LASTEXITCODE -ne 0) { throw 'Input pack validation failed.' }
New-Item -ItemType Directory -Path $out | Out-Null
$stage=Join-Path $out 'stage'
$evidence=Join-Path $out 'evidence'
New-Item -ItemType Directory -Path $evidence | Out-Null
$versionArgs = @()
if ($ReleaseVersion) { $versionArgs = @('-p:Version=' + $ReleaseVersion.Substring(1)) }
& dotnet publish (Join-Path $repo 'src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj') -c Release -r win-x64 --self-contained false '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' @versionArgs -o $stage '--verbosity' 'minimal' *> (Join-Path $evidence 'publish.log')
if ($LASTEXITCODE -ne 0) { throw 'Audit publish failed; inspect publish.log.' }
New-Item -ItemType Directory -Path (Join-Path $stage 'tools') | Out-Null
Copy-Item -LiteralPath $pack -Destination (Join-Path $stage 'tools/runtime') -Recurse
foreach ($file in @('LICENSE','THIRD-PARTY-NOTICES.md','LICENSING.md')) { Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $stage }
Copy-Item -LiteralPath (Join-Path $repo 'tools/license-audit/license-election.json') -Destination (Join-Path $stage 'license-election.json')
New-Item -ItemType Directory -Path (Join-Path $stage 'tools/license-audit') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'tools/license-audit/license-election.json') -Destination (Join-Path $stage 'tools/license-audit/license-election.json')
$licenseStage = Join-Path $stage 'licenses'
New-Item -ItemType Directory -Path $licenseStage | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo 'licenses') | Where-Object Name -ne 'dotnet' |
    Copy-Item -Destination $licenseStage -Recurse
$dotnetLicenseStage = Join-Path $licenseStage 'dotnet'
New-Item -ItemType Directory -Path $dotnetLicenseStage | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'licenses/dotnet/Microsoft.NETCore.App.Host.win-x64') -Destination $dotnetLicenseStage -Recurse
if ($ReleaseVersion) {
    $archiveName = "DK-PHP-Compatibility-$ReleaseVersion-win-x64.zip"
    $readme = @"
DK-PHP-Version-Compatibility-Checker $ReleaseVersion

1. Install .NET Desktop Runtime 10 x64 from https://dotnet.microsoft.com/en-us/download/dotnet/10.0.
2. Extract the ZIP and run Dk.PhpCompatibilityChecker.exe.
3. Select the PHP source folder and target version, then run the check.

The bundled engine supports offline checks after setup. PHP may require the
x64 Visual C++ Redistributable from https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist.

Corresponding source, notices, and SHA-256:
https://github.com/danhk0612/DK-PHP-Version-Compatibility-Checker/releases/tag/$ReleaseVersion
"@
    [IO.File]::WriteAllText((Join-Path $stage 'READ-ME.txt'), $readme, [Text.UTF8Encoding]::new($false))
} else {
    Copy-Item -LiteralPath (Join-Path $repo 'tests/t09/WINDOWS11_TEST.md') -Destination $stage
    [IO.File]::WriteAllText((Join-Path $stage 'AUDIT-ONLY.txt'), "Local self-test candidate. Install Microsoft .NET Desktop Runtime 10 x64 from https://dotnet.microsoft.com/en-us/download/dotnet/10.0 before running the app. PHP/Zend successor-license election is recorded in license-election.json. Keep the paired source archive and paired-candidate.json with this ZIP. Public distribution is not approved; remaining correspondence/notice and durable publication checks are recorded in LICENSING.md.`r`n", [Text.UTF8Encoding]::new($false))
    $archiveName = 'DK-PHP-Compatibility-audit-only.zip'
}
$zip = Join-Path $out $archiveName
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
& python (Join-Path $repo 'tests/t09/check_package.py') --stage $stage --zip $zip --output $evidence
if ($LASTEXITCODE -ne 0) { throw 'Package byte verification failed.' }
# Extract to an independent Unicode path and exercise the framework-dependent apphost.
$extract=Join-Path $out '한글 package & 실행'
[IO.Compression.ZipFile]::ExtractToDirectory($zip,$extract)
$process=[Diagnostics.Process]::new()
$process.StartInfo=[Diagnostics.ProcessStartInfo]::new((Join-Path $extract 'Dk.PhpCompatibilityChecker.exe'))
$process.StartInfo.WorkingDirectory=$extract
$process.StartInfo.UseShellExecute=$false
$shown=$false
try {
    if (-not $process.Start()) { throw 'Packaged app failed to start.' }
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    do {
        if ($process.HasExited) { throw "Packaged app exited before a window appeared: $($process.ExitCode)" }
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero -and $process.Responding) { $shown=$true; break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if (-not $shown) { throw 'Packaged app window was not observed.' }
    if (-not $process.CloseMainWindow() -or -not $process.WaitForExit(20000) -or $process.ExitCode -ne 0) { throw 'Packaged app did not close normally.' }
} finally {
    if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
    $process.Dispose()
}
& python (Join-Path $repo 'tests/t09/check_package.py') --stage $extract --zip $zip --output $evidence --post-launch
if ($LASTEXITCODE -ne 0) { throw 'Packaged files changed during app launch.' }
$report=[ordered]@{ scopePassed=$true; platform=[Environment]::OSVersion.VersionString; sdk='10.0.403'; selfContained=$false; desktopRuntimeRequired='Microsoft.WindowsDesktop.App 10.0 (x64)'; localUserTestingPrepared=$true; licenseElectionMade=$true; windowShown=$shown; normalExit=$true; archive=(Split-Path $zip -Leaf); archiveSha256=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant(); redistributionCleared=$false; releaseGatePassed=$false; limits=@('Windows Server runner is not clean Windows 11','No CRT-missing machine tested','No scanner GUI interaction in this packaging test','T08/T10 and durable matching-source delivery remain gates') }
[IO.File]::WriteAllText((Join-Path $evidence 'summary.json'), (($report | ConvertTo-Json -Depth 8)+"`n"),[Text.UTF8Encoding]::new($false))
Write-Output ($report | ConvertTo-Json -Depth 8)
