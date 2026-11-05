<#
.SYNOPSIS
Build original and audit-marker libiconv static libraries in separate copies.
.DESCRIPTION
This is a maintainer audit, not a product pack builder. It verifies the pinned
Git source bytes, never edits the input checkout, and records build evidence.
It does not build PHP, demonstrate relinking, or determine license compliance.
Requires Windows x64, PowerShell 7, Git, and an initialized VS17 x64 toolchain.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $SourcePath,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [string] $MSBuildPath = 'MSBuild.exe',
    [string] $DumpbinPath = 'dumpbin.exe'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture -ne [System.Runtime.InteropServices.Architecture]::X64) { throw 'Windows x64 PowerShell is required.' }
$source = [IO.Path]::GetFullPath($SourcePath).TrimEnd([IO.Path]::DirectorySeparatorChar)
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
$comparison = [StringComparison]::OrdinalIgnoreCase
if ($output.Equals($source, $comparison) -or $output.StartsWith($source + [IO.Path]::DirectorySeparatorChar, $comparison) -or $source.StartsWith($output + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'Source and output must be separate directories.' }
if (Test-Path -LiteralPath $output) { throw 'Output already exists; use a new directory.' }
$revision = (& git -C $source rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $revision -ne '8f55a0aa6532f30f947261d90e72b200f350a5df') { throw 'Unexpected libiconv source commit.' }
$root = (& git -C $source rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or -not ([IO.Path]::GetFullPath($root).TrimEnd([IO.Path]::DirectorySeparatorChar)).Equals($source, $comparison)) { throw 'SourcePath must be the Git checkout root.' }
$index = @(& git -c core.quotepath=false -C $source ls-tree -r --full-tree HEAD)
if ($LASTEXITCODE -ne 0 -or $index.Count -ne 1148) { throw 'Unexpected source inventory.' }

function Get-GitBlobHash([byte[]] $Bytes) {
    $header = [Text.Encoding]::ASCII.GetBytes("blob $($Bytes.Length)`0")
    $stream = [IO.MemoryStream]::new()
    try {
        $stream.Write($header, 0, $header.Length)
        $stream.Write($Bytes, 0, $Bytes.Length)
        return [Convert]::ToHexString([Security.Cryptography.SHA1]::HashData($stream.ToArray())).ToLowerInvariant()
    } finally { $stream.Dispose() }
}
function Get-TrackedSnapshot {
    $rows = [Collections.Generic.List[object]]::new()
    foreach ($line in $index) {
        if ($line -notmatch '^(100644|100755) blob ([0-9a-f]{40})\t(.+)$') { throw "Unexpected source tree entry: $line" }
        $blob = $Matches[2]; $relative = $Matches[3]
        $file = [IO.Path]::GetFullPath((Join-Path $source $relative))
        if (-not $file.StartsWith($source + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw "Source path escapes checkout: $relative" }
        $item = Get-Item -LiteralPath $file -Force
        if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Unsupported source entry: $relative" }
        $bytes = [IO.File]::ReadAllBytes($file)
        if ((Get-GitBlobHash $bytes) -ne $blob) { throw "Source bytes differ from pinned Git blob: $relative" }
        $rows.Add([pscustomobject]@{ path = $relative; gitBlob = $blob; sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant(); bytes = $bytes.Length })
    }
    return $rows.ToArray()
}

$before = @(Get-TrackedSnapshot)
New-Item -ItemType Directory -Path $output | Out-Null
try {
    foreach ($name in @('original', 'marker')) {
        $destination = Join-Path $output $name
        foreach ($row in $before) {
            $file = Join-Path $destination $row.path
            New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $source $row.path) -Destination $file
            if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant() -ne $row.sha256) { throw "Copied source differs: $name/$($row.path)" }
        }
    }
    $marker = Join-Path $output 'marker/source/lib/iconv.c'
    $bytes = [IO.File]::ReadAllBytes($marker)
    $old = [Text.Encoding]::ASCII.GetBytes('int _libiconv_version = _LIBICONV_VERSION;')
    $new = [Text.Encoding]::ASCII.GetBytes('int _libiconv_version = _LIBICONV_VERSION + 1;')
    $content = [Text.Encoding]::Latin1.GetString($bytes)
    $oldText = [Text.Encoding]::ASCII.GetString($old)
    if (([regex]::Matches($content, [regex]::Escape($oldText))).Count -ne 1) { throw 'Expected exactly one version marker location.' }
    $content = $content.Replace($oldText, [Text.Encoding]::ASCII.GetString($new))
    [IO.File]::WriteAllBytes($marker, [Text.Encoding]::Latin1.GetBytes($content))
    $libraries = [Collections.Generic.List[object]]::new()
    foreach ($name in @('original', 'marker')) {
        $base = Join-Path $output $name
        $project = Join-Path $base 'MSVC17/libiconv_static/libiconv_static.vcxproj'
        $log = Join-Path $output "$name-msbuild.log"
        & $MSBuildPath $project '/nologo' '/t:Rebuild' '/p:Configuration=Release' '/p:Platform=x64' *> $log
        if ($LASTEXITCODE -ne 0) { throw "MSBuild failed for $name; inspect $log" }
        $library = Join-Path $base 'MSVC17/x64/lib/libiconv_a.lib'
        if (-not (Test-Path -LiteralPath $library -PathType Leaf)) { throw "Missing built library: $library" }
        $symbols = Join-Path $output "$name-symbols.txt"
        & $DumpbinPath '/symbols' $library *> $symbols
        if ($LASTEXITCODE -ne 0) { throw "dumpbin failed for $name; inspect $symbols" }
        $symbolMatches = @(Select-String -LiteralPath $symbols -Pattern '_libiconv_version')
        if ($symbolMatches.Count -eq 0) { throw "Version symbol absent from $library" }
        $libraries.Add([pscustomobject]@{ profile = $name; path = [IO.Path]::GetRelativePath($output, $library).Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash.ToLowerInvariant(); bytes = (Get-Item -LiteralPath $library).Length; symbols = @($symbolMatches | ForEach-Object { $_.Line }); msbuildLog = "$name-msbuild.log"; symbolLog = "$name-symbols.txt" })
    }
    if ($libraries[0].sha256 -eq $libraries[1].sha256) { throw 'Original and marker libraries have identical hashes.' }
    $report = [ordered]@{ sourceCommit = $revision; trackedFiles = $before.Count; sourceGitBlobBytesVerified = $true; sourcePath = $source; marker = 'Test-only _LIBICONV_VERSION + 1; not a real version change'; libraries = $libraries.ToArray(); nativePhpRelinkVerified = $false; redistributionCleared = $false; scope = 'Isolated original/marker libiconv static library builds only' }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'libiconv-rebuild.json') -Encoding utf8
} finally {
    $after = @(Get-TrackedSnapshot)
    if (($before | ConvertTo-Json -Compress -Depth 4) -cne ($after | ConvertTo-Json -Compress -Depth 4)) { throw 'Input tracked source changed during audit.' }
}
