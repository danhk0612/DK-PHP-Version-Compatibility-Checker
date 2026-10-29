using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

internal static class Program
{
    private static readonly List<string> Passed = [];
    private static readonly List<object> Failed = [];
    private static string Repo = "";
    private static string PackPath = "";
    private static string Area = "";
    private static EnginePack Pack = null!;
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2 || !OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Usage on actual Windows x64: <repoRoot> <runtimePack>");
            return 2;
        }
        Repo = Path.GetFullPath(args[0]);
        PackPath = Path.GetFullPath(args[1]);
        Area = Path.Combine(Path.GetTempPath(), "dk-t05-verification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Area);
        try
        {
            await Case("load prepared pack", async () =>
            {
                Pack = await new EnginePackLoader().LoadAsync(PackPath);
                Check(Pack.Targets.Count > 0 && Pack.Targets.Select(t => t.Version).Distinct().Count() == Pack.Targets.Count, "Verified target list missing/duplicate");
            });
            if (Pack is not null)
            {
                ParserCases();
                await FixtureCases();
                await ServiceCases();
                await PackCases();
                await SyntheticProcessCases();
            }
        }
        finally
        {
            try { Directory.Delete(Area, true); }
            catch (Exception ex) { Failed.Add(new { name = "managed test area cleanup", error = ex.ToString() }); }
        }
        var summary = new
        {
            environment = new { os = Environment.OSVersion.VersionString, architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(), sdk = SdkVersion(), runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"), sourceSha = Environment.GetEnvironmentVariable("GITHUB_SHA") },
            packId = Pack?.PackId,
            targets = Pack?.Targets.Select(t => t.Version),
            passed = Passed,
            failures = Failed,
            scopePassed = Failed.Count == 0,
            scope = "C# T05 pack/scan/parser regression on Windows; no GUI, Release, Windows 11 deployment or future PHP support claim."
        };
        var output = Path.Combine(Repo, "artifacts/t05");
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, Pretty), new UTF8Encoding(false));
        Console.WriteLine("T05_SUMMARY_JSON=" + JsonSerializer.Serialize(summary));
        return Failed.Count == 0 ? 0 : 1;
    }

    private static async Task Case(string name, Func<Task> body)
    {
        try { await body(); Passed.Add(name); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { Failed.Add(new { name, error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Pure(string name, Action body) => Case(name, () => { body(); return Task.CompletedTask; }).GetAwaiter().GetResult();
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string SdkVersion()
    {
        using var p = Process.Start(new ProcessStartInfo("dotnet", "--version") { RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = Repo });
        if (p is null) return "unavailable";
        var text = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); return text;
    }
    private static string NewProject(string name)
    {
        var p = Path.Combine(Area, name); Directory.CreateDirectory(p); return p;
    }
    private static string Subject(string project, string name, string text = "<?php echo 'safe';\n")
    {
        var p = Path.Combine(project, name); Directory.CreateDirectory(Path.GetDirectoryName(p)!); File.WriteAllText(p, text, new UTF8Encoding(false)); return p;
    }
    private static SortedDictionary<string, string> Snapshot(string root)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            foreach (var p in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                var relative = Path.GetRelativePath(root, p);
                var attr = File.GetAttributes(p);
                if ((attr & FileAttributes.ReparsePoint) != 0) { result[relative] = "reparse:" + new FileInfo(p).LinkTarget; continue; }
                if ((attr & FileAttributes.Directory) != 0) { result[relative] = "directory"; pending.Push(p); }
                else result[relative] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
            }
        }
        return result;
    }
    private static void Unchanged(SortedDictionary<string, string> before, string root) =>
        Check(before.SequenceEqual(Snapshot(root)), "Source file/directory list or SHA-256 changed");
    private static async Task<ScanResult> Scan(string project, string? version = null, bool recursive = true, string? pack = null)
    {
        await using var service = new ScanService(pack ?? PackPath);
        return await service.ScanAsync(new ScanRequest(project, version ?? Pack.Targets[0].Version, recursive));
    }

    private static JsonObject Message(string type = "ERROR", string source = "Rule.One", bool fixable = false) => new()
    {
        ["message"] = "Original 한국어 message", ["source"] = source, ["severity"] = 5,
        ["fixable"] = fixable, ["type"] = type, ["line"] = 2, ["column"] = 3
    };
    private static JsonObject Report(params (string Path, JsonObject[] Messages)[] entries)
    {
        var files = new JsonObject(); int errors = 0, warnings = 0, fixable = 0;
        foreach (var entry in entries)
        {
            int fe = entry.Messages.Count(m => m["type"]!.GetValue<string>() == "ERROR");
            int fw = entry.Messages.Count(m => m["type"]!.GetValue<string>() == "WARNING");
            fixable += entry.Messages.Count(m => m["fixable"]!.GetValue<bool>());
            errors += fe; warnings += fw;
            files[entry.Path] = new JsonObject { ["errors"] = fe, ["warnings"] = fw, ["messages"] = new JsonArray(entry.Messages.Select(m => (JsonNode)m.DeepClone()).ToArray()) };
        }
        return new JsonObject { ["totals"] = new JsonObject { ["errors"] = errors, ["warnings"] = warnings, ["fixable"] = fixable }, ["files"] = files };
    }
    private static void ParserCases()
    {
        var project = NewProject("parser"); var file = Subject(project, "sample.php"); var other = Subject(project, "other.inc");
        var request = new ScanRequest(project, Pack.Targets[0].Version, true);
        var parser = new PhpcsReportParser();
        ScanResult Parse(string json, int exit = 0, string stderr = "", params string[] selected) =>
            parser.Parse(request, selected.Length == 0 ? new[] { file } : selected, Pack.EngineInfo, exit, json, stderr);
        Pure("parser valid zero findings", () =>
        {
            var r = Parse(Report((file, [])).ToJsonString());
            Check(r.Status == ScanStatus.Completed && r.ProcessedFileCount == 1 && r.ErrorCount == 0 && r.WarningCount == 0, "Zero finding report incorrect");
        });
        Pure("parser preserves same-line independent findings and raw fields", () =>
        {
            var r = Parse(Report((file, [Message(), Message("WARNING", "Rule.Two")])).ToJsonString(), 2);
            Check(r.Status == ScanStatus.Completed && r.Issues.Count == 2 && r.ErrorCount == 1 && r.WarningCount == 1, "Findings collapsed or counts wrong");
            Check(r.Issues[0].Line == 2 && r.Issues[0].Column == 3 && r.Issues[0].Severity == 5 && r.Issues[0].Source == "Rule.One" && r.Issues[0].Message == "Original 한국어 message" && !r.Issues[0].Fixable, "Raw fields changed");
        });
        Pure("parser accepts fixable exit 1 and mixed exit 3", () =>
        {
            Check(Parse(Report((file, [Message(fixable: true)])).ToJsonString(), 1).Status == ScanStatus.Completed, "Fixable exit 1 rejected");
            Check(Parse(Report((file, [Message(fixable: true), Message("WARNING", "Rule.Two")])).ToJsonString(), 3).Status == ScanStatus.Completed, "Mixed exit 3 rejected");
        });
        Pure("parser rejects polluted and malformed JSON with unknown processed count", () =>
        {
            foreach (var text in new[] { "pollution\n" + Report((file, [])).ToJsonString(), "{", "null", "[]" })
            { var r = Parse(text); Check(r.Status == ScanStatus.Failed && r.ProcessedFileCount is null, "Malformed JSON looked successful"); }
        });
        Pure("parser rejects duplicate JSON keys", () =>
        {
            var text = Report((file, [])).ToJsonString().Replace("\"totals\":", "\"totals\":{},\"totals\":", StringComparison.Ordinal);
            Check(Parse(text).Status == ScanStatus.Failed, "Duplicate fields accepted");
        });
        Pure("parser rejects type and negative-location corruption", () =>
        {
            foreach (var change in new Action<JsonObject>[] { m => m["line"] = -1, m => m["severity"] = "5", m => m["type"] = "NOTICE", m => m["fixable"] = "false" })
            { var m = Message(); change(m); Check(Parse(Report((file, [m])).ToJsonString(), 2).Status == ScanStatus.Failed, "Corrupt row accepted"); }
        });
        Pure("parser rejects file and overall totals mismatch", () =>
        {
            var r = Report((file, [Message()])); r["totals"]!["errors"] = 0;
            Check(Parse(r.ToJsonString(), 2).Status == ScanStatus.Failed, "Overall count mismatch accepted");
            r = Report((file, [Message()])); r["files"]![file]!["errors"] = 0;
            Check(Parse(r.ToJsonString(), 2).Status == ScanStatus.Failed, "File count mismatch accepted");
        });
        Pure("parser rejects contradictory exits and stderr diagnostics", () =>
        {
            Check(Parse(Report((file, [Message()])).ToJsonString(), 0).Status == ScanStatus.Failed, "Exit 0 with findings accepted");
            Check(Parse(Report((file, [])).ToJsonString(), 2).Status == ScanStatus.Failed, "Exit 2 without findings accepted");
            Check(Parse(Report((file, [])).ToJsonString(), 0, "PHP diagnostic").Status == ScanStatus.Failed, "stderr suppressed");
        });
        Pure("parser keeps valid partial findings as failure", () =>
        {
            var r = Parse(Report((file, [Message()])).ToJsonString(), 2, "", file, other);
            Check(r.Status == ScanStatus.Failed && r.Issues.Count == 1 && r.ProcessedFileCount == 1, "Partial findings/count not retained");
            r = Parse(Report((file, [Message()])).ToJsonString(), 255);
            Check(r.Status == ScanStatus.Failed && r.Issues.Count == 1, "Execution failure lost valid findings");
        });
        Pure("parser rejects unexpected reported files", () =>
        {
            Check(Parse(Report((other, [])).ToJsonString()).Status == ScanStatus.Failed, "Unselected file accepted");
        });
    }

    private static async Task FixtureCases()
    {
        using var definitions = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo, "tests/fixtures/cases.json")));
        foreach (var definition in definitions.RootElement.GetProperty("cases").EnumerateArray())
        {
            string relative = definition.GetProperty("file").GetString()!;
            foreach (var target in Pack.Targets)
            {
                var expectation = definition.GetProperty("targets").GetProperty(target.Version);
                await Case("actual fixture " + Path.GetFileName(relative) + " @ " + target.Version, async () =>
                {
                    var project = NewProject("fixture-" + Guid.NewGuid().ToString("N"));
                    var destination = Path.Combine(project, Path.GetFileName(relative));
                    File.Copy(Path.Combine(Repo, "tests/fixtures", relative.Replace('/', Path.DirectorySeparatorChar)), destination);
                    var before = Snapshot(project);
                    var result = await Scan(project, target.Version);
                    Check(result.Status == ScanStatus.Completed && result.SelectedFileCount == 1 && result.ProcessedFileCount == 1, result.FailureMessage ?? "Actual fixture did not complete");
                    string kind = expectation.GetProperty("expectation").GetString()!;
                    if (kind == "finding")
                    {
                        string source = expectation.GetProperty("source_prefix").GetString()!; int line = expectation.GetProperty("line").GetInt32();
                        Check(result.Issues.Any(m => m.Source.StartsWith(source, StringComparison.Ordinal) && m.Line == line), "Expected finding absent");
                    }
                    else if (kind == "no_findings") Check(result.Issues.Count == 0 && result.ErrorCount == 0 && result.WarningCount == 0, "Unexpected control finding");
                    Unchanged(before, project);
                });
            }
        }
    }

    private static async Task ServiceCases()
    {
        await Case("actual empty folder is NoFiles", async () =>
        {
            var p = NewProject("empty"); var before = Snapshot(p); var r = await Scan(p);
            Check(r.Status == ScanStatus.NoFiles && r.SelectedFileCount == 0 && r.Issues.Count == 0, "Empty folder looked compatible/completed"); Unchanged(before, p);
        });
        await Case("unsupported target rejected without modifying source", async () =>
        {
            var p = NewProject("unsupported"); Subject(p, "a.php"); var before = Snapshot(p);
            Check((await Scan(p, "99.0")).Status == ScanStatus.Failed, "Unverified target accepted"); Unchanged(before, p);
        });
        await Case("extension and recursive selection are explicit", async () =>
        {
            var p = NewProject("recursive"); Subject(p, "a.PHP"); Subject(p, "b.InC"); Subject(p, "ignore.txt", "not PHP"); Subject(p, "nested/c.php");
            Directory.CreateDirectory(Path.Combine(p, "empty-dir")); var before = Snapshot(p);
            var local = await Scan(p, recursive: false); var all = await Scan(p);
            Check(local.Status == ScanStatus.Completed && local.SelectedFileCount == 2 && local.ProcessedFileCount == 2, "Local extension selection incorrect");
            Check(all.Status == ScanStatus.Completed && all.SelectedFileCount == 3 && all.ProcessedFileCount == 3, "Recursive selection incorrect"); Unchanged(before, p);
        });
        await Case("source annotations cannot suppress selected findings", async () =>
        {
            var p = NewProject("annotations");
            foreach (var f in Directory.GetFiles(Path.Combine(Repo, "tests/t04/annotations"))) File.Copy(f, Path.Combine(p, Path.GetFileName(f)));
            var before = Snapshot(p); var r = await Scan(p);
            Check(r.Status == ScanStatus.Completed && r.SelectedFileCount == 3 && r.ProcessedFileCount == 3, r.FailureMessage ?? "Annotation scan incomplete");
            Check(r.Issues.Select(x => Path.GetFileName(x.FilePath)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3, "Annotation hid a finding"); Unchanged(before, p);
        });
        await Case("Korean spaces metacharacters and hostile project settings remain inert", async () =>
        {
            var p = NewProject("한글 space & project");
            Subject(p, "subject.php"); Subject(p, "bootstrap.php", "<?php throw new Exception('TARGET_EXECUTED');\n");
            Subject(p, "vendor/autoload.php", "<?php throw new Exception('AUTOLOAD_EXECUTED');\n");
            File.WriteAllText(Path.Combine(p, "phpcs.xml"), "<ruleset name=\"poison\"><bootstrap>bootstrap.php</bootstrap><rule ref=\"DoesNotExist\"/></ruleset>");
            File.WriteAllText(Path.Combine(p, "composer.json"), "{\"scripts\":{\"pre-install-cmd\":\"@php bootstrap.php\"}}");
            File.WriteAllText(Path.Combine(p, "php.ini"), "auto_prepend_file=bootstrap.php\n");
            var outside = NewProject("external-ini"); string poison = Subject(outside, "poison.php", "<?php throw new Exception('INI_EXECUTED');\n");
            string ini = Path.Combine(outside, "php.ini"); File.WriteAllText(ini, "auto_prepend_file=\"" + poison.Replace('\\', '/') + "\"\n", new UTF8Encoding(false));
            var oldPhprc = Environment.GetEnvironmentVariable("PHPRC"); var oldScan = Environment.GetEnvironmentVariable("PHP_INI_SCAN_DIR");
            var before = Snapshot(p);
            try
            {
                var control = await Run(Pack.PhpExecutable, ["-c", ini, Subject(outside, "control.php")], outside);
                Check(control.Exit != 0 && (control.Out + control.Error).Contains("INI_EXECUTED", StringComparison.Ordinal), "External ini positive control failed");
                Environment.SetEnvironmentVariable("PHPRC", ini); Environment.SetEnvironmentVariable("PHP_INI_SCAN_DIR", outside);
                var r = await Scan(p);
                Check(r.Status == ScanStatus.Completed && r.SelectedFileCount == 3 && r.ProcessedFileCount == 3, r.FailureMessage ?? "Ambient/project settings affected scan"); Unchanged(before, p);
            }
            finally { Environment.SetEnvironmentVariable("PHPRC", oldPhprc); Environment.SetEnvironmentVariable("PHP_INI_SCAN_DIR", oldScan); }
        });
        await Case("junction and file symlink excluded; linked root and ancestor rejected", async () =>
        {
            var p = NewProject("links"); Subject(p, "inside.php"); var outside = NewProject("link-outside"); Subject(outside, "outside.php", "<?php mysql_query('bad');\n");
            string junction = Path.Combine(p, "junction"); string linkedFile = Path.Combine(p, "linked.php");
            var mk = await Run("cmd.exe", ["/d", "/c", "mklink", "/J", junction, outside], Area); Check(mk.Exit == 0, "Junction creation failed: " + mk.Error);
            File.CreateSymbolicLink(linkedFile, Path.Combine(outside, "outside.php"));
            try
            {
                var before = Snapshot(p); var r = await Scan(p);
                Check(r.Status == ScanStatus.Completed && r.SelectedFileCount == 1 && r.ProcessedFileCount == 1 && r.Issues.Count == 0 && r.ExcludedPaths.Count >= 2, "Links were followed or exclusions hidden");
                Check((await Scan(junction)).Status == ScanStatus.Failed, "Linked root accepted");
                var child = Path.Combine(outside, "nested"); Directory.CreateDirectory(child); Subject(child, "a.php");
                Check((await Scan(Path.Combine(junction, "nested"))).Status == ScanStatus.Failed, "Linked ancestor accepted"); Unchanged(before, p);
            }
            finally { File.Delete(linkedFile); Directory.Delete(junction); }
        });
        await Case("read-denied source is Failed and ACL restored", async () =>
        {
            var p = NewProject("read-denied"); string f = Subject(p, "denied.php"); var before = Snapshot(p);
            string user = Environment.UserDomainName + "\\" + Environment.UserName;
            var denied = await Run("icacls.exe", [f, "/deny", user + ":(R)"], Area); Check(denied.Exit == 0, "Could not deny input read");
            try
            {
                bool actuallyDenied = false; try { _ = File.ReadAllBytes(f); } catch (UnauthorizedAccessException) { actuallyDenied = true; }
                Check(actuallyDenied, "ACL positive control did not deny read");
                var r = await Scan(p); Check(r.Status == ScanStatus.Failed && r.ProcessedFileCount is null, "Unreadable source looked complete");
            }
            finally { var restored = await Run("icacls.exe", [f, "/remove:d", user], Area); Check(restored.Exit == 0, "ACL restore failed"); }
            Unchanged(before, p);
        });
        await Case("SUBST alias of managed temp fixture is rejected before creating scan work", async () =>
        {
            var used = DriveInfo.GetDrives().Select(d => d.Name[..2]).ToHashSet(StringComparer.OrdinalIgnoreCase);
            string drive = Enumerable.Range('C', 'Z' - 'C' + 1).Reverse()
                .Select(c => ((char)c).ToString() + ":").First(d => !used.Contains(d));
            string physical = NewProject("subst-physical");
            Subject(physical, "subject.php", "<?php echo 'T05_SUBST_WITNESS';\n");
            string aliasRoot = drive + "\\";
            var created = await Run("subst.exe", [drive, physical], Area);
            Check(created.Exit == 0, "SUBST creation failed: " + created.Error);
            string? oldTmp = Environment.GetEnvironmentVariable("TMP");
            string? oldTemp = Environment.GetEnvironmentVariable("TEMP");
            try
            {
                Check(Directory.Exists(aliasRoot) && File.ReadAllText(Path.Combine(aliasRoot, "subject.php")) == File.ReadAllText(Path.Combine(physical, "subject.php")), "SUBST positive control did not reach managed fixture data");
                Environment.SetEnvironmentVariable("TMP", physical);
                Environment.SetEnvironmentVariable("TEMP", physical);
                Check(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())).Equals(physical, StringComparison.OrdinalIgnoreCase), "Managed TMP/TEMP override did not affect GetTempPath");
                var before = Snapshot(physical);
                var r = await Scan(aliasRoot);
                Check(r.Status == ScanStatus.Failed && r.FailureMessage?.Contains("겹", StringComparison.Ordinal) == true, "SUBST source/temp physical overlap was not rejected before enumeration");
                Unchanged(before, physical);
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
                Environment.SetEnvironmentVariable("TEMP", oldTemp);
                var removed = await Run("subst.exe", [drive, "/D"], Area);
                Check(removed.Exit == 0 && !Directory.Exists(aliasRoot), "SUBST cleanup failed: " + removed.Error);
            }
        });
        await Case("missing source and temp/source overlap rejected", async () =>
        {
            Check((await Scan(Path.Combine(Area, "does-not-exist"))).Status == ScanStatus.Failed, "Missing source accepted");
            // A scan of the temp root would contain the service work area and must not write there.
            var r = await Scan(Path.GetTempPath());
            Check(r.Status == ScanStatus.Failed && r.FailureMessage?.Contains("겹", StringComparison.Ordinal) == true, "Temp/source overlap was not rejected before enumeration");
            r = await Scan(Path.GetPathRoot(PackPath)!);
            Check(r.Status == ScanStatus.Failed && r.FailureMessage?.Contains("겹", StringComparison.Ordinal) == true, "Drive containing pack was not rejected before enumeration");
        });
    }

    private static async Task PackCases()
    {
        var copy = CopyPack("invalid-pack");
        var manifestPath = Path.Combine(copy, "manifest.json"); var original = File.ReadAllBytes(manifestPath);
        async Task RejectManifest(string name, Action<JsonObject> change)
        {
            await Case(name, async () =>
            {
                var data = JsonNode.Parse(original)!.AsObject(); change(data); File.WriteAllText(manifestPath, data.ToJsonString());
                try { await RejectedPack(copy); } finally { File.WriteAllBytes(manifestPath, original); }
            });
        }
        await RejectManifest("loader rejects unknown schema", m => m["schemaVersion"] = 2);
        await RejectManifest("loader rejects unknown report profile", m => m["reportProfile"] = "future-profile");
        await RejectManifest("loader rejects unverified target", m => m["targets"]!.AsArray().Add(new JsonObject { ["version"] = "99.0", ["verificationRef"] = "verification/results.json#targets/99.0", ["limitations"] = new JsonArray() }));
        await RejectManifest("loader rejects duplicate target", m => m["targets"]!.AsArray().Add(m["targets"]![0]!.DeepClone()));
        await RejectManifest("loader rejects traversal path", m => m["engine"]!["files"]!["../outside"] = new string('0', 64));
        await RejectManifest("loader rejects wrong lock metadata", m => m["engine"]!["lockSha256"] = new string('0', 64));
        await Case("loader rejects duplicate JSON keys", async () =>
        {
            string text = Encoding.UTF8.GetString(original).Replace("\"schemaVersion\":", "\"schemaVersion\":2,\"schemaVersion\":", StringComparison.Ordinal);
            File.WriteAllText(manifestPath, text); try { await RejectedPack(copy); } finally { File.WriteAllBytes(manifestPath, original); }
        });
        foreach (var name in new[] { "php/php.exe", "engine/ruleset.xml", "engine/composer.lock", "verification/results.json" })
        {
            await Case("loader rejects changed " + name, async () =>
            {
                string file = Path.Combine(copy, name.Replace('/', Path.DirectorySeparatorChar)); var bytes = File.ReadAllBytes(file);
                File.WriteAllBytes(file, bytes.Concat(new byte[] { 0 }).ToArray());
                try { await RejectedPack(copy); } finally { File.WriteAllBytes(file, bytes); }
            });
        }
        await Case("loader rejects added unlisted file", async () =>
        {
            string f = Subject(copy, "engine/unlisted.php"); try { await RejectedPack(copy); } finally { File.Delete(f); }
        });
        await Case("loader rejects missing file", async () =>
        {
            string f = Path.Combine(copy, "engine/ruleset.xml"); var bytes = File.ReadAllBytes(f); File.Delete(f);
            try { await RejectedPack(copy); } finally { File.WriteAllBytes(f, bytes); }
        });
        await Case("loader rejects reparse point in pack", async () =>
        {
            string link = Path.Combine(copy, "engine/link.php"); File.CreateSymbolicLink(link, Path.Combine(copy, "php/php.exe"));
            try { await RejectedPack(copy); } finally { File.Delete(link); }
        });
        await Case("loader accepts restored test pack", async () => { _ = await new EnginePackLoader().LoadAsync(copy); });
    }
    private static async Task RejectedPack(string path)
    {
        try { _ = await new EnginePackLoader().LoadAsync(path); }
        catch (InvalidDataException) { return; }
        catch (IOException) { return; }
        throw new InvalidOperationException("Invalid pack accepted");
    }
    private static string CopyPack(string name)
    {
        var copy = NewProject(name);
        foreach (var directory in Directory.EnumerateDirectories(PackPath, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(copy, Path.GetRelativePath(PackPath, directory)));
        foreach (var file in Directory.EnumerateFiles(PackPath, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(copy, Path.GetRelativePath(PackPath, file)));
        return copy;
    }
    private static void PatchEntry(string pack, string helper)
    {
        var manifestPath = Path.Combine(pack, "manifest.json"); var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        string entry = manifest["engine"]!["entryPoint"]!.GetValue<string>(); string full = Path.Combine(pack, entry.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(full, helper, new UTF8Encoding(false));
        string digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
        manifest["engine"]!["files"]![entry] = digest;
        string report = manifest["verification"]!["reportPath"]!.GetValue<string>(); string evidencePath = Path.Combine(pack, report.Replace('/', Path.DirectorySeparatorChar));
        var evidence = JsonNode.Parse(File.ReadAllText(evidencePath))!;
        evidence["engineFiles"]![entry] = digest; File.WriteAllText(evidencePath, evidence.ToJsonString(), new UTF8Encoding(false));
        manifest["verification"]!["files"]![report] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(evidencePath))).ToLowerInvariant();
        File.WriteAllText(manifestPath, manifest.ToJsonString(), new UTF8Encoding(false));
    }
    private static async Task SyntheticProcessCases()
    {
        var copy = CopyPack("synthetic-process-pack"); var project = NewProject("synthetic-subject"); Subject(project, "subject.php");
        await Case("synthetic helper drains large stdout and stderr without deadlock", async () =>
        {
            // The helper is test-owned tool code, never inspected project code or a support claim.
            PatchEntry(copy, "<?php fwrite(STDOUT,str_repeat('o',1048576)); fwrite(STDERR,str_repeat('e',1048576)); exit(255);\n");
            var before = Snapshot(project);
            await using var service = new ScanService(copy);
            var result = await service.ScanAsync(new ScanRequest(project, Pack.Targets[0].Version, true)).WaitAsync(TimeSpan.FromSeconds(25));
            Check(result.Status == ScanStatus.Failed && result.ProcessedFileCount is null, "Large invalid engine output looked successful"); Unchanged(before, project);
        });
        await Case("synthetic helper rejects concurrent scan and Dispose kills owned process", async () =>
        {
            PatchEntry(copy, "<?php file_put_contents(getenv('DK_T05_PID'),(string)getmypid()); sleep(120);\n");
            string pidFile = Path.Combine(Area, "helper.pid"); string? old = Environment.GetEnvironmentVariable("DK_T05_PID");
            var tempParent = Path.Combine(Path.GetTempPath(), "DK-PHP-Compatibility-Checker");
            var beforeTemp = Directory.Exists(tempParent) ? Directory.GetDirectories(tempParent).ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int? ownedPid = null; var before = Snapshot(project); var service = new ScanService(copy);
            try
            {
                Environment.SetEnvironmentVariable("DK_T05_PID", pidFile);
                var scan = service.ScanAsync(new ScanRequest(project, Pack.Targets[0].Version, true));
                var deadline = DateTime.UtcNow.AddSeconds(25);
                while (!File.Exists(pidFile) && DateTime.UtcNow < deadline && !scan.IsCompleted) await Task.Delay(50);
                Check(File.Exists(pidFile), "Synthetic helper did not start");
                ownedPid = int.Parse(File.ReadAllText(pidFile));
                bool rejected = false;
                try { var second = await service.ScanAsync(new ScanRequest(project, Pack.Targets[0].Version, true)); rejected = second.Status == ScanStatus.Failed; }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "Concurrent scan accepted");
                await service.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));
                var result = await scan.WaitAsync(TimeSpan.FromSeconds(5));
                Check(result.Status == ScanStatus.Failed, "Interrupted process became completed");
                bool exited;
                try { using var child = Process.GetProcessById(ownedPid.Value); exited = child.HasExited; }
                catch (ArgumentException) { exited = true; }
                Check(exited, "Owned process survived disposal");
                var afterTemp = Directory.Exists(tempParent) ? Directory.GetDirectories(tempParent).ToHashSet(StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Check(afterTemp.SetEquals(beforeTemp), "Service temporary scan folder survived disposal"); Unchanged(before, project);
            }
            finally
            {
                Environment.SetEnvironmentVariable("DK_T05_PID", old);
                if (ownedPid.HasValue)
                {
                    try { using var child = Process.GetProcessById(ownedPid.Value); if (!child.HasExited) { child.Kill(true); await child.WaitForExitAsync(); } }
                    catch (ArgumentException) { }
                }
                await service.DisposeAsync();
            }
        });
    }
    private static async Task<(int Exit, string Out, string Error)> Run(string executable, string[] args, string cwd)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = cwd, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start test helper");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); return (process.ExitCode, await stdout, await stderr);
    }
}
