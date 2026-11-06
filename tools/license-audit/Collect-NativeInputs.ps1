param(
    [string] $OutputDirectory = 'artifacts/t08-native-inputs'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# These names were observed in the official PHP 8.4.27-1 x64 NTS build log.
# Current server bytes are candidate inputs; a name and an observed hash alone
# cannot establish identity with the binary inputs used by that historical run.
$files = @(
    'libargon2-20190702-vs17-x64.zip',
    'libiconv-1.19-vs17-x64.zip',
    'libxml2-2.11.9-8-vs17-x64.zip',
    'wineditline-2.208-vs17-x64.zip',
    'zlib-1.3.2-1-vs17-x64.zip'
)
$baseUrl = 'https://downloads.php.net/~windows/php-sdk/deps/vs17/x64/'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) {
    throw "Output already exists; preserve it and select a fresh directory: $output"
}
[IO.Directory]::CreateDirectory($output) | Out-Null

$records = @()
foreach ($name in $files) {
    $url = $baseUrl + $name
    $path = Join-Path $output $name
    Invoke-WebRequest -Uri $url -OutFile $path -MaximumRedirection 5
    $item = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    # Validate the retrieved file is a readable ZIP; never extract it here.
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entryCount = $zip.Entries.Count
        if ($entryCount -eq 0) { throw "Empty ZIP: $name" }
    }
    finally { $zip.Dispose() }
    $records += [ordered]@{
        name = $name
        url = $url
        bytes = $item.Length
        sha256 = $hash
        zipEntries = $entryCount
    }
}

$result = [ordered]@{
    observedUtc = [DateTime]::UtcNow.ToString('o')
    buildReference = 'https://github.com/php/php-windows-builder/actions/runs/40301342976'
    buildCommit = '68dc945d9509c41b72332c74c8b99731bb75bc25'
    source = 'Current bytes downloaded from the official PHP Windows dependency directory'
    scope = 'Candidate download inventory only; historical build byte identity, source correspondence, native rebuild, relink and redistribution clearance are not established.'
    historicalBuildByteIdentityVerified = $false
    nativeBuildOrRelinkVerified = $false
    redistributionCleared = $false
    files = $records
}
$json = $result | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText((Join-Path $output 'native-inputs.json'), $json + "`n", [Text.UTF8Encoding]::new($false))
Write-Output $json
