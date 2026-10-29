using System.IO;
using System.Text.Json;
using Dk.PhpCompatibilityChecker.Models;

namespace Dk.PhpCompatibilityChecker.Services;

/// <summary>Interprets only the verified phpcs4-json-v1 report/exit contract.</summary>
public sealed class PhpcsReportParser
{
    public ScanResult Parse(ScanRequest request, IReadOnlyList<string> selectedFiles,
        ScanEngineInfo engineInfo, int exitCode, string stdout, string stderr)
    {
        var issues = new List<ScanIssue>();
        int? processed = null;
        string? failure = null;
        try
        {
            var selected = new HashSet<string>(selectedFiles.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            Require(selected.Count == selectedFiles.Count, "선택 파일 목록에 중복 경로가 있습니다.");
            using var document = JsonDocument.Parse(stdout);
            CheckDuplicates(document.RootElement);
            var root = Object(document.RootElement);
            var totals = Object(Property(root, "totals"));
            var errors = Number(totals, "errors");
            var warnings = Number(totals, "warnings");
            var fixable = Number(totals, "fixable");
            var files = Property(root, "files");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Require(files.ValueKind == JsonValueKind.Object
                || (files.ValueKind == JsonValueKind.Array && files.GetArrayLength() == 0), "잘못된 검사 파일 결과입니다.");
            if (files.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in files.EnumerateObject())
                {
                    Require(Path.IsPathFullyQualified(entry.Name), "검사 결과 파일 경로가 절대 경로가 아닙니다.");
                    var path = Path.GetFullPath(entry.Name);
                    Require(selected.Contains(path) && seen.Add(path), "선택하지 않았거나 중복된 파일 결과입니다.");
                    var file = Object(entry.Value);
                    var fileErrors = Number(file, "errors");
                    var fileWarnings = Number(file, "warnings");
                    var messages = Property(file, "messages");
                    Require(messages.ValueKind == JsonValueKind.Array, "잘못된 문제 목록입니다.");
                    var fileIssues = new List<ScanIssue>();
                    foreach (var message in messages.EnumerateArray())
                    {
                        var row = Object(message);
                        var type = Text(row, "type");
                        Require(type is "ERROR" or "WARNING", "알 수 없는 문제 구분입니다.");
                        var rawFixable = Property(row, "fixable");
                        Require(rawFixable.ValueKind is JsonValueKind.True or JsonValueKind.False, "잘못된 fixable 값입니다.");
                        fileIssues.Add(new ScanIssue(entry.Name, Number(row, "line"), Number(row, "column"),
                            type, Number(row, "severity"), Text(row, "source"), Text(row, "message"), rawFixable.GetBoolean()));
                    }
                    Require(fileIssues.Count(i => i.Type == "ERROR") == fileErrors
                        && fileIssues.Count(i => i.Type == "WARNING") == fileWarnings, "파일별 문제 수가 일치하지 않습니다.");
                    issues.AddRange(fileIssues);
                }
            }
            // This is an observed report count, never a substitute for the input count.
            processed = seen.Count;
            Require(issues.Count(i => i.Type == "ERROR") == errors
                && issues.Count(i => i.Type == "WARNING") == warnings
                && issues.Count(i => i.Fixable) == fixable, "전체 문제 수가 일치하지 않습니다.");
            Require(selected.Count > 0 && seen.SetEquals(selected), "선택 파일과 검사 결과 파일 목록이 일치하지 않습니다.");
            Require(exitCode is >= 0 and <= 3, "검사 엔진이 비정상 종료했습니다.");
            var expectedExit = (fixable > 0 ? 1 : 0) | (issues.Any(i => !i.Fixable) ? 2 : 0);
            Require(exitCode == expectedExit, "검사 종료 코드와 문제 수가 일치하지 않습니다.");
            Require(string.IsNullOrWhiteSpace(stderr), "검사 엔진의 진단 출력이 있어 검사를 완료할 수 없습니다.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or ArgumentException
            or NotSupportedException or OverflowException)
        {
            failure = ex is JsonException ? "검사 엔진의 JSON 출력이 올바르지 않습니다." : ex.Message;
        }
        if (exitCode is < 0 or > 3)
        {
            processed = null;
            failure ??= "검사 엔진이 비정상 종료했습니다.";
        }
        return new ScanResult(request, failure is null ? ScanStatus.Completed : ScanStatus.Failed,
            selectedFiles.Count, processed, issues.Count(i => i.Type == "ERROR"),
            issues.Count(i => i.Type == "WARNING"), issues.AsReadOnly(), engineInfo, failure)
        { EngineExitCode = exitCode, EngineStandardError = stderr };
    }

    private static void CheckDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                Require(names.Add(property.Name), "검사 JSON에 중복된 필드가 있습니다.");
                CheckDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                CheckDuplicates(item);
    }

    private static JsonElement Property(JsonElement element, string name)
    {
        Require(element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out _), "검사 결과 필드가 없습니다: " + name);
        return element.GetProperty(name);
    }

    private static JsonElement Object(JsonElement element)
    {
        Require(element.ValueKind == JsonValueKind.Object, "검사 결과가 JSON 객체가 아닙니다.");
        return element;
    }

    private static int Number(JsonElement element, string name)
    {
        var value = Property(element, name);
        Require(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _) && value.GetInt32() >= 0,
            "검사 결과 숫자가 올바르지 않습니다: " + name);
        return value.GetInt32();
    }

    private static string Text(JsonElement element, string name)
    {
        var value = Property(element, name);
        Require(value.ValueKind == JsonValueKind.String, "검사 결과 문자열이 올바르지 않습니다: " + name);
        return value.GetString()!;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
