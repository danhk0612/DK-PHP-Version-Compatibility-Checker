using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Dk.PhpCompatibilityChecker.Models;

namespace Dk.PhpCompatibilityChecker.Services;

/// <summary>Exports a terminal scan snapshot without writing into the inspected project.</summary>
public static class ScanResultExporter
{
    private const string AnalysisLimit = "정적 분석 결과이며 실제 PHP 실행, DB·외부 라이브러리 및 서비스의 정상 동작을 보장하지 않습니다.";
    private const string CsvTextNotice = "CSV 텍스트의 첫 비공백 문자가 =,+,-,@이거나 원문이 작은따옴표로 시작하면 안전한 엑셀 열람과 원문 구분을 위해 앞에 작은따옴표 하나를 추가했습니다. 내보낸 텍스트가 작은따옴표로 시작하면 그 접두사 하나를 제거하여 원문을 복원할 수 있습니다.";

    public static string RenderCsv(ScanResult result, IReadOnlyList<string> limitations)
    {
        var snapshot = Snapshot(result, limitations);
        return Csv(snapshot.Result, snapshot.Limitations);
    }

    public static string RenderHtml(ScanResult result, IReadOnlyList<string> limitations)
    {
        var snapshot = Snapshot(result, limitations);
        return Html(snapshot.Result, snapshot.Limitations);
    }

    // Deliberately not async here: caller-owned collections are copied before
    // returning the task, so dialog-time/result changes cannot alter the export.
    public static Task SaveAsync(ScanResult result, IReadOnlyList<string> limitations,
        string destination, bool html, CancellationToken ct = default)
    {
        var snapshot = Snapshot(result, limitations);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var full = Path.GetFullPath(destination);
        var extension = Path.GetExtension(full);
        if (html ? !extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
                   && !extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
                 : !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(html ? "HTML 형식은 .html 또는 .htm으로 저장하세요." : "CSV 형식은 .csv로 저장하세요.", nameof(destination));
        return SaveCoreAsync(snapshot.Result, snapshot.Limitations, full, html, ct);
    }

    private static (ScanResult Result, IReadOnlyList<string> Limitations) Snapshot(
        ScanResult result, IReadOnlyList<string> limitations)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(limitations);
        if (result.Status is not (ScanStatus.Completed or ScanStatus.Failed or ScanStatus.NoFiles))
            throw new ArgumentException("완료되거나 실패한 검사 결과만 저장할 수 있습니다.", nameof(result));
        return (result with
        {
            Request = result.Request with
            {
                ExcludedPaths = result.Request.ExcludedPaths is null ? null
                    : Array.AsReadOnly(result.Request.ExcludedPaths.ToArray())
            },
            Issues = Array.AsReadOnly(result.Issues.ToArray()),
            ExcludedPaths = Array.AsReadOnly(result.ExcludedPaths.ToArray())
        }, Array.AsReadOnly(limitations.ToArray()));
    }

