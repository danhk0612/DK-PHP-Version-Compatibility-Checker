# Maintainer evidence collection only; never run in an inspected PHP project.
param([string]$RepoRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path $RepoRoot).Path
$out = Join-Path $repo 'artifacts/t08'
$pack = Join-Path $repo 'tools/runtime'
$project = Join-Path $repo 'src/Dk.PhpCompatibilityChecker/Dk.PhpCompatibilityChecker.csproj'
if (-not $IsWindows -or -not [Environment]::Is64BitProcess) { throw 'Actual Windows x64 PowerShell is required.' }
if ((& dotnet --version).Trim() -ne '10.0.403') { throw 'The approved SDK 10.0.403 is required.' }
if (-not (Test-Path (Join-Path $pack 'manifest.json'))) { throw 'Prepare the verified T04 runtime first.' }
if (Test-Path $out) { throw 'artifacts/t08 already exists; preserve it and collect in a fresh checkout.' }
New-Item $out -ItemType Directory | Out-Null
$utf8 = [Text.UTF8Encoding]::new($false)
function Save-Json($Value, [string]$Path) {
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 60), $utf8)
}
function Digest([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Relative([string]$Root, [string]$Path) { [IO.Path]::GetRelativePath($Root, $Path).Replace('\', '/') }
function File-Record([string]$Root, [IO.FileInfo]$File) {
    $version = if ($File.Extension -in @('.exe', '.dll')) { [Diagnostics.FileVersionInfo]::GetVersionInfo($File.FullName) } else { $null }
    [ordered]@{ path = (Relative $Root $File.FullName); bytes = $File.Length; sha256 = (Digest $File.FullName)
        fileVersion = $version.FileVersion; productVersion = $version.ProductVersion; companyName = $version.CompanyName
        productName = $version.ProductName; originalFilename = $version.OriginalFilename; copyright = $version.LegalCopyright }
}
function Copy-Evidence([string]$From, [string]$RelativePath) {
    $destination = Join-Path $out $RelativePath
    New-Item (Split-Path $destination) -ItemType Directory -Force | Out-Null
    Copy-Item -LiteralPath $From -Destination $destination
    [ordered]@{ path = $RelativePath.Replace('\', '/'); sha256 = (Digest $destination); bytes = (Get-Item $destination).Length }
}
& dotnet --info | Out-File (Join-Path $out 'dotnet-info.txt') -Encoding utf8
$publish = Join-Path $out 'publish'
$publishLog = Join-Path $out 'publish.log'
& dotnet publish $project -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=false' '-p:PublishTrimmed=false' -o $publish --verbosity normal '-clp:Summary' ('-bl:' + (Join-Path $out 'publish.binlog')) 2>&1 | Tee-Object -FilePath $publishLog
if ($LASTEXITCODE -ne 0) { throw 'Self-contained publish failed; inspect publish.log/binlog.' }
$publishText = Get-Content $publishLog -Raw
$warningSummary = [regex]::Matches($publishText, '(\d+) Warning\(s\)')
$errorSummary = [regex]::Matches($publishText, '(\d+) Error\(s\)')
$build = [ordered]@{ exitCode = 0; warnings = $(if ($warningSummary.Count) { [int]$warningSummary[-1].Groups[1].Value } else { $null })
    errors = $(if ($errorSummary.Count) { [int]$errorSummary[-1].Groups[1].Value } else { $null }); command = 'dotnet publish <project> -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/t08/publish --verbosity normal -clp:Summary' }
Save-Json $build (Join-Path $out 'publish-result.json')
$assetsPath = Join-Path (Split-Path $project) 'obj/project.assets.json'
$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
Copy-Evidence $assetsPath 'dependency-data/project.assets.json' | Out-Null
$depsPath = Join-Path $publish 'Dk.PhpCompatibilityChecker.deps.json'
$deps = Get-Content $depsPath -Raw | ConvertFrom-Json
Copy-Evidence $depsPath 'dependency-data/Dk.PhpCompatibilityChecker.deps.json' | Out-Null
Copy-Evidence (Join-Path $publish 'Dk.PhpCompatibilityChecker.runtimeconfig.json') 'dependency-data/Dk.PhpCompatibilityChecker.runtimeconfig.json' | Out-Null
Copy-Evidence (Join-Path $pack 'manifest.json') 'pack-manifest.json' | Out-Null
Copy-Evidence (Join-Path $pack 'engine/composer.lock') 'dependency-data/composer.lock' | Out-Null
Copy-Evidence (Join-Path $pack 'engine/vendor/composer/installed.json') 'dependency-data/composer-installed.json' | Out-Null
Copy-Evidence (Join-Path $pack 'verification/composer-version.txt') 'dependency-data/composer-version.txt' | Out-Null
$nugetRoots = @($assets.packageFolders.PSObject.Properties.Name)
$dotnetRoot = Split-Path (Get-Command dotnet).Source
$packageRequests = @()
foreach ($name in $deps.libraries.PSObject.Properties.Name) {
    if ($name -match '^(?:runtimepack\.)?(Microsoft\.(?:NETCore|WindowsDesktop)\.App\.Runtime\.win-x64)/([^/]+)$') {
        $packageRequests += [ordered]@{ id = $Matches[1]; version = $Matches[2] }
    }
}
if ($packageRequests.Count -ne 2) { throw 'Expected exactly the NETCore and WindowsDesktop win-x64 runtime packages in deps.json.' }
$coreVersion = ($packageRequests | Where-Object { $_.id -eq 'Microsoft.NETCore.App.Runtime.win-x64' }).version
$packageRequests += [ordered]@{ id = 'Microsoft.NETCore.App.Host.win-x64'; version = $coreVersion }
$payloadByHash = @{}
$packages = @()
foreach ($request in $packageRequests) {
    $id = $request.id; $version = $request.version; $lower = $id.ToLowerInvariant()
    $archivePath = $null
    foreach ($root in $nugetRoots) {
        $candidate = Join-Path $root "$lower/$version/$lower.$version.nupkg"
        if (Test-Path $candidate) { $archivePath = $candidate; break }
    }
    $url = "https://api.nuget.org/v3-flatcontainer/$lower/$version/$lower.$version.nupkg"
    $obtainedFrom = 'restored NuGet package cache'
    if ($null -eq $archivePath) {
        $archivePath = Join-Path $out "downloads/$lower.$version.nupkg"
        New-Item (Split-Path $archivePath) -ItemType Directory -Force | Out-Null
        Invoke-WebRequest $url -OutFile $archivePath
        $obtainedFrom = 'exact-version official NuGet flat-container download (cache did not retain nupkg)'
    }
    $archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
    $payload = @(); $legal = @(); $nuspec = $null
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $stream = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
            $payload += [ordered]@{ path = $entry.FullName; bytes = $entry.Length; sha256 = $hash }
            if (-not $payloadByHash.ContainsKey($hash)) { $payloadByHash[$hash] = @() }
            $payloadByHash[$hash] += "$id/$version/$($entry.FullName)"
            if ($entry.FullName -match '(?i)(^|/)(license[^/]*|licence[^/]*|third[-_. ]party[^/]*|notice[^/]*|copying[^/]*)$' -or $entry.FullName.EndsWith('.nuspec')) {
                $rel = "licenses/dotnet/$id/$version/$($entry.FullName)"
                $target = Join-Path $out $rel
                New-Item (Split-Path $target) -ItemType Directory -Force | Out-Null
                $inputStream = $entry.Open(); $outputStream = [IO.File]::Create($target)
                try { $inputStream.CopyTo($outputStream) } finally { $inputStream.Dispose(); $outputStream.Dispose() }
                $legal += [ordered]@{ path = $rel; sha256 = $hash; bytes = $entry.Length }
                if ($entry.FullName.EndsWith('.nuspec')) { $nuspec = [IO.File]::ReadAllText($target) }
            }
        }
    } finally { $archive.Dispose() }
    if (-not ($legal | Where-Object { $_.path -match '(?i)(^|/)license' })) { throw "No license text found in $id/$version" }
    if ($id -eq 'Microsoft.WindowsDesktop.App.Runtime.win-x64' -and -not ($legal | Where-Object { $_.path -match '(?i)third.*party' })) {
        # This runtime package does not carry its WPF/WinForms notice files.
        # Preserve them from the exact source commit declared by its nuspec,
        # and distinguish these supplements from original package payloads.
        [xml]$specification = $nuspec
        $sourceUrl = [string]$specification.package.metadata.repository.url
        $sourceCommit = [string]$specification.package.metadata.repository.commit
        if ($sourceUrl -ne 'https://github.com/dotnet/dotnet' -or $sourceCommit -notmatch '^[0-9a-f]{40}$') { throw 'Unrecognized WindowsDesktop source identity; review missing notices.' }
        foreach ($component in @('wpf', 'winforms')) {
            $noticeUrl = "https://raw.githubusercontent.com/dotnet/dotnet/$sourceCommit/src/$component/THIRD-PARTY-NOTICES.TXT"
            $noticeRelative = "licenses/dotnet/$id/$version/upstream-$component-THIRD-PARTY-NOTICES.TXT"
            $noticeTarget = Join-Path $out $noticeRelative
            Invoke-WebRequest $noticeUrl -OutFile $noticeTarget
            if ((Get-Item $noticeTarget).Length -lt 100) { throw 'Incomplete WindowsDesktop source notice.' }
            $legal += [ordered]@{ path = $noticeRelative; sha256 = (Digest $noticeTarget); bytes = (Get-Item $noticeTarget).Length
                originUrl = $noticeUrl; sourceCommit = $sourceCommit; origin = 'supplemental pinned source notice; absent from original nupkg' }
        }
    }
    if ($id -match '\.Runtime\.' -and -not ($legal | Where-Object { $_.path -match '(?i)third.*party' })) { throw "No third-party notices found in $id/$version" }
    Save-Json $payload (Join-Path $out "licenses/dotnet/$id/$version/payload-files.json")
    $packages += [ordered]@{ id = $id; version = $version; originUrl = $url; obtainedFrom = $obtainedFrom
        archiveSha256 = (Digest $archivePath); archiveBytes = (Get-Item $archivePath).Length; payloadFileCount = $payload.Count; legalFiles = $legal; nuspec = $nuspec }
}
Save-Json $packages (Join-Path $out 'dotnet-packages.json')
$publishFiles = @()
foreach ($file in Get-ChildItem $publish -File -Recurse -Force | Sort-Object FullName) {
    $row = File-Record $publish $file
    if ($payloadByHash.ContainsKey($row.sha256)) { $row['component'] = 'exact runtime/host NuGet payload'; $row['packagePayloads'] = @($payloadByHash[$row.sha256]) }
    elseif ($row.path -in @('Dk.PhpCompatibilityChecker.dll', 'Dk.PhpCompatibilityChecker.pdb')) { $row['component'] = 'own application output'; $row['packagePayloads'] = @() }
    elseif ($row.path -in @('Dk.PhpCompatibilityChecker.deps.json', 'Dk.PhpCompatibilityChecker.runtimeconfig.json')) { $row['component'] = 'SDK-generated application dependency metadata'; $row['packagePayloads'] = @() }
    elseif ($row.path -eq 'Dk.PhpCompatibilityChecker.exe') { $row['component'] = 'SDK-patched application host'; $row['packagePayloads'] = @("Microsoft.NETCore.App.Host.win-x64/$coreVersion/runtimes/win-x64/native/apphost.exe (template; patched bytes differ)") }
    else { $row['component'] = 'unmapped: manual license/source attribution required'; $row['packagePayloads'] = @() }
    $publishFiles += $row
}
Save-Json $publishFiles (Join-Path $out 'publish-files.json')
$lock = Get-Content (Join-Path $pack 'engine/composer.lock') -Raw | ConvertFrom-Json
$packFiles = @(); $copied = @()
foreach ($file in Get-ChildItem $pack -File -Recurse -Force | Sort-Object FullName) {
    $row = File-Record $pack $file
    $row['component'] = if ($row.path.StartsWith('php/')) { 'official PHP ZIP aggregate: component-specific attribution still required' } else { 'pack configuration/verification or Composer-generated files' }
    foreach ($dependency in $lock.packages) {
        if ($row.path.StartsWith('engine/vendor/' + $dependency.name + '/')) { $row.component = "$($dependency.name)@$($dependency.version)"; break }
    }
    $packFiles += $row
    if ($file.Name -match '(?i)(license|licence|copying|notice|copyright|readme)' -or $row.path -like 'php/extras/sbom/*') {
        $copied += Copy-Evidence $file.FullName ('licenses/pack/' + $row.path)
    }
}
Save-Json $packFiles (Join-Path $out 'pack-files.json')
Save-Json $copied (Join-Path $out 'copied-pack-documents.json')
$php = Join-Path $pack 'php/php.exe'
& $php -n -i | Out-File (Join-Path $out 'php-info-no-ini.txt') -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Pack PHP -n -i failed.' }
$deplister = Join-Path $pack 'php/deplister.exe'
$dependencyDiagnostics = @()
if (Test-Path $deplister) {
    foreach ($image in Get-ChildItem (Join-Path $pack 'php') -File -Recurse | Where-Object { $_.Extension -in @('.dll', '.exe') } | Sort-Object FullName) {
        $target = Relative (Join-Path $pack 'php') $image.FullName
        $outputFile = Join-Path $out ('php-imports-' + $target.Replace('/', '-') + '.txt')
        & $deplister $image.FullName 2>&1 | Out-File $outputFile -Encoding utf8
        $dependencyDiagnostics += [ordered]@{ target = $target; exitCode = $LASTEXITCODE; evidence = (Split-Path $outputFile -Leaf) }
    }
}
$start = [Diagnostics.ProcessStartInfo]::new($php)
$start.UseShellExecute = $false
$start.WorkingDirectory = $out
$start.ArgumentList.Add('-n'); $start.ArgumentList.Add('-r'); $start.ArgumentList.Add('sleep(15);')
$start.Environment['PHPRC'] = ''; $start.Environment['PHP_INI_SCAN_DIR'] = ''
$owned = [Diagnostics.Process]::Start($start)
$loadedModules = @()
try {
    Start-Sleep -Milliseconds 500
    if ($owned.HasExited) { throw 'Owned PHP module-inspection process exited unexpectedly.' }
    $owned.Refresh()
    foreach ($module in $owned.Modules) {
        $moduleFile = Get-Item -LiteralPath $module.FileName
        $row = File-Record (Split-Path $module.FileName) $moduleFile
        $row['modulePath'] = $module.FileName
        $row['inRedistributedPhpZip'] = $module.FileName.StartsWith((Join-Path $pack 'php') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
        $row['externalCrtCandidate'] = $module.ModuleName -match '(?i)^(vcruntime|msvcp|ucrtbase|api-ms-win-crt)'
        $loadedModules += $row
    }
} finally {
    if (-not $owned.HasExited) { $owned.Kill($true) }
    $owned.WaitForExit(); $owned.Dispose()
}
Save-Json $loadedModules (Join-Path $out 'php-loaded-modules.json')
# A full checkout includes tracked tests/build files removed from Composer dist
# archives by export-ignore. No .git database or untracked content is included.
$sourceHelper = @'
import hashlib, json, pathlib, subprocess, sys, zipfile
root, vendor, archive, inventory = map(pathlib.Path, sys.argv[1:])
revision = subprocess.check_output(['git','-C',str(root),'rev-parse','HEAD'],text=True).strip()
raw = subprocess.check_output(['git','-C',str(root),'ls-files','--stage','-z'])
rows = []
for entry in raw.split(b'\0'):
    if not entry: continue
    header, name = entry.split(b'\t',1)
    mode, blob, stage = header.decode().split()
    if mode not in ('100644','100755') or stage != '0': raise RuntimeError('Unsupported tracked source mode: '+header.decode())
    name = name.decode('utf-8')
    data = (root / name).read_bytes()
    # Verify every local checkout byte against the actual blob identity. This
    # detects newline conversion instead of treating converted text as upstream.
    actual_blob = hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
    if actual_blob != blob: raise RuntimeError('Checkout bytes differ from Git blob: '+name)
    rows.append({'path':name,'gitMode':mode,'gitBlob':blob,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
by_name = {r['path']:r for r in rows}
comparisons = []
for f in sorted(vendor.rglob('*')):
    if not f.is_file(): continue
    relative = f.relative_to(vendor).as_posix()
    sha = hashlib.sha256(f.read_bytes()).hexdigest()
    reference = by_name.get(relative)
    comparisons.append({'path':relative,'vendorSha256':sha,'checkoutSha256':reference['sha256'] if reference else None,'matches':reference is not None and reference['sha256']==sha})
archive.parent.mkdir(parents=True,exist_ok=True)
prefix = archive.stem + '/'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
    for row in rows: z.write(root/row['path'],prefix+row['path'])
result={'checkoutCommit':revision,'archiveSha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'archiveBytes':archive.stat().st_size,'trackedFileCount':len(rows),'sourceFiles':rows,'vendorFileCount':len(comparisons),'vendorComparisons':comparisons,'vendorFilesMatchCheckout':all(r['matches'] for r in comparisons)}
inventory.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k not in ('sourceFiles','vendorComparisons')}))
'@
$helper = Join-Path $out 'collect-corresponding-source.py'
[IO.File]::WriteAllText($helper, $sourceHelper, $utf8)
$sourceRecords = @()
foreach ($name in @('phpcompatibility/php-compatibility', 'phpcsstandards/phpcsutils')) {
    $dependency = @($lock.packages | Where-Object { $_.name -eq $name })
    if ($dependency.Count -ne 1) { throw "Expected one locked source for $name" }
    $dependency = $dependency[0]
    $reference = $dependency.source.reference
    $url = $dependency.source.url
    if ($reference -notmatch '^[0-9a-f]{40}$' -or $url -notmatch '^https://github\.com/[^/]+/[^/]+\.git$') { throw 'Unexpected locked source identity.' }
    $safe = $name.Replace('/', '-')
    $checkout = Join-Path $out ('downloads/source-' + $safe)
    & git init --quiet $checkout
    if ($LASTEXITCODE -ne 0) { throw 'Source checkout init failed.' }
    & git -C $checkout config core.autocrlf false
    & git -C $checkout config core.eol lf
    & git -C $checkout remote add origin $url
    & git -C $checkout fetch --no-tags --depth 1 origin $reference
    if ($LASTEXITCODE -ne 0) { throw 'Pinned LGPL source fetch failed.' }
    & git -C $checkout checkout --detach --quiet FETCH_HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Pinned LGPL source checkout failed.' }
    $actual = (& git -C $checkout rev-parse HEAD).Trim()
    if ($actual -ne $reference) { throw 'Source checkout commit differs from lock.' }
    $sourceOut = Join-Path $out 'corresponding-source'
    New-Item $sourceOut -ItemType Directory -Force | Out-Null
    $archiveName = "$safe-$reference.zip"
    $inventoryName = "$safe-$reference.json"
    & python $helper $checkout (Join-Path $pack ('engine/vendor/' + $name)) (Join-Path $sourceOut $archiveName) (Join-Path $sourceOut $inventoryName)
    if ($LASTEXITCODE -ne 0) { throw 'Full source enumeration/byte check failed.' }
    $sourceInventory = Get-Content (Join-Path $sourceOut $inventoryName) -Raw | ConvertFrom-Json
    $sourceRecords += [ordered]@{ package = $name; version = $dependency.version; sourceUrl = $url; sourceCommit = $actual
        archivePath = ('corresponding-source/' + $archiveName); archiveSha256 = $sourceInventory.archiveSha256; archiveBytes = $sourceInventory.archiveBytes
        inventoryPath = ('corresponding-source/' + $inventoryName); inventorySha256 = (Digest (Join-Path $sourceOut $inventoryName)); trackedFileCount = $sourceInventory.trackedFileCount
        vendorFileCount = $sourceInventory.vendorFileCount; vendorFilesMatchCheckout = $sourceInventory.vendorFilesMatchCheckout }
}
Save-Json $sourceRecords (Join-Path $out 'corresponding-source.json')
$manifest = Get-Content (Join-Path $pack 'manifest.json') -Raw | ConvertFrom-Json
$summary = [ordered]@{
    environment = [ordered]@{ os = [Environment]::OSVersion.VersionString; architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString(); sdk = (& dotnet --version).Trim(); runId = $env:GITHUB_RUN_ID; checkoutSha = $env:GITHUB_SHA }
    publish = $build; runtimePackages = @($packages | ForEach-Object { [ordered]@{ id = $_.id; version = $_.version; archiveSha256 = $_.archiveSha256 } })
    publishFileCount = $publishFiles.Count; unmappedPublishFiles = @($publishFiles | Where-Object { $_.component.StartsWith('unmapped:') } | ForEach-Object { $_.path })
    packId = $manifest.packId; packManifestSha256 = (Digest (Join-Path $pack 'manifest.json')); packFileCount = $packFiles.Count; copiedPackDocumentCount = $copied.Count
    phpInfo = 'php-info-no-ini.txt'; phpDependencyDiagnostics = $dependencyDiagnostics; loadedPhpModuleCount = $loadedModules.Count; externalCrtModules = @($loadedModules | Where-Object { $_.externalCrtCandidate -and -not $_.inRedistributedPhpZip })
    correspondingSource = $sourceRecords; sourceCollectionPassed = @($sourceRecords | Where-Object { -not $_.vendorFilesMatchCheckout }).Count -eq 0
    scope = 'Actual Windows self-contained publish and complete prepared pack file/source/license inventory. This is evidence collection, not a redistribution/legal approval or clean Windows deployment test.'
}
Save-Json $summary (Join-Path $out 'summary.json')
Write-Output ('T08_SUMMARY_JSON=' + (ConvertTo-Json -InputObject $summary -Depth 30 -Compress))
if (-not $summary.sourceCollectionPassed) { throw 'Shipped LGPL vendor files differ from the full pinned checkout; review corresponding-source evidence before redistribution.' }
if ($summary.unmappedPublishFiles.Count) { throw 'Some publish files have no package/application attribution; inspect publish-files.json.' }
