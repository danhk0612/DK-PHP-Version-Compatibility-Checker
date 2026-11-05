<# Maintainer-only local package rehearsal. No release or redistribution clearance. #>
[CmdletBinding()]
param([string]$RepoRoot=(Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'Windows x64 PowerShell is required.' }
$repo=(Resolve-Path $RepoRoot).Path
if ((& dotnet --version).Trim() -ne '10.0.403') { throw 'Approved SDK 10.0.403 required.' }
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
& dotnet publish (Join-Path $repo 'src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj') -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' -o $stage '--verbosity' 'minimal' *> (Join-Path $evidence 'publish.log')
if ($LASTEXITCODE -ne 0) { throw 'Audit publish failed; inspect publish.log.' }
New-Item -ItemType Directory -Path (Join-Path $stage 'tools') | Out-Null
Copy-Item -LiteralPath $pack -Destination (Join-Path $stage 'tools/runtime') -Recurse
foreach ($file in @('LICENSE','THIRD-PARTY-NOTICES.md','LICENSING.md')) { Copy-Item -LiteralPath (Join-Path $repo $file) -Destination $stage }
Copy-Item -LiteralPath (Join-Path $repo 'tools/license-audit/license-election.json') -Destination (Join-Path $stage 'license-election.json')
New-Item -ItemType Directory -Path (Join-Path $stage 'tools/license-audit') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'tools/license-audit/license-election.json') -Destination (Join-Path $stage 'tools/license-audit/license-election.json')
Copy-Item -LiteralPath (Join-Path $repo 'licenses') -Destination (Join-Path $stage 'licenses') -Recurse
Copy-Item -LiteralPath (Join-Path $repo 'tests/t09/WINDOWS11_TEST.md') -Destination $stage
[IO.File]::WriteAllText((Join-Path $stage 'AUDIT-ONLY.txt'), "Local self-test candidate. PHP/Zend successor-license election is recorded in license-election.json. Keep the paired source archive and paired-candidate.json with this ZIP. Public distribution is not approved; remaining correspondence/notice and durable publication checks are recorded in LICENSING.md.`r`n", [Text.UTF8Encoding]::new($false))
$zip=Join-Path $out 'DK-PHP-Compatibility-audit-only.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
& python (Join-Path $repo 'tests/t09/check_package.py') --stage $stage --zip $zip --output $evidence
if ($LASTEXITCODE -ne 0) { throw 'Package byte verification failed.' }
# Extract to an independent Unicode path and exercise the actual self-contained apphost.
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
$report=[ordered]@{ scopePassed=$true; platform=[Environment]::OSVersion.VersionString; sdk='10.0.403'; selfContained=$true; localUserTestingPrepared=$true; licenseElectionMade=$true; windowShown=$shown; normalExit=$true; archive=(Split-Path $zip -Leaf); archiveSha256=(Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant(); redistributionCleared=$false; releaseGatePassed=$false; limits=@('Windows Server runner is not clean Windows 11','No CRT-missing machine tested','No scanner GUI interaction in this packaging test','T08/T10 and durable matching-source delivery remain gates') }
[IO.File]::WriteAllText((Join-Path $evidence 'summary.json'), (($report | ConvertTo-Json -Depth 8)+"`n"),[Text.UTF8Encoding]::new($false))
Write-Output ($report | ConvertTo-Json -Depth 8)
