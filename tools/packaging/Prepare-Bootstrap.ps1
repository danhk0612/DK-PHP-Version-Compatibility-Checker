# Preparation-only PHP and Composer. Nothing here is copied into the product pack.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'Windows x64 PowerShell 7 required.' }
$source=Get-Content (Join-Path $PSScriptRoot '../engine-pack/sources.json') -Raw | ConvertFrom-Json
$composer=Get-Content (Join-Path $PSScriptRoot 'bootstrap-sources.json') -Raw | ConvertFrom-Json
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Bootstrap output must be a fresh directory.' }
$root=(New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$zip=Join-Path $root 'php.zip'
Invoke-WebRequest $source.php.url -OutFile $zip
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $source.php.sha256) { throw 'Preparation PHP archive hash mismatch.' }
Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $root 'php')
Remove-Item -LiteralPath $zip
New-Item (Join-Path $root 'empty-ini') -ItemType Directory | Out-Null
$ini=@'
extension_dir="ext"
extension=openssl
extension=curl
extension=zip
'@
# Use an absolute extension directory; working directory may be anywhere.
$ini=$ini.Replace('extension_dir="ext"', 'extension_dir="' + (Join-Path $root 'php/ext').Replace('\','/') + '"')
[IO.File]::WriteAllText((Join-Path $root 'php/php.ini'),$ini,[Text.UTF8Encoding]::new($false))
$phar=Join-Path $root 'composer.phar'
Invoke-WebRequest $composer.url -OutFile $phar
if ((Get-FileHash -LiteralPath $phar -Algorithm SHA256).Hash.ToLowerInvariant() -ne $composer.sha256) { throw 'Preparation Composer hash mismatch.' }
$wrapper=@'
& "$PSScriptRoot/php/php.exe" -c "$PSScriptRoot/php/php.ini" "$PSScriptRoot/composer.phar" @args
exit $LASTEXITCODE
'@
[IO.File]::WriteAllText((Join-Path $root 'composer.ps1'),$wrapper,[Text.UTF8Encoding]::new($false))
