# Maintainer preparation only. Never run in an inspected PHP project.
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$pack = Join-Path $repo 'tools/runtime'
$composerCommand = (Get-Command composer -ErrorAction Stop).Source
$pythonCommand = (Get-Command python -ErrorAction Stop).Source
$source = Get-Content (Join-Path $PSScriptRoot 'sources.json') -Raw | ConvertFrom-Json
if (Test-Path $pack) { throw 'tools/runtime already exists; preserve it and prepare in a fresh checkout.' }
New-Item $pack -ItemType Directory | Out-Null
$work = Join-Path ([IO.Path]::GetTempPath()) ('dk-pack-' + [guid]::NewGuid())
New-Item $work -ItemType Directory | Out-Null
$oldHome = $env:COMPOSER_HOME
$oldCache = $env:COMPOSER_CACHE_DIR
try {
    $zip = Join-Path $work 'php.zip'
    Invoke-WebRequest $source.php.url -OutFile $zip
    if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.php.sha256) { throw 'Official PHP archive hash mismatch' }
    Expand-Archive $zip (Join-Path $pack 'php')
    $engine = Join-Path $pack 'engine'
    New-Item $engine -ItemType Directory | Out-Null
    foreach ($file in @('composer.json', 'composer.lock', 'ruleset.xml')) { Copy-Item (Join-Path $PSScriptRoot $file) $engine }
    # Isolate Composer configuration and cache from the user's existing projects.
    $env:COMPOSER_HOME = Join-Path $work 'composer-home'
    $env:COMPOSER_CACHE_DIR = Join-Path $work 'composer-cache'
    & $composerCommand --working-dir=$engine validate --no-check-publish
    if ($LASTEXITCODE -ne 0) { throw 'Composer validation failed' }
    & $composerCommand --working-dir=$engine install --no-dev --prefer-dist --no-interaction --no-scripts
    if ($LASTEXITCODE -ne 0) { throw 'Locked engine installation failed' }
    & $composerCommand --working-dir=$engine audit --format=json | Set-Content (Join-Path $work 'audit.json') -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Composer audit failed; do not silently change approved versions' }
    $verification = Join-Path $pack 'verification'
    New-Item $verification -ItemType Directory | Out-Null
    Copy-Item (Join-Path $work 'audit.json') $verification
    & $composerCommand --version | Set-Content (Join-Path $verification 'composer-version.txt') -Encoding utf8
    # Product scans use only this verified runtime with -n (no ambient php.ini).
    & (Join-Path $pack 'php/php.exe') -n -v
    if ($LASTEXITCODE -ne 0) { throw 'Official PHP CLI could not start' }
    & $pythonCommand (Join-Path $PSScriptRoot 'verify_pack.py') --prepare $pack
    if ($LASTEXITCODE -ne 0) { throw 'Pack verification failed; generated folder is not approved for use' }
} finally {
    $env:COMPOSER_HOME = $oldHome
    $env:COMPOSER_CACHE_DIR = $oldCache
    Remove-Item $work -Recurse -Force
}
