using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dk.PhpCompatibilityChecker.Models;

namespace Dk.PhpCompatibilityChecker.Services;

/// <summary>Installs an offered engine pack for next startup without changing the active scan service.</summary>
public sealed class EnginePackInstaller : IDisposable
{
    private const string Repository = "danhk0612/DK-PHP-Version-Compatibility-Checker";
    private const string ReleaseAssetPrefix = "/" + Repository + "/releases/download/";
    private const int CatalogLimit = 256 * 1024;
    private const int PointerLimit = 4096;
    private const long ArchiveLimit = 128L * 1024 * 1024;
    private const long ExtractedLimit = 256L * 1024 * 1024;
    private const int EntryLimit = 4096;
    private static readonly Regex Identifier = new(@"\A[A-Za-z0-9][A-Za-z0-9._-]{0,127}\z", RegexOptions.CultureInvariant);
    private static readonly Regex Target = new(@"\A[0-9]+\.[0-9]+\z", RegexOptions.CultureInvariant);
    private static readonly Regex Hash = new(@"\A[a-f0-9]{64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex DeviceName = new(@"\A(?:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = false };
    private readonly string _managedRoot;
    private readonly Uri _catalogUri;
    private readonly bool _injectedTransport;
    private readonly HttpClient _http;
    private IReadOnlyList<EnginePackOffer> _offers = Array.Empty<EnginePackOffer>();

    /// <param name="catalogUri">App-owned HTTPS URL. Product catalogs belong to this GitHub repository on the main branch.</param>
    /// <param name="transport">Optional test transport owned and disposed by the installer.</param>
    public EnginePackInstaller(string managedRoot, Uri catalogUri, HttpMessageHandler? transport = null)
    {
        ArgumentNullException.ThrowIfNull(catalogUri);
        _managedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(managedRoot));
        _injectedTransport = transport is not null;
        Require(IsPlainHttps(catalogUri) && string.IsNullOrEmpty(catalogUri.Query)
            && (_injectedTransport || (catalogUri.Host == "raw.githubusercontent.com"
                && catalogUri.AbsolutePath.StartsWith("/" + Repository + "/main/", StringComparison.Ordinal))),
            "신뢰할 수 있는 엔진 팩 목록 주소가 아닙니다.");
        _catalogUri = catalogUri;
        _http = new HttpClient(transport ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<IReadOnlyList<EnginePackOffer>> ReadCatalogAsync(CancellationToken ct = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        using var response = await GetAsync(_catalogUri, asset: false, deadline.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await using (var body = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false))
            await CopyBoundedAsync(body, buffer, CatalogLimit, deadline.Token).ConfigureAwait(false);
        using var document = Parse(buffer.ToArray());
        var catalog = document.RootElement;
        Require(ReadInt64(catalog, "schemaVersion") == 1, "지원하지 않는 엔진 팩 목록 형식입니다.");
        var offers = ReadArray(catalog, "packs").EnumerateArray().Select(ReadOffer).ToArray();
        Require(offers.Select(offer => offer.PackId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == offers.Length,
            "엔진 팩 목록에 중복 ID가 있습니다.");
        var snapshot = Array.AsReadOnly(offers);
        Volatile.Write(ref _offers, snapshot);
        return snapshot;
    }

    public async Task<EnginePack> InstallAsync(EnginePackOffer offer, string sourceDirectory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(offer);
        Require(Volatile.Read(ref _offers).Any(row => ReferenceEquals(row, offer)), "현재 목록에서 확인한 엔진 팩을 선택하세요.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        token.ThrowIfCancellationRequested();
        var rootIdentity = CheckManagedRoot(_managedRoot, sourceDirectory);
        Directory.CreateDirectory(_managedRoot);
        RequireRootIdentity(_managedRoot, rootIdentity);
        var lockPath = Path.Combine(_managedRoot, ".install.lock");
        SourceFileEnumerator.RejectReparseAncestors(lockPath);
        // One installer at a time across application processes. This file is a
        // lock only, and is never truncated or written.
        using var installLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var prior = await ReadActivationAsync(_managedRoot, token).ConfigureAwait(false);
        var previous = prior?.Current;
        var previousPack = await TryLoadAsync(_managedRoot, previous, token).ConfigureAwait(false);
        if (previousPack is null)
        {
            previous = prior?.Previous;
            previousPack = await TryLoadAsync(_managedRoot, previous, token).ConfigureAwait(false);
        }
        Require(previousPack is null || previousPack.Targets.All(target => offer.Targets.Any(row => row.Version == target.Version)),
            "설치된 엔진 팩의 검사 목표가 새 팩에서 누락되었습니다.");
        var current = new ActivationEntry(offer.ArchiveSha256, offer.ManifestSha256);
        if (previous == current)
            previous = prior?.Previous == current ? null : prior?.Previous;

        var staging = Path.Combine(_managedRoot, ".stage-" + Guid.NewGuid().ToString("N"));
        RequireRootIdentity(_managedRoot, rootIdentity);
        Directory.CreateDirectory(staging);
        SourceFileEnumerator.RejectReparseAncestors(staging);
        try
        {
            var archivePath = Path.Combine(staging, "pack.zip");
            await DownloadAsync(offer, archivePath, token).ConfigureAwait(false);
            var payload = Path.Combine(staging, "payload");
            Directory.CreateDirectory(payload);
            await ExtractAsync(archivePath, payload, token).ConfigureAwait(false);
            await ValidateAsync(Path.Combine(payload, "runtime"), offer, token).ConfigureAwait(false);
            RequireRootIdentity(_managedRoot, rootIdentity);
            SourceFileEnumerator.RejectReparseAncestors(payload);
            var versions = Path.Combine(_managedRoot, "versions");
            SourceFileEnumerator.RejectReparseAncestors(versions);
            Directory.CreateDirectory(versions);
            var destination = Path.Combine(versions, offer.ArchiveSha256);
            SourceFileEnumerator.RejectReparseAncestors(destination);
            if (!Directory.Exists(destination))
            {
                token.ThrowIfCancellationRequested();
                Directory.Move(payload, destination);
            }
            // Reusing an installed identity never modifies that directory.
            var installed = await ValidateAsync(Path.Combine(destination, "runtime"), offer, token).ConfigureAwait(false);
            DeleteStaging(_managedRoot, rootIdentity, staging);
            await ActivateAsync(new Activation(current, previous), rootIdentity, token).ConfigureAwait(false);
            return installed;
        }
        catch
        {
            try { DeleteStaging(_managedRoot, rootIdentity, staging); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    public static async Task<EnginePack?> LoadInstalledAsync(string managedRoot, CancellationToken ct = default)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(managedRoot));
        var state = await ReadActivationAsync(root, ct).ConfigureAwait(false);
        foreach (var entry in new[] { state?.Current, state?.Previous })
        {
            var pack = await TryLoadAsync(root, entry, ct).ConfigureAwait(false);
            if (pack is not null)
                return pack;
        }
        return null;
    }

    public void Dispose() => _http.Dispose();

    private EnginePackOffer ReadOffer(JsonElement entry)
    {
        var id = ReadText(entry, "packId");
        var assetUrl = ReadUrl(entry, "assetUrl");
        var sourceUrl = ReadUrl(entry, "sourceUrl");
        var archiveHash = ReadText(entry, "archiveSha256");
        var manifestHash = ReadText(entry, "manifestSha256");
        var bytes = ReadInt64(entry, "archiveBytes");
        var runtimeVersion = ReadText(entry, "phpRuntimeVersion");
        var profile = ReadText(entry, "reportProfile");
        Require(ReadInt64(entry, "packSchemaVersion") == 1 && profile == "phpcs4-json-v1", "지원하지 않는 엔진 팩 형식입니다.");
        Require(Identifier.IsMatch(id) && Identifier.IsMatch(runtimeVersion) && Hash.IsMatch(archiveHash)
            && Hash.IsMatch(manifestHash) && bytes > 0 && bytes <= ArchiveLimit, "엔진 팩 크기·식별자·해시 정보가 올바르지 않습니다.");
        Require(IsAllowedAsset(assetUrl) && IsAllowedAsset(sourceUrl), "허용된 엔진 팩·대응 소스 주소가 아닙니다.");
        var targets = ReadArray(entry, "targets").EnumerateArray().Select(target =>
        {
            var version = ReadText(target, "version");
            Require(Target.IsMatch(version), "잘못된 목표 PHP 버전입니다.");
            var limitations = ReadArray(target, "limitations").EnumerateArray().Select(ReadString).ToArray();
            return new EnginePackOfferTarget(version, Array.AsReadOnly(limitations));
        }).ToArray();
        Require(targets.Length > 0 && targets.Select(target => target.Version).Distinct(StringComparer.Ordinal).Count() == targets.Length,
            "엔진 팩 목록의 목표 PHP 버전이 없거나 중복되었습니다.");
        var versions = ReadObject(entry, "engineVersions").EnumerateObject()
            .ToDictionary(pair => pair.Name, pair => ReadString(pair.Value), StringComparer.Ordinal);
        Require(versions.Count > 0, "엔진 구성 버전이 없습니다.");
        return new EnginePackOffer(id, 1, profile, assetUrl, sourceUrl, bytes, archiveHash, manifestHash,
            runtimeVersion, new ReadOnlyDictionary<string, string>(versions), Array.AsReadOnly(targets));
    }

    private async Task DownloadAsync(EnginePackOffer offer, string path, CancellationToken token)
    {
        using var response = await GetAsync(offer.AssetUrl, asset: true, token).ConfigureAwait(false);
        Require(response.Content.Headers.ContentLength is null || response.Content.Headers.ContentLength == offer.ArchiveBytes,
            "엔진 팩 다운로드 크기가 목록과 다릅니다.");
        await using (var body = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true))
            Require(await CopyBoundedAsync(body, file, offer.ArchiveBytes, token).ConfigureAwait(false) == offer.ArchiveBytes,
                "엔진 팩 다운로드가 끝나기 전에 연결이 종료되었습니다.");
        Require(await FileHashAsync(path, token).ConfigureAwait(false) == offer.ArchiveSha256, "엔진 팩 ZIP 해시가 목록과 다릅니다.");
    }

    private async Task<HttpResponseMessage> GetAsync(Uri uri, bool asset, CancellationToken ct)
    {
        var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
        {
            using (response)
            {
                var location = response.Headers.Location;
                Require(asset && !_injectedTransport && location is { IsAbsoluteUri: true }
                    && IsPlainHttps(location) && location.Host == "release-assets.githubusercontent.com",
                    "허용하지 않는 엔진 팩 다운로드 이동 주소입니다.");
                uri = location!;
            }
            response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        if (!response.IsSuccessStatusCode)
        {
            var status = response.StatusCode;
            response.Dispose();
            throw new HttpRequestException("엔진 팩 서버 응답 오류: " + (int)status, null, status);
        }
        return response;
    }

    private bool IsAllowedAsset(Uri uri) => IsPlainHttps(uri) && string.IsNullOrEmpty(uri.Query)
        && (_injectedTransport ? uri.GetLeftPart(UriPartial.Authority) == _catalogUri.GetLeftPart(UriPartial.Authority)
            : uri.Host == "github.com" && uri.AbsolutePath.StartsWith(ReleaseAssetPrefix, StringComparison.Ordinal)
                && uri.AbsolutePath[ReleaseAssetPrefix.Length..].Split('/') is [var tag, var file]
                && Identifier.IsMatch(tag) && Identifier.IsMatch(file) && file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

    private static bool IsPlainHttps(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment);

    private static async Task ExtractAsync(string archivePath, string destination, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        Require(archive.Entries.Count is > 0 and <= EntryLimit, "엔진 팩 ZIP 파일 수가 허용 범위를 벗어났습니다.");
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long remaining = ExtractedLimit;
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            var isDirectory = entry.FullName.EndsWith('/');
            var relative = isDirectory ? entry.FullName[..^1] : entry.FullName;
            var parts = relative.Split('/');
            int kind = (entry.ExternalAttributes >>> 16) & 0xf000;
            Require(kind == 0 || kind == (isDirectory ? 0x4000 : 0x8000), "엔진 팩 ZIP에 링크 또는 특수 항목이 있습니다.");
            Require((entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) == 0,
                "엔진 팩 ZIP에 reparse point가 있습니다.");
            Require(parts[0] is "runtime" or "notices" && (parts.Length > 1 || isDirectory)
                && relative.Length <= 512 && parts.All(IsSafeZipSegment) && entries.Add(relative),
                "엔진 팩 ZIP의 경로가 올바르지 않거나 중복되었습니다.");
            var prefix = parts[0];
            for (int index = 0; index < parts.Length - (isDirectory ? 0 : 1); index++)
            {
                if (index > 0)
                    prefix += "/" + parts[index];
                Require(!files.Contains(prefix)
                    && (!directories.TryGetValue(prefix, out var existing) || existing == prefix),
                    "엔진 팩 ZIP에 충돌하는 폴더가 있습니다.");
                directories[prefix] = prefix;
            }
            var path = Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
            if (isDirectory)
            {
                Require(entry.Length == 0, "엔진 팩 ZIP의 폴더 항목에 데이터가 있습니다.");
                Directory.CreateDirectory(path);
                continue;
            }
            Require(!directories.ContainsKey(relative) && files.Add(relative) && entry.Length >= 0 && entry.Length <= remaining,
                "엔진 팩 ZIP의 경로나 풀린 데이터 크기가 올바르지 않습니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using var content = entry.Open();
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
            Require(await CopyBoundedAsync(content, output, entry.Length, ct).ConfigureAwait(false) == entry.Length,
                "엔진 팩 ZIP의 파일 데이터가 잘렸습니다.");
            remaining -= entry.Length;
        }
    }

    private static bool IsSafeZipSegment(string name) => name.Length is > 0 and <= 128
        && name is not "." and not ".." && !name.EndsWith('.')
        && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
        && !DeviceName.IsMatch(name.Split('.', 2)[0]);

    private static async Task<EnginePack> ValidateAsync(string root, EnginePackOffer offer, CancellationToken ct)
    {
        var manifest = Path.Combine(root, "manifest.json");
        SourceFileEnumerator.RejectReparseAncestors(manifest);
        Require(File.Exists(manifest) && await FileHashAsync(manifest, ct).ConfigureAwait(false) == offer.ManifestSha256,
            "엔진 팩 manifest 해시가 목록과 다릅니다.");
        var pack = await new EnginePackLoader().LoadAsync(root, ct).ConfigureAwait(false);
        Require(pack.PackId == offer.PackId && pack.ReportProfile == offer.ReportProfile
            && pack.EngineInfo.PhpRuntimeVersion == offer.PhpRuntimeVersion
            && pack.Targets.Count == offer.Targets.Count
            && pack.Targets.Zip(offer.Targets).All(pair => pair.First.Version == pair.Second.Version
                && pair.First.Limitations.SequenceEqual(pair.Second.Limitations)),
            "엔진 팩 구성 또는 검증된 목표가 목록과 다릅니다.");
        using var document = Parse(await File.ReadAllBytesAsync(manifest, ct).ConfigureAwait(false));
        var versions = ReadObject(ReadObject(document.RootElement, "engine"), "versions").EnumerateObject().ToArray();
        Require(versions.Length == offer.EngineVersions.Count && versions.All(pair =>
            offer.EngineVersions.TryGetValue(pair.Name, out var version) && ReadString(pair.Value) == version),
            "엔진 팩 구성 버전이 목록과 다릅니다.");
        return pack;
    }

    private async Task ActivateAsync(Activation state, string rootIdentity, CancellationToken ct)
    {
        var pointer = Path.Combine(_managedRoot, "active.json");
        var temp = Path.Combine(_managedRoot, ".active-" + Guid.NewGuid().ToString("N"));
        RequireRootIdentity(_managedRoot, rootIdentity);
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            RequireRootIdentity(_managedRoot, rootIdentity);
            // This replaces only the directory entry for the pointer, never the
            // bytes of a prior pointer or an externally hardlinked target.
            File.Move(temp, pointer, overwrite: true);
        }
        catch
        {
            try
            {
                RequireRootIdentity(_managedRoot, rootIdentity);
                File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    private static async Task<Activation?> ReadActivationAsync(string root, CancellationToken ct)
    {
        try
        {
            var path = Path.Combine(root, "active.json");
            SourceFileEnumerator.RejectReparseAncestors(path);
            if (!File.Exists(path))
                return null;
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, useAsync: true);
            using var data = new MemoryStream();
            await CopyBoundedAsync(stream, data, PointerLimit, ct).ConfigureAwait(false);
            using var document = Parse(data.ToArray());
            var state = document.RootElement;
            return new Activation(ReadActivationEntry(state, "current"), ReadActivationEntry(state, "previous"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    private static ActivationEntry? ReadActivationEntry(JsonElement state, string name)
    {
        if (!state.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
            return null;
        var archive = value.TryGetProperty("archiveSha256", out var archiveValue) && archiveValue.ValueKind == JsonValueKind.String
            ? archiveValue.GetString() : null;
        var manifest = value.TryGetProperty("manifestSha256", out var manifestValue) && manifestValue.ValueKind == JsonValueKind.String
            ? manifestValue.GetString() : null;
        return archive is not null && manifest is not null && Hash.IsMatch(archive) && Hash.IsMatch(manifest)
            ? new ActivationEntry(archive, manifest) : null;
    }

    private static async Task<EnginePack?> TryLoadAsync(string managedRoot, ActivationEntry? entry, CancellationToken ct)
    {
        if (entry is null)
            return null;
        try
        {
            var root = Path.Combine(managedRoot, "versions", entry.ArchiveSha256, "runtime");
            var manifest = Path.Combine(root, "manifest.json");
            SourceFileEnumerator.RejectReparseAncestors(manifest);
            if (!File.Exists(manifest) || await FileHashAsync(manifest, ct).ConfigureAwait(false) != entry.ManifestSha256)
                return null;
            return await new EnginePackLoader().LoadAsync(root, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    private static string CheckManagedRoot(string root, string source)
    {
        var physicalRoot = SourceFileEnumerator.PhysicalDirectoryPath(root, allowMissing: true);
        if (!string.IsNullOrWhiteSpace(source))
        {
            var physicalSource = SourceFileEnumerator.PhysicalDirectoryPath(source, allowMissing: true);
            if (Contains(physicalRoot, physicalSource) || Contains(physicalSource, physicalRoot))
                throw new IOException("검사 대상과 엔진 팩 관리 폴더를 분리하세요.");
        }
        return physicalRoot;
    }

    private static bool Contains(string root, string child) => child.Equals(root, StringComparison.OrdinalIgnoreCase)
        || child.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);

    private static void RequireRootIdentity(string root, string expected)
    {
        if (!SourceFileEnumerator.PhysicalDirectoryPath(root).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new IOException("엔진 팩 관리 폴더가 변경되었습니다.");
    }

    private static void DeleteStaging(string root, string identity, string staging)
    {
        RequireRootIdentity(root, identity);
        SourceFileEnumerator.RejectReparseAncestors(staging);
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
    }

    private static async Task<long> CopyBoundedAsync(Stream input, Stream output, long limit, CancellationToken ct)
    {
        var buffer = new byte[65536];
        long count = 0;
        int read;
        while ((read = await input.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) != 0)
        {
            count += read;
            Require(count <= limit, "엔진 팩 데이터가 허용된 크기를 초과했습니다.");
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
        return count;
    }

    private static async Task<string> FileHashAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false));
    }

    private static JsonDocument Parse(byte[] data)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(data.AsMemory(data.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }) ? 3 : 0));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("엔진 팩 JSON 형식이 올바르지 않습니다.", ex);
        }
        try
        {
            Require(document.RootElement.ValueKind == JsonValueKind.Object, "엔진 팩 JSON 객체가 필요합니다.");
            CheckDuplicateKeys(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void CheckDuplicateKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Require(names.Add(property.Name), "엔진 팩 JSON에 중복 항목이 있습니다.");
                CheckDuplicateKeys(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                CheckDuplicateKeys(item);
        }
    }

    private static JsonElement ReadProperty(JsonElement value, string name, JsonValueKind kind)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var property) || property.ValueKind != kind)
            throw new InvalidDataException("엔진 팩 목록 항목이 올바르지 않습니다: " + name);
        return property;
    }

    private static JsonElement ReadObject(JsonElement value, string name) => ReadProperty(value, name, JsonValueKind.Object);
    private static JsonElement ReadArray(JsonElement value, string name) => ReadProperty(value, name, JsonValueKind.Array);
    private static string ReadText(JsonElement value, string name) => ReadString(ReadProperty(value, name, JsonValueKind.String));
    private static string ReadString(JsonElement value) => value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
        ? text : throw new InvalidDataException("엔진 팩 목록에 비어 있거나 잘못된 문자열이 있습니다.");
    private static long ReadInt64(JsonElement value, string name) => ReadProperty(value, name, JsonValueKind.Number).TryGetInt64(out var number)
        ? number : throw new InvalidDataException("엔진 팩 목록에 올바른 정수가 필요합니다: " + name);
    private static Uri ReadUrl(JsonElement value, string name) => Uri.TryCreate(ReadText(value, name), UriKind.Absolute, out var uri)
        ? uri : throw new InvalidDataException("엔진 팩 목록에 올바른 주소가 필요합니다: " + name);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record Activation(ActivationEntry? Current, ActivationEntry? Previous);
    private sealed record ActivationEntry(string ArchiveSha256, string ManifestSha256);
}
