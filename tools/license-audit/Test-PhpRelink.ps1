<# Isolated PHP build with original/marker libiconv. Requires PHP SDK task shell. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $PhpSource,
    [Parameter(Mandatory)] [string] $LibiconvSource,
    [Parameter(Mandatory)] [string] $DepsDirectory,
    [Parameter(Mandatory)] [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64) { throw 'Windows x64 is required.' }
$php = [IO.Path]::GetFullPath($PhpSource)
$lib = [IO.Path]::GetFullPath($LibiconvSource)
$deps = [IO.Path]::GetFullPath($DepsDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Output exists; use a fresh audit directory.' }
foreach ($source in @($php, $lib, $deps)) {
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Missing input: $source" }
    $sep = [IO.Path]::DirectorySeparatorChar
    if ($output.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or $output.StartsWith($source.TrimEnd($sep) + $sep, [StringComparison]::OrdinalIgnoreCase) -or $source.StartsWith($output.TrimEnd($sep) + $sep, [StringComparison]::OrdinalIgnoreCase)) { throw 'Audit output must be separate from every input.' }
}
New-Item -ItemType Directory -Path $output | Out-Null
$timer = [Diagnostics.Stopwatch]::StartNew()
& (Join-Path $PSScriptRoot 'Test-LibiconvRebuild.ps1') -SourcePath $lib -OutputDirectory (Join-Path $output 'libiconv')
$libReport = Get-Content -Raw -LiteralPath (Join-Path $output 'libiconv/libiconv-rebuild.json') | ConvertFrom-Json
$results = @()
foreach ($profile in @('original', 'marker')) {
    $root = Join-Path $output $profile
    $src = Join-Path $root 'php-src'
    $profileDeps = Join-Path $root 'deps'
    New-Item -ItemType Directory -Path $src,$profileDeps -Force | Out-Null
    Copy-Item -Path (Join-Path $php '*') -Destination $src -Recurse -Force
    Copy-Item -Path (Join-Path $deps '*') -Destination $profileDeps -Recurse -Force
    $libRow = @($libReport.libraries | Where-Object profile -eq $profile)
    if ($libRow.Count -ne 1) { throw "Missing libiconv build report for $profile" }
    $builtLib = Join-Path (Join-Path $output 'libiconv') $libRow[0].path
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $builtLib).Hash.ToLowerInvariant() -ne $libRow[0].sha256) { throw "Built libiconv hash changed: $profile" }
    $linkLib = Join-Path $profileDeps 'lib/libiconv_a.lib'
    if (-not (Test-Path -LiteralPath $linkLib -PathType Leaf)) { throw 'Pinned dependency package lacks lib/libiconv_a.lib' }
    Copy-Item -LiteralPath $builtLib -Destination $linkLib -Force
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $linkLib).Hash.ToLowerInvariant() -ne $libRow[0].sha256) { throw "Copied link input changed: $profile" }
    Push-Location $src
    try {
        & cmd.exe /d /c 'call buildconf.bat' *> (Join-Path $output "$profile-buildconf.log")
        if ($LASTEXITCODE -ne 0) { throw "buildconf failed: $profile" }
        $configureArgs = @('--disable-all','--disable-zts','--enable-cli','--with-iconv','--with-libxml','--with-simplexml','--enable-xmlwriter','--enable-tokenizer',("--with-php-build=$profileDeps"))
        & .\configure.bat @configureArgs *> (Join-Path $output "$profile-configure.log")
        if ($LASTEXITCODE -ne 0) { throw "configure failed: $profile" }
        & nmake.exe *> (Join-Path $output "$profile-nmake.log")
        if ($LASTEXITCODE -ne 0) { throw "nmake failed: $profile" }
    } finally { Pop-Location }
    $executables = @(Get-ChildItem -LiteralPath $src -Recurse -Filter php.exe -File)
    if ($executables.Count -ne 1) { throw "Expected one built php.exe: $profile, got $($executables.Count)" }
    $exe = $executables[0].FullName
    $dll = Join-Path $executables[0].DirectoryName 'php8.dll'
    if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "Missing php8.dll: $profile" }
    $observed = (& $exe -n -r 'echo PHP_VERSION, "|", ICONV_VERSION;').Trim()
    if ($LASTEXITCODE -ne 0) { throw "Built PHP probe failed: $profile" }
    $expected = if ($profile -eq 'original') { '8.4.27|1.19' } else { '8.4.27|1.20' }
    if ($observed -ne $expected) { throw "Unexpected PHP/iconv probe: $profile = $observed" }
    $results += [ordered]@{ profile=$profile; executable=$exe; executableSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $exe).Hash.ToLowerInvariant(); dll=$dll; dllSha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash.ToLowerInvariant(); libiconvSha256=$libRow[0].sha256; observed=$observed }
}
$timer.Stop()
$report = [ordered]@{ source='Pinned PHP 8.4.27 Windows source; independently built test binaries'; marker='ICONV_VERSION 1.20 is a test-only marker, not an actual library version'; windowsSdkVersion=$libReport.windowsSdkVersion; elapsedSeconds=$timer.Elapsed.TotalSeconds; results=$results; phpRelinkProbePassed=$true; phpcsRegressionVerified=$false; officialDistributionBinaryReproduced=$false; redistributionCleared=$false; scope='Isolated rebuilt test PHP; not the shipped PHP binary or legal clearance' }
[IO.File]::WriteAllText((Join-Path $output 'php-relink.json'), (($report | ConvertTo-Json -Depth 8) + "`n"), [Text.UTF8Encoding]::new($false))