    private static async Task SaveCoreAsync(ScanResult result, IReadOnlyList<string> limitations,
        string destination, bool html, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var parent = Path.GetDirectoryName(destination)
            ?? throw new ArgumentException("저장 폴더를 확인할 수 없습니다.", nameof(destination));
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(result.Request.SourceDirectory));
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException("저장할 기존 폴더를 선택하세요.");
        if (Contains(source, destination))
            throw new IOException("검사 대상 폴더 밖에 결과를 저장하세요.");
        SourceFileEnumerator.RejectReparseAncestors(parent);
        var physicalSource = SourceFileEnumerator.PhysicalDirectoryPath(source, allowMissing: true);
        var physicalParent = SourceFileEnumerator.PhysicalDirectoryPath(parent);
        if (Contains(physicalSource, physicalParent))
            throw new IOException("검사 대상 폴더의 별칭 경로에는 결과를 저장할 수 없습니다.");
        var contents = html ? Html(result, limitations) : Csv(result, limitations);
        var temporary = Path.Combine(parent, ".dk-php-export-" + Guid.NewGuid().ToString("N") + ".tmp");
        var ownsTemporary = false;
        try
        {
            ct.ThrowIfCancellationRequested();
            SourceFileEnumerator.RejectReparseAncestors(parent);
            if (!SourceFileEnumerator.PhysicalDirectoryPath(parent).Equals(physicalParent, StringComparison.OrdinalIgnoreCase))
                throw new IOException("저장 폴더가 변경되었습니다. 다시 선택하세요.");
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous))
            {
                ownsTemporary = true;
                await using var writer = new StreamWriter(stream, new UTF8Encoding(!html));
                await writer.WriteAsync(contents.AsMemory(), ct).ConfigureAwait(false);
                await writer.FlushAsync(ct).ConfigureAwait(false);
            }
            ct.ThrowIfCancellationRequested();
            SourceFileEnumerator.RejectReparseAncestors(parent);
            if (!SourceFileEnumerator.PhysicalDirectoryPath(parent).Equals(physicalParent, StringComparison.OrdinalIgnoreCase))
                throw new IOException("저장 폴더가 변경되었습니다. 다시 선택하세요.");
            // Replace a directory entry, never open/truncate the destination:
            // an existing hardlink or symlink cannot modify its source target.
            File.Move(temporary, destination, overwrite: true);
            ownsTemporary = false;
        }
        finally
        {
            if (ownsTemporary)
                File.Delete(temporary);
        }
    }

    private static bool Contains(string root, string path)
    {
        root = Path.TrimEndingDirectorySeparator(root);
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.Equals(root, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Name, string Value)> Summary(ScanResult result)
    {
        yield return ("검사할 폴더", result.Request.SourceDirectory);
        yield return ("목표 PHP 버전", result.Request.TargetPhpVersion);
        yield return ("상태", result.Status.ToString());
        yield return ("결과 의미", result.Status switch
        {
            ScanStatus.Failed => result.Issues.Count > 0 ? "검사 실패·확인된 일부 결과" : "검사 실패·완료하지 못함",
            ScanStatus.NoFiles => "검사 대상 없음·호환성 통과를 뜻하지 않음",
            _ => result.Issues.Count == 0 ? "정적 검사에서 문제를 발견하지 못함" : "정적 검사 결과"
        });
        yield return ("하위 폴더 포함", result.Request.IncludeSubdirectories ? "True" : "False");
        yield return ("선택 파일", Number(result.SelectedFileCount));
        yield return ("처리 파일", result.ProcessedFileCount is int processed ? Number(processed) : "미확인");
        yield return ("오류 (ERROR)", Number(result.ErrorCount));
        yield return ("경고 (WARNING)", Number(result.WarningCount));
        if (result.EngineInfo is { } engine)
        {
            yield return ("엔진 팩", engine.PackId);
            yield return ("실행 PHP", engine.PhpRuntimeVersion);
            yield return ("PHPCompatibility", engine.PhpCompatibilityVersion);
            yield return ("PHP_CodeSniffer", engine.PhpCodeSnifferVersion);
            yield return ("PHPCSUtils", engine.PhpcsUtilsVersion);
        }
        if (!string.IsNullOrEmpty(result.FailureMessage)) yield return ("실패 이유", result.FailureMessage);
        if (result.EngineExitCode is int code) yield return ("검사 엔진 종료 코드", Number(code));
        if (!string.IsNullOrEmpty(result.EngineStandardError)) yield return ("검사 엔진 상세 오류", result.EngineStandardError);
        if (!string.IsNullOrEmpty(result.EngineStandardOutputPreview)) yield return ("검사 엔진 표준 출력 일부", result.EngineStandardOutputPreview);
    }

    private static IEnumerable<string> Limitations(IReadOnlyList<string> limitations) =>
        new[] { AnalysisLimit }.Concat(limitations).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal);

    private static string Csv(ScanResult result, IReadOnlyList<string> limitations)
    {
        var output = new StringBuilder();
        void Row(params string[] values) => output.AppendJoin(',', values.Select(CsvCell)).Append("\r\n");
        Row("구분", "항목", "값");
        foreach (var row in Summary(result)) Row("요약", row.Name, row.Value);
        foreach (var path in result.Request.ExcludedPaths ?? Array.Empty<string>()) Row("지정 제외", "경로", path);
        foreach (var path in result.ExcludedPaths) Row("실제 제외", "경로", path);
        foreach (var limit in Limitations(limitations)) Row("한계", "안내", limit);
        Row("CSV 안내", "텍스트 보호", CsvTextNotice);
        Row();
        Row("파일", "줄", "열", "구분", "심각도", "규칙", "문제 설명", "fixable");
        foreach (var issue in result.Issues)
            Row(issue.FilePath, Number(issue.Line), Number(issue.Column), issue.Type, Number(issue.Severity),
                issue.Source, issue.Message, issue.Fixable ? "True" : "False");
        return output.ToString();
    }

    private static string CsvCell(string value)
    {
        var first = value.AsSpan().TrimStart();
        if (value.StartsWith('\'') || first.Length > 0 && first[0] is '=' or '+' or '-' or '@')
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string Html(ScanResult result, IReadOnlyList<string> limitations)
    {
        static string Encode(string value) => WebUtility.HtmlEncode(value);
        var output = new StringBuilder("<!doctype html>\n<html lang=\"ko\"><head><meta charset=\"utf-8\"><title>PHP 소스 호환성 검사 결과</title><style>body{font-family:Segoe UI,sans-serif;margin:2rem;color:#111;background:#fff}table{border-collapse:collapse;width:100%;margin-bottom:1rem}th,td{border:1px solid #aaa;padding:.4rem;text-align:left;vertical-align:top;white-space:pre-wrap;overflow-wrap:anywhere}th{background:#eee}h1{font-size:1.5rem}li{white-space:pre-wrap;overflow-wrap:anywhere}</style></head><body><h1>PHP 소스 호환성 검사 결과</h1><table><thead><tr><th>항목</th><th>값</th></tr></thead><tbody>");
        foreach (var row in Summary(result)) output.Append("<tr><th>").Append(Encode(row.Name)).Append("</th><td>").Append(Encode(row.Value)).Append("</td></tr>\n");
        output.Append("</tbody></table>");
        void List(string title, IEnumerable<string> values)
        {
            output.Append("<h2>").Append(Encode(title)).Append("</h2><ul>");
            foreach (var value in values) output.Append("<li>").Append(Encode(value)).Append("</li>");
            output.Append("</ul>");
        }
        List("지정한 제외 경로", result.Request.ExcludedPaths ?? Array.Empty<string>());
        List("실제 제외한 경로", result.ExcludedPaths);
        List("정적 분석의 한계", Limitations(limitations));
        output.Append("<h2>발견한 문제</h2><table><thead><tr><th>파일</th><th>줄</th><th>열</th><th>구분</th><th>심각도</th><th>규칙</th><th>문제 설명</th><th>fixable</th></tr></thead><tbody>");
        foreach (var issue in result.Issues)
        {
            output.Append("<tr>");
            foreach (var value in new[] { issue.FilePath, Number(issue.Line), Number(issue.Column), issue.Type,
                         Number(issue.Severity), issue.Source, issue.Message, issue.Fixable ? "True" : "False" })
                output.Append("<td>").Append(Encode(value)).Append("</td>");
            output.Append("</tr>\n");
        }
        return output.Append("</tbody></table></body></html>\n").ToString();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
