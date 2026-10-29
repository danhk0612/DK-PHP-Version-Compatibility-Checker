using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dk.PhpCompatibilityChecker.Models;

namespace Dk.PhpCompatibilityChecker.Services;

/// <summary>Reads a trusted schema 1 pack and checks its complete on-disk composition.</summary>
public sealed class EnginePackLoader
{
    private const string ReportProfile = "phpcs4-json-v1";
    private const string PhpEntry = "php/php.exe";
    private const string PhpcsEntry = "engine/vendor/squizlabs/php_codesniffer/bin/phpcs";
    private const string RulesetEntry = "engine/ruleset.xml";

    public Task<EnginePack> LoadAsync(string packDirectory, CancellationToken cancellationToken = default) =>
        Task.Run(() => Load(packDirectory, cancellationToken), cancellationToken);

    private static EnginePack Load(string packDirectory, CancellationToken cancellationToken)
    {
        try
        {
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(packDirectory));
            Require(Directory.Exists(root), "엔진 팩 폴더가 없습니다.");
            RejectReparseAncestors(root);
            var actualFiles = Inventory(root, cancellationToken);
            Require(actualFiles.ContainsKey("manifest.json"), "엔진 팩 manifest.json이 없습니다.");
            using var manifestDocument = ReadJson(Path.Combine(root, "manifest.json"));
            var manifest = Object(manifestDocument.RootElement);
            var schema = Property(manifest, "schemaVersion");
            Require(schema.ValueKind == JsonValueKind.Number && schema.TryGetInt32(out var version) && version == 1,
                "지원하지 않는 엔진 팩 스키마입니다.");
            Require(Text(manifest, "reportProfile") == ReportProfile, "지원하지 않는 보고서 형식입니다.");
            var packId = Text(manifest, "packId");
            Require(Regex.IsMatch(packId, "\\A[A-Za-z0-9._-]+\\z", RegexOptions.CultureInvariant), "잘못된 엔진 팩 ID입니다.");
            var runtime = Object(Property(manifest, "runtime"));
            var engine = Object(Property(manifest, "engine"));
            var verification = Object(Property(manifest, "verification"));
            var runtimeFiles = FileMap(runtime, "php");
            var engineFiles = FileMap(engine, "engine");
            var verificationFiles = FileMap(verification, "verification");
            var expectedFiles = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var files in new[] { runtimeFiles, engineFiles, verificationFiles })
                foreach (var item in files)
                    Require(expectedFiles.TryAdd(item.Key, item.Value), "중복된 팩 파일 경로입니다.");
            Require(expectedFiles.Count + 1 == actualFiles.Count, "엔진 팩에 누락되거나 추가된 파일이 있습니다.");
            foreach (var item in expectedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Require(actualFiles.TryGetValue(item.Key, out var actual) && actual == item.Value,
                    "엔진 팩 파일이 없거나 해시가 다릅니다: " + item.Key);
            }
            Require(Text(runtime, "executable") == PhpEntry && Text(engine, "entryPoint") == PhpcsEntry
                && Text(engine, "ruleset") == RulesetEntry, "지원하지 않는 엔진 실행 경로입니다.");
            Require(runtimeFiles.ContainsKey(PhpEntry) && engineFiles.ContainsKey(PhpcsEntry)
                && engineFiles.ContainsKey(RulesetEntry), "필수 엔진 실행 파일이 없습니다.");
            var runtimeVersion = Text(runtime, "version");
            var cliVersion = Text(runtime, "cliVersion");
            var archiveHash = Text(runtime, "archiveSha256");
            Require(IsHash(archiveHash) && Uri.TryCreate(Text(runtime, "sourceUrl"), UriKind.Absolute, out var source)
                && source.Scheme == Uri.UriSchemeHttps && source.Host == "downloads.php.net"
                && source.AbsolutePath.StartsWith("/~windows/releases/", StringComparison.Ordinal), "잘못된 PHP 배포 출처입니다.");
            var versions = StringMap(Property(engine, "versions"));
            Require(versions.Count > 0, "엔진 버전 목록이 없습니다.");
            foreach (var name in new[] { "phpcompatibility/php-compatibility", "squizlabs/php_codesniffer",
                "phpcsstandards/phpcsutils", "dealerdirect/phpcodesniffer-composer-installer" })
                Require(versions.ContainsKey(name), "필수 엔진 버전 정보가 없습니다: " + name);
            Require(engineFiles.TryGetValue("engine/composer.lock", out var lockHash)
                && Text(engine, "lockSha256") == lockHash, "Composer lock 해시가 다릅니다.");
            Require(engineFiles.ContainsKey("engine/vendor/composer/installed.json"), "설치된 엔진 정보가 없습니다.");
            using var lockDocument = ReadJson(CheckedPath(root, "engine/composer.lock"));
            using var installedDocument = ReadJson(CheckedPath(root, "engine/vendor/composer/installed.json"));
            var lockData = Object(lockDocument.RootElement);
            Require(Array(Property(lockData, "packages-dev")).GetArrayLength() == 0, "개발용 의존성이 포함된 엔진 팩입니다.");
            var lockedPackages = Packages(Property(lockData, "packages"));
            var installedPackages = Packages(Property(Object(installedDocument.RootElement), "packages"));
            Require(Equivalent(lockedPackages.Versions, versions) && Equivalent(installedPackages.Versions, versions)
                && Equivalent(lockedPackages.References, installedPackages.References), "lock과 설치된 엔진 버전 또는 참조가 다릅니다.");
            var reportPath = Text(verification, "reportPath");
            Require(verificationFiles.ContainsKey(reportPath), "검증 근거 파일이 팩 목록에 없습니다.");
            using var evidenceDocument = ReadJson(CheckedPath(root, reportPath));
            var evidence = Object(evidenceDocument.RootElement);
            Require(Text(evidence, "packId") == packId && Text(evidence, "reportProfile") == ReportProfile,
                "검증 근거의 팩 ID 또는 보고서 형식이 다릅니다.");
            Require(Equivalent(StringMap(Property(evidence, "versions")), versions)
                && Text(evidence, "runtimeVersion") == runtimeVersion && Text(evidence, "cliVersion") == cliVersion
                && Text(evidence, "archiveSha256") == archiveHash, "검증 근거의 구성 버전이 다릅니다.");
            Require(Equivalent(StringMap(Property(evidence, "engineFiles")), engineFiles)
                && Equivalent(StringMap(Property(evidence, "runtimeFiles")), runtimeFiles), "검증 근거의 파일 구성이 다릅니다.");
            var checks = Object(Property(evidence, "checks"));
            Require(checks.EnumerateObject().Any() && checks.EnumerateObject().All(p => p.Value.ValueKind == JsonValueKind.True),
                "실패하거나 잘못된 팩 검증 결과가 있습니다.");
            foreach (var name in new[] { "fixtureRegression", "annotations", "sourceUnchanged", "offline",
                "configIsolation", "jsonContract", "withoutComposer" })
                Require(Property(checks, name).ValueKind == JsonValueKind.True, "필수 팩 검증 결과가 없습니다: " + name);
            var evidenceTargets = Object(Property(evidence, "targets"));
            var cases = Array(Property(evidence, "cases"));
            var targets = new List<PhpTarget>();
            var seenTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in Array(Property(manifest, "targets")).EnumerateArray())
            {
                var target = Object(item);
                var targetVersion = Text(target, "version");
                Require(Regex.IsMatch(targetVersion, "\\A[0-9]+\\.[0-9]+\\z", RegexOptions.CultureInvariant)
                    && seenTargets.Add(targetVersion), "잘못되거나 중복된 목표 PHP 버전입니다.");
                var verificationRef = Text(target, "verificationRef");
                Require(verificationRef == reportPath + "#targets/" + targetVersion, "목표 PHP 버전의 검증 참조가 다릅니다.");
                var limitations = Strings(Property(target, "limitations"));
                var targetEvidence = Object(Property(evidenceTargets, targetVersion));
                Require(Property(targetEvidence, "passed").ValueKind == JsonValueKind.True
                    && limitations.SequenceEqual(Strings(Property(targetEvidence, "limitations"))), "검증되지 않은 목표 PHP 버전입니다.");
                var targetCases = cases.EnumerateArray().Where(c => Text(Object(c), "target") == targetVersion).ToArray();
                Require(targetCases.Any(c => Text(c, "expectation") == "finding")
                    && targetCases.Any(c => Text(c, "expectation") == "no_findings"), "목표 PHP 버전의 양성/음성 대조가 없습니다.");
                targets.Add(new PhpTarget(targetVersion, verificationRef, System.Array.AsReadOnly(limitations)));
            }
            Require(targets.Count > 0 && seenTargets.SetEquals(evidenceTargets.EnumerateObject().Select(p => p.Name)),
                "목표 목록과 검증 근거가 다릅니다.");
            cancellationToken.ThrowIfCancellationRequested();
            var info = new ScanEngineInfo(packId, runtimeVersion, versions["phpcompatibility/php-compatibility"],
                versions["squizlabs/php_codesniffer"], versions["phpcsstandards/phpcsutils"]);
            return new EnginePack(root, packId, ReportProfile, CheckedPath(root, PhpEntry), CheckedPath(root, PhpcsEntry),
                CheckedPath(root, RulesetEntry), CheckedPath(root, "engine"), targets.AsReadOnly(), info);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("엔진 팩 JSON을 읽을 수 없습니다.", ex);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("잘못된 엔진 팩 경로 또는 값입니다.", ex);
        }
    }

    private static Dictionary<string, string> Inventory(string root, CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                Require((attributes & FileAttributes.ReparsePoint) == 0, "엔진 팩에 링크 또는 reparse point가 있습니다.");
                var relative = Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/');
                Require(names.Add(relative), "대소문자가 충돌하는 팩 경로입니다.");
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push(entry);
                else
                {
                    _ = CheckedPath(root, relative);
                    using var stream = File.OpenRead(entry);
                    files.Add(relative, Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
                }
            }
        }
        return files;
    }

    private static void RejectReparseAncestors(string path)
    {
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "엔진 팩 경로에 링크 또는 reparse point가 있습니다.");
    }

    private static Dictionary<string, string> FileMap(JsonElement section, string folder)
    {
        var files = StringMap(Property(section, "files"));
        Require(files.Count > 0, "엔진 팩 파일 목록이 비어 있습니다.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in files)
        {
            ValidateRelativeName(item.Key);
            Require(item.Key.StartsWith(folder + "/", StringComparison.Ordinal) && IsHash(item.Value)
                && names.Add(item.Key), "잘못되거나 충돌하는 팩 파일 목록입니다.");
        }
        return files;
    }

    private static string CheckedPath(string root, string relative)
    {
        ValidateRelativeName(relative);
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        Require(full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
            "엔진 팩 밖을 가리키는 경로입니다.");
        return full;
    }

    private static void ValidateRelativeName(string relative)
    {
        Require(!string.IsNullOrEmpty(relative) && !relative.StartsWith('/') && !relative.Contains('\\')
            && !relative.Any(c => char.IsControl(c) || ":<>\"|?*".Contains(c)), "잘못된 팩 상대 경로입니다.");
        foreach (var part in relative.Split('/'))
            Require(part.Length > 0 && part != "." && part != ".." && !part.EndsWith('.') && !part.EndsWith(' '),
                "정규화되지 않은 팩 상대 경로입니다.");
    }

    private static JsonDocument ReadJson(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var offset = bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf ? 3 : 0;
        var document = JsonDocument.Parse(bytes.AsMemory(offset));
        try
        {
            CheckDuplicates(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), "중복된 JSON 필드가 있습니다.");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                CheckDuplicates(item);
    }

    private static (Dictionary<string, string> Versions, Dictionary<string, string> References) Packages(JsonElement packages)
    {
        var versions = new Dictionary<string, string>(StringComparer.Ordinal);
        var references = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in Array(packages).EnumerateArray())
        {
            var package = Object(item);
            var name = Text(package, "name");
            Require(versions.TryAdd(name, Text(package, "version"))
                && references.TryAdd(name, Text(Object(Property(package, "source")), "reference")), "중복된 Composer 패키지입니다.");
        }
        return (versions, references);
    }

    private static Dictionary<string, string> StringMap(JsonElement element)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in Object(element).EnumerateObject())
        {
            Require(property.Value.ValueKind == JsonValueKind.String, "문자열 값이 아닌 팩 정보가 있습니다.");
            var value = property.Value.GetString();
            Require(!string.IsNullOrEmpty(value) && result.TryAdd(property.Name, value), "비어 있거나 중복된 팩 정보입니다.");
        }
        return result;
    }

    private static string[] Strings(JsonElement element)
    {
        var result = new List<string>();
        foreach (var value in Array(element).EnumerateArray())
        {
            Require(value.ValueKind == JsonValueKind.String, "잘못된 한계 설명입니다.");
            result.Add(value.GetString()!);
        }
        return result.ToArray();
    }

    private static JsonElement Property(JsonElement element, string name)
    {
        Require(element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _), "필수 팩 정보가 없습니다: " + name);
        return element.GetProperty(name);
    }

    private static string Text(JsonElement element, string name)
    {
        var value = Property(element, name);
        Require(value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString()), "잘못된 팩 문자열 정보입니다: " + name);
        return value.GetString()!;
    }

    private static JsonElement Object(JsonElement element)
    {
        Require(element.ValueKind == JsonValueKind.Object, "팩 JSON 객체가 아닙니다.");
        return element;
    }

    private static JsonElement Array(JsonElement element)
    {
        Require(element.ValueKind == JsonValueKind.Array, "팩 JSON 배열이 아닙니다.");
        return element;
    }

    private static bool Equivalent(Dictionary<string, string> first, Dictionary<string, string> second) =>
        first.Count == second.Count && first.All(p => second.TryGetValue(p.Key, out var value) && value == p.Value);

    private static bool IsHash(string value) => Regex.IsMatch(value, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
