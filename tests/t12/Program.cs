using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

internal static class Program
{
    private static readonly List<string> Passed = [];
    private static readonly List<object> Failures = [];
    private static string Area = "";
    private static string Source = "";
    private static string Destination = "";
    private static string Output = "";
    private static readonly string[] Limits = ["T12 검증용 합성 결과 — 실제 검사 결과 아님", "엔진 사전 공개판·일부 기능 미탐지", "정적 분석은 실제 실행을 보장하지 않습니다."];

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || !OperatingSystem.IsWindows()
            || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Console.Error.WriteLine("Actual Windows x64 required: <repoRoot>");
            return 2;
        }
        var repo = Path.GetFullPath(args[0]);
        var output = Path.Combine(repo, "artifacts/t12");
        Output = output;
        Directory.CreateDirectory(output);
        Area = Path.Combine(Path.GetTempPath(), "dk-t12-export-" + Guid.NewGuid().ToString("N"));
        Source = Path.Combine(Area, "한글 검사 원본");
        Destination = Path.Combine(Area, "보고서 출력");
        Directory.CreateDirectory(Source);
        Directory.CreateDirectory(Path.Combine(Source, "empty-directory"));
        Directory.CreateDirectory(Destination);
        File.WriteAllText(Path.Combine(Source, "owned.php"), "<?php echo 'OWNED_FIXTURE';\n", new UTF8Encoding(false));
        try
        {
            await RenderCases();
            await SaveCases();
        }
        catch (Exception ex) { Failures.Add(new { name = "verification orchestration", error = ex.ToString() }); }
        finally
        {
            try { Directory.Delete(Area, recursive: true); }
            catch (Exception ex) { Failures.Add(new { name = "owned fixture cleanup", error = ex.ToString() }); }
        }
        var summary = new
        {
            environment = new { os = Environment.OSVersion.VersionString, architecture = RuntimeInformation.ProcessArchitecture.ToString(), sdk = SdkVersion(repo), runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"), sourceSha = Environment.GetEnvironmentVariable("GITHUB_SHA") },
            passed = Passed, failures = Failures, scopePassed = Failures.Count == 0,
            scope = "CSV/HTML render and actual Windows filesystem export integrity with owned synthetic scan results; no inspected PHP execution or Windows11 deployment claim",
            limits = new[] { "Native SaveFileDialog and current GUI result availability are verified separately", "CSV UTF8 BOM and RFC parsing tested; the desktop Excel application was not executed", "No PHP engine or supported target changes" }
        };
        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(output, "summary.json"), json + "\n", new UTF8Encoding(false));
        Console.WriteLine("T12_SUMMARY_JSON=" + JsonSerializer.Serialize(summary));
        return Failures.Count == 0 ? 0 : 1;
    }

    private static ScanResult Result(ScanStatus status = ScanStatus.Failed)
    {
        var issue = new ScanIssue(Path.Combine(Source, "한국어, \"인용\".php"), 123, 7, "WARNING", 5,
            "Example.Rule.Raw", "한국어, 첫 줄\r\n\"인용\" <script>alert('issue')</script> & 끝", true);
        return new ScanResult(new ScanRequest(Source, "8.4", true, [Path.Combine(Source, "configured-only")]),
            status, 42, status == ScanStatus.Failed ? null : 42, 0, 1, [issue],
            new ScanEngineInfo("owned-export-pack", "8.4.27", "10.0.0-alpha2", "4.0.4", "1.2.3"),
            status == ScanStatus.Failed ? "실패 이유, \"원문\"\n<failure>" : null)
        {
            ExcludedPaths = [Path.Combine(Source, "encountered-only")], EngineExitCode = status == ScanStatus.Failed ? 255 : status == ScanStatus.NoFiles ? null : 1,
            EngineStandardError = status == ScanStatus.Failed ? "stderr <tag> & \"quote\"" : null,
            EngineStandardOutputPreview = status == ScanStatus.Failed ? "stdout\n미리 보기 <script>alert('output')</script>" : null
        };
    }

    private static async Task RenderCases()
    {
        await Case("CSV RFC roundtrip preserves all issue fields including quotes commas Korean and embedded newline", () =>
        {
            var result = Result(); var csv = ScanResultExporter.RenderCsv(result, Limits); var rows = ParseCsv(csv);
            var header = rows.Single(r => r.SequenceEqual(new[] { "파일", "줄", "열", "구분", "심각도", "규칙", "문제 설명", "fixable" }));
            Check(header.Length == 8, "Issue column coverage changed");
            var issue = result.Issues[0]; var row = rows.Single(r => r.Length == 8 && r[0] == issue.FilePath);
            Check(row.SequenceEqual(new[] { issue.FilePath, "123", "7", "WARNING", "5", issue.Source, issue.Message, "True" }), "CSV parsing changed raw issue fields");
            Check(csv.EndsWith("\r\n", StringComparison.Ordinal), "CSV record terminator is not CRLF");
        });
        await Case("CSV metadata distinguishes configured actual exclusions and includes engine diagnostics and limits", () =>
        {
            var result = Result(); var rows = ParseCsv(ScanResultExporter.RenderCsv(result, Limits));
            Check(Meta(rows, "검사할 폴더") == Source && Meta(rows, "목표 PHP 버전") == "8.4", "Missing scan context");
            Check(Meta(rows, "엔진 팩") == "owned-export-pack" && Meta(rows, "실행 PHP") == "8.4.27"
                && Meta(rows, "PHPCompatibility") == "10.0.0-alpha2" && Meta(rows, "PHP_CodeSniffer") == "4.0.4"
                && Meta(rows, "PHPCSUtils") == "1.2.3", "Engine metadata lost");
            Check(Meta(rows, "검사 엔진 종료 코드") == "255" && Meta(rows, "검사 엔진 상세 오류") == result.EngineStandardError
                && Meta(rows, "검사 엔진 표준 출력 일부") == result.EngineStandardOutputPreview && Meta(rows, "실패 이유") == result.FailureMessage,
                "Failure diagnostics lost");
            Check(rows.Any(r => r.SequenceEqual(new[] { "지정 제외", "경로", result.Request.ExcludedPaths![0] }))
                && rows.Any(r => r.SequenceEqual(new[] { "실제 제외", "경로", result.ExcludedPaths[0] })), "Exclusion meanings merged");
            Check(Limits.All(limit => rows.Any(r => r.Length == 3 && r[0] == "한계" && r[2] == limit)), "Caller limitations lost");
            Check(rows.Any(r => r.Length == 3 && r[0] == "한계" && r[2].Contains("보장하지", StringComparison.Ordinal)), "Static-analysis limit missing");
        });
        await Case("CSV neutralizes formula prefixes in every dynamic text category while retaining the original suffix", () =>
        {
            foreach (var value in new[] { "=SUM(1,1)", "  +1+1", "\t-2+3", "\r\n @SUM(1,1)", "'=SUM(1,1)", "'literal original text", "''two literal apostrophes" })
            {
                var result = Result() with
                {
                    Request = new ScanRequest(value, value, true, [value]),
                    Issues = [new ScanIssue(value, 1, 2, value, 5, value, value, false)],
                    ExcludedPaths = [value], FailureMessage = value, EngineStandardError = value, EngineStandardOutputPreview = value,
                    EngineInfo = new ScanEngineInfo(value, value, value, value, value)
                };
                var rows = ParseCsv(ScanResultExporter.RenderCsv(result, [value])); var cells = rows.SelectMany(r => r).ToArray();
                Check(!cells.Contains(value, StringComparer.Ordinal) && cells.Count(c => c == "'" + value) >= 15,
                    "Formula text was dropped or unprotected: " + JsonSerializer.Serialize(value));
                Check(cells.Any(c => c.Contains("작은따옴표", StringComparison.Ordinal)), "Formula-neutralization notice absent");
            }
        });
        await Case("standalone HTML encodes hostile dynamic text and contains all raw issue and summary data", () =>
        {
            var hostile = "<script>alert(\"owned\")</script><img src=x onerror=alert(1)> & ' \"";
            var result = Result() with { FailureMessage = hostile, EngineStandardError = hostile,
                Issues = [Result().Issues[0] with { Message = hostile, Source = hostile }], ExcludedPaths = [hostile] };
            var html = ScanResultExporter.RenderHtml(result, [hostile]);
            Check(html.Contains("<meta charset=\"utf-8\">", StringComparison.OrdinalIgnoreCase), "HTML encoding metadata missing");
            Check(html.Contains(WebUtility.HtmlEncode(hostile), StringComparison.Ordinal), "Dynamic HTML content not encoded");
            Check(!Regex.IsMatch(html, @"<\s*(script|img|iframe)\b", RegexOptions.IgnoreCase), "Dynamic markup became executable content");
            Check(!Regex.IsMatch(html, "<(link|script)[^>]+(src|href)=", RegexOptions.IgnoreCase), "Report depends on external resources");
            var decoded = WebUtility.HtmlDecode(html);
            Check(new[] { Source, "8.4", "owned-export-pack", "8.4.27", "10.0.0-alpha2", "4.0.4", "1.2.3", "123", "7", "WARNING", "True", hostile }
                .All(value => decoded.Contains(value, StringComparison.Ordinal)), "HTML lost context or issue fields");
        });
        foreach (var status in new[] { ScanStatus.Completed, ScanStatus.NoFiles, ScanStatus.Failed })
        {
            var captured = status;
            await Case("terminal status " + status + " keeps zero findings and unknown processing distinct", () =>
            {
                var result = Result(captured) with { Issues = Array.Empty<ScanIssue>(), ErrorCount = 0, WarningCount = 0,
                    SelectedFileCount = captured == ScanStatus.NoFiles ? 0 : 42,
                    ProcessedFileCount = captured == ScanStatus.Failed ? null : captured == ScanStatus.NoFiles ? 0 : 42,
                    EngineExitCode = captured == ScanStatus.Failed ? 255 : captured == ScanStatus.NoFiles ? null : 0 };
                var rows = ParseCsv(ScanResultExporter.RenderCsv(result, Limits));
                Check(Meta(rows, "상태") == captured.ToString(), "Status was rewritten");
                Check(Meta(rows, "처리 파일") == (captured == ScanStatus.Failed ? "미확인" : captured == ScanStatus.NoFiles ? "0" : "42"), "Processing count substituted selected count");
                var html = ScanResultExporter.RenderHtml(result, Limits);
                Check(html.Contains(captured.ToString(), StringComparison.Ordinal), "HTML status absent");
                if (captured == ScanStatus.NoFiles) Check(Meta(rows, "결과 의미").Contains("통과를 뜻하지", StringComparison.Ordinal), "NoFiles overstated compatibility");
                if (captured == ScanStatus.Failed) Check(Meta(rows, "결과 의미").Contains("실패", StringComparison.Ordinal), "Failed empty scan looked compatible");
            });
        }
        await Case("failed partial result retains known issues and incomplete meaning", () =>
        {
            var rows = ParseCsv(ScanResultExporter.RenderCsv(Result(), Limits));
            Check(Meta(rows, "결과 의미").Contains("일부", StringComparison.Ordinal) && Meta(rows, "처리 파일") == "미확인"
                && rows.Any(r => r.Length == 8 && r[0] == Result().Issues[0].FilePath), "Partial failure falsely completed or discarded issues");
        });
        await Case("Idle and Running have no exportable terminal report", () =>
        {
            foreach (var status in new[] { ScanStatus.Idle, ScanStatus.Running })
            {
                Reject(() => ScanResultExporter.RenderCsv(Result(status), Limits));
                Reject(() => ScanResultExporter.RenderHtml(Result(status), Limits));
            }
        });
    }

    private static async Task SaveCases()
    {
        await Case("actual CSV save is UTF8 BOM and outside-source report preserves all original files and empty directories", async () =>
        {
            var before = Snapshot(Source); var result = Result(); var target = Path.Combine(Destination, "한글 & 결과.csv");
            await ScanResultExporter.SaveAsync(result, Limits, target, html: false);
            var bytes = File.ReadAllBytes(target);
            Check(bytes.Length > 3 && bytes.AsSpan(0, 3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }), "CSV BOM absent");
            Check(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3) == ScanResultExporter.RenderCsv(result, Limits), "Saved CSV differs from rendering");
            File.Copy(target, Path.Combine(Output, "synthetic-report.csv"), overwrite: true);
            Check(before.SequenceEqual(Snapshot(Source)), "Export modified source content or directory list");
        });
        await Case("actual HTML save matches standalone rendering and allows a sibling-prefix output folder", async () =>
        {
            var before = Snapshot(Source); var folder = Source + "-report"; Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, "report.htm"); var result = Result();
            await ScanResultExporter.SaveAsync(result, Limits, target, html: true);
            Check(File.ReadAllText(target, Encoding.UTF8) == ScanResultExporter.RenderHtml(result, Limits), "HTML save or sibling boundary failed");
            File.Copy(target, Path.Combine(Output, "synthetic-report.html"), overwrite: true);
            Check(before.SequenceEqual(Snapshot(Source)), "HTML export altered source");
        });
        await Case("source-root and source-subtree destinations are rejected without creating or overwriting files", async () =>
        {
            var existing = Path.Combine(Source, "existing.csv"); File.WriteAllText(existing, "ORIGINAL_OWNED_CSV");
            var before = Snapshot(Source);
            foreach (var target in new[] { existing, Path.Combine(Source, "new.csv"), Path.Combine(Source, "empty-directory/report.html") })
                await RejectAsync(() => ScanResultExporter.SaveAsync(Result(), Limits, target, html: target.EndsWith(".html", StringComparison.Ordinal)));
            Check(before.SequenceEqual(Snapshot(Source)), "Rejected direct export modified source");
        });
        await Case("junction output alias into source is rejected and original remains unchanged", async () =>
        {
            var link = Path.Combine(Area, "junction-output");
            var run = await Run("cmd.exe", ["/d", "/c", "mklink", "/J", link, Source]);
            Check(run.Exit == 0 && Directory.Exists(link), "Junction positive control failed: " + run.Output);
            var before = Snapshot(Source);
            try { await RejectAsync(() => ScanResultExporter.SaveAsync(Result(), Limits, Path.Combine(link, "report.csv"), false)); }
            finally { Directory.Delete(link); }
            Check(before.SequenceEqual(Snapshot(Source)), "Junction export modified source");
        });
        await Case("SUBST output alias into source is rejected by physical directory identity", async () =>
        {
            var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
            var letter = Enumerable.Range('R', 'Z' - 'R' + 1).Select(i => (char)i).First(c => !used.Contains(c));
            var drive = letter + ":"; var mapped = await Run("subst.exe", [drive, Source]);
            Check(mapped.Exit == 0 && Directory.Exists(drive + "\\"), "SUBST positive control failed: " + mapped.Output);
            var before = Snapshot(Source);
            try { await RejectAsync(() => ScanResultExporter.SaveAsync(Result(), Limits, drive + "\\report.csv", false)); }
            finally { var removed = await Run("subst.exe", [drive, "/D"]); Check(removed.Exit == 0, "SUBST cleanup failed"); }
            Check(before.SequenceEqual(Snapshot(Source)), "SUBST export modified source");
        });
        await Case("atomic overwrite of an external hardlink never truncates its inspected source target", async () =>
        {
            var original = Path.Combine(Source, "owned.php"); var target = Path.Combine(Destination, "hardlink.csv");
            Check(CreateHardLinkW(target, original, IntPtr.Zero), "Hardlink positive control failed: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
            Check(File.ReadAllBytes(original).SequenceEqual(File.ReadAllBytes(target)), "Hardlink initial data mismatch");
            var before = Snapshot(Source);
            await ScanResultExporter.SaveAsync(Result(), Limits, target, false);
            Check(before.SequenceEqual(Snapshot(Source)), "Hardlink export truncated original PHP");
            Check(File.ReadAllText(target, Encoding.UTF8).Contains("owned-export-pack", StringComparison.Ordinal), "Hardlink directory entry not replaced with report");
        });
        await Case("pre-cancelled save preserves existing output and leaves no temporary files", async () =>
        {
            var target = Path.Combine(Destination, "cancelled.csv"); File.WriteAllText(target, "KEEP_CANCELLED");
            var before = Snapshot(Destination); using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await RejectAsync(() => ScanResultExporter.SaveAsync(Result(), Limits, target, false, cancel.Token));
            Check(before.SequenceEqual(Snapshot(Destination)), "Cancelled save modified report or left temporary files");
        });
        await Case("invalid status or extension preserves existing output and creates no temporary files", async () =>
        {
            var target = Path.Combine(Destination, "invalid.csv"); File.WriteAllText(target, "KEEP_INVALID"); var before = Snapshot(Destination);
            await RejectAsync(() => ScanResultExporter.SaveAsync(Result(ScanStatus.Running), Limits, target, false));
            await RejectAsync(() => ScanResultExporter.SaveAsync(Result(), Limits, Path.Combine(Destination, "invalid.php"), false));
            Check(before.SequenceEqual(Snapshot(Destination)), "Invalid save changed existing files");
        });
        await Case("failed overwrite of a locked report preserves old output and cleans owned temporary files", async () =>
        {
            var target = Path.Combine(Destination, "locked.csv"); File.WriteAllText(target, "KEEP_LOCKED"); var before = Snapshot(Destination);
            using (var locked = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                await RejectAsync(() => ScanResultExporter.SaveAsync(Result(), Limits, target, false));
            Check(before.SequenceEqual(Snapshot(Destination)), "Failed atomic rename changed old output or left temporary file");
        });
        await Case("SaveAsync snapshots mutable issue exclusion and limitation collections before returning", async () =>
        {
            var issues = Result().Issues.ToList(); var configured = new List<string> { "configured-before" };
            var excluded = new List<string> { "encountered-before" }; var limits = new List<string> { "limit-before" };
            var result = Result() with { Issues = issues, Request = Result().Request with { ExcludedPaths = configured }, ExcludedPaths = excluded };
            var expected = ScanResultExporter.RenderCsv(result, limits); var target = Path.Combine(Destination, "snapshot.csv");
            var pending = ScanResultExporter.SaveAsync(result, limits, target, false);
            issues.Clear(); configured.Clear(); configured.Add("configured-after"); excluded.Clear(); limits.Clear(); limits.Add("limit-after");
            await pending;
            Check(File.ReadAllText(target, Encoding.UTF8) == expected, "Caller mutation altered in-flight saved snapshot");
        });
    }

    private static List<string[]> ParseCsv(string csv)
    {
        var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false;
        for (int index = 0; index < csv.Length; index++)
        {
            char c = csv[index];
            if (quoted)
            {
                if (c != '"') cell.Append(c);
                else if (index + 1 < csv.Length && csv[index + 1] == '"') { cell.Append('"'); index++; }
                else quoted = false;
            }
            else if (c == '"') { Check(cell.Length == 0, "Unexpected quote outside CSV field"); quoted = true; }
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\r' || c == '\n')
            {
                Check(c == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n', "CSV record newline is not CRLF"); index++;
                row.Add(cell.ToString()); cell.Clear(); rows.Add(row.ToArray()); row.Clear();
            }
            else cell.Append(c);
        }
        Check(!quoted && row.Count == 0 && cell.Length == 0, "Unterminated CSV document");
        return rows;
    }

    private static string Meta(List<string[]> rows, string name) => rows.Single(r => r.Length == 3 && r[0] == "요약" && r[1] == name)[2];
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected export input rejection");
    }
    private static async Task RejectAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or OperationCanceledException) { return; }
        throw new InvalidOperationException("Expected save rejection");
    }
    private static Task Case(string name, Action action) => Case(name, () => { action(); return Task.CompletedTask; });
    private static async Task Case(string name, Func<Task> action)
    {
        try { await action(); Passed.Add(name); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { Failures.Add(new { name, error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static SortedDictionary<string, string> Snapshot(string root)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)) result["D:" + Path.GetRelativePath(root, dir)] = "directory";
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) result["F:" + Path.GetRelativePath(root, file)] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        return result;
    }
    private static async Task<(int Exit, string Output)> Run(string executable, string[] args)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Area, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start owned test helper");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
        return (process.ExitCode, await stdout + await stderr);
    }
    private static string SdkVersion(string repo)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", "--version") { UseShellExecute = false, RedirectStandardOutput = true, WorkingDirectory = repo });
        if (process is null) return "unknown"; string text = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit(); return text;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, IntPtr securityAttributes);
}
