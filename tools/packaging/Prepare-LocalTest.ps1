<# Prepare a paired local self-test candidate. No public upload or release occurs. #>
[CmdletBinding()]
param([string]$RepoRoot=(Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'Windows x64 PowerShell 7 required.' }
$repo=(Resolve-Path $RepoRoot).Path
foreach ($command in @('dotnet','python','git')) { Get-Command $command -ErrorAction Stop | Out-Null }
if ((& dotnet --version).Trim() -ne '10.0.403') { throw 'Install .NET SDK 10.0.403 before preparation.' }
foreach ($path in @('artifacts/t08','artifacts/t08-core-source','artifacts/t08-build-source','artifacts/t08-source-sidecar','artifacts/t09')) {
    if (Test-Path -LiteralPath (Join-Path $repo $path)) { throw "Preserve previous output and use a fresh checkout: $path" }
}
Push-Location $repo
$oldPath=$env:PATH
$oldPhprc=$env:PHPRC
$oldScanDir=$env:PHP_INI_SCAN_DIR
$bootstrap=[IO.Path]::GetFullPath((Join-Path $repo ('artifacts/local-preparation-' + [guid]::NewGuid())))
try {
    # Preparation tools are private to this invocation, never installed globally.
    ./tools/packaging/Prepare-Bootstrap.ps1 -OutputDirectory $bootstrap
    $env:PATH="$bootstrap;$bootstrap/php;$oldPath"
    $env:PHPRC=Join-Path $bootstrap 'php/php.ini'
    $env:PHP_INI_SCAN_DIR=Join-Path $bootstrap 'empty-ini'
    foreach ($name in @('php','composer')) {
        $resolved=[IO.Path]::GetFullPath((Get-Command $name -ErrorAction Stop).Source)
        if (-not $resolved.StartsWith($bootstrap + [IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
            throw "Preparation command did not resolve to the private tool: $name"
        }
    }
    $runtime = Join-Path $repo 'tools/runtime'
    if ((Test-Path -LiteralPath $runtime -PathType Container) -and
        -not (Test-Path -LiteralPath (Join-Path $runtime 'manifest.json') -PathType Leaf)) {
        $backup = Join-Path $repo ('artifacts/incomplete-runtime-' + [guid]::NewGuid().ToString('N'))
        [IO.Directory]::Move($runtime, $backup)
        Write-Output "Preserved incomplete engine pack: $backup"
    }
    if (-not (Test-Path -LiteralPath $runtime)) { ./tools/engine-pack/Prepare-Pack.ps1 }
    python tools/engine-pack/verify_pack.py tools/runtime
    if ($LASTEXITCODE -ne 0) { throw 'Engine pack validation failed' }
    ./tools/license-audit/Collect-Windows.ps1
    $probe=& ./tools/runtime/php/php.exe -n -r "echo json_encode(['phpVersion'=>PHP_VERSION,'passwordAlgorithms'=>password_algos(),'extensions'=>get_loaded_extensions()], JSON_THROW_ON_ERROR);"
    if ($LASTEXITCODE -ne 0) { throw 'Safe runtime probe failed' }
    [IO.File]::WriteAllText("$repo/artifacts/t08/php-source-probe.json",$probe,[Text.UTF8Encoding]::new($false))
    python tools/license-audit/Collect-PhpCoreSource.py --sbom tools/runtime/php/extras/sbom/php.cdx.json --php-info artifacts/t08/php-info-no-ini.txt --probe artifacts/t08/php-source-probe.json --output artifacts/t08-core-source
    if ($LASTEXITCODE -ne 0) { throw 'Core source collection failed' }
    python tools/license-audit/Collect-BuildSources.py --output artifacts/t08-build-source
    if ($LASTEXITCODE -ne 0) { throw 'Build recipe source collection failed' }
    python tools/license-audit/Prepare-SourceSidecar.py --core-audit artifacts/t08-core-source --engine-audit artifacts/t08 --build-audit artifacts/t08-build-source --output artifacts/t08-source-sidecar
    if ($LASTEXITCODE -ne 0) { throw 'Source companion preparation failed' }
    ./tools/packaging/Prepare-AuditPackage.ps1 -RepoRoot $repo
    python tests/t09/check_pair.py --package artifacts/t09 --sidecar artifacts/t08-source-sidecar --output artifacts/t09/evidence
    if ($LASTEXITCODE -ne 0) { throw 'Candidate/source pairing failed' }
    Write-Output 'Local test files are ready. Keep all three files together:'
    Write-Output "$repo/artifacts/t09/DK-PHP-Compatibility-audit-only.zip"
    Write-Output "$repo/artifacts/t08-source-sidecar/DK-PHP-Compatibility-sources-and-notices.zip"
    Write-Output "$repo/artifacts/t09/evidence/paired-candidate.json"
    Write-Output 'Follow tests/t09/WINDOWS11_TEST.md. Public redistribution is not approved.'
} finally {
    $env:PATH=$oldPath
    $env:PHPRC=$oldPhprc
    $env:PHP_INI_SCAN_DIR=$oldScanDir
    if (Test-Path -LiteralPath $bootstrap) { Remove-Item -LiteralPath $bootstrap -Recurse -Force }
    Pop-Location
}
