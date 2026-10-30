using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dk.PhpCompatibilityChecker;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

internal static class Program
{
    private static readonly List<string> Passed = [];
    private static readonly List<object> Failures = [];
    private static readonly List<object> Screenshots = [];
    private static readonly List<object> Runs = [];
    private static readonly List<object> NativeDialogs = [];
    private static readonly List<MainWindow> OwnedWindows = [];
    private static string Repo = "", PackPath = "", Area = "", Output = "";
    private static EnginePack Pack = null!;
    private static MainWindow Window = null!;
    private static ScanPresentation Model => Window.Presentation;
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || !OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        { Console.Error.WriteLine("Actual Windows x64 required: <repoRoot> <runtimePack>"); return 2; }
        Repo = Path.GetFullPath(args[0]); PackPath = Path.GetFullPath(args[1]);
        Output = Path.Combine(Repo, "artifacts/t07");
        Area = Path.Combine(Path.GetTempPath(), "dk-t07-verification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Area); Directory.CreateDirectory(Output);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            Case("actual Windows WPF startup and verified target list", () =>
            {
                Pack = Await(new EnginePackLoader().LoadAsync(PackPath));
                Window = Open(PackPath);
                Check(IsWindowVisible(new WindowInteropHelper(Window).Handle) && Process.GetCurrentProcess().Responding, "Actual WPF HWND not responsive/visible");
                Check(Model.Targets.Select(t => t.Version).SequenceEqual(Pack.Targets.Select(t => t.Version)), "Displayed targets differ from verified pack");
                Check(!C<Button>("ScanButton").IsEnabled && Model.Issues.Count == 0, "Unprepared scan/results available");
            });
            if (Pack is not null && Window is not null)
            {
                InputsAndDialog();
                FixtureCases();
                ActualProtectionCases();
                LargeFixture();
                CloseDuringActualScan();
                CloseDuringPackLoad();
                SyntheticPartial();
            }
        }
        finally
        {
            foreach (var window in OwnedWindows)
            {
                try { if (window.IsVisible) { window.Close(); Until(() => !window.IsVisible, 30); } }
                catch (Exception ex) { Failures.Add(new { name = "owned window cleanup", error = ex.ToString() }); }
            }
            app.Shutdown();
            try { Directory.Delete(Area, true); }
            catch (Exception ex) { Failures.Add(new { name = "test area cleanup", error = ex.ToString() }); }
        }
        var summary = new
        {
            environment = new { os = Environment.OSVersion.VersionString, architecture = RuntimeInformation.ProcessArchitecture.ToString(), sdk = SdkVersion(), runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"), sourceSha = Environment.GetEnvironmentVariable("GITHUB_SHA") },
            packId = Pack?.PackId, targets = Pack?.Targets.Select(t => t.Version),
            passed = Passed, failures = Failures, runs = Runs, nativeDialogs = NativeDialogs, screenshots = Screenshots,
            scopePassed = Failures.Count == 0,
            scope = "Actual shown Windows WPF controls and ScanButton to real fixed PHPCompatibility engine, plus separately identified synthetic partial-output helper. No Windows 11 deployment claim.",
            unverified = new[] { "Clean Windows 11/CRT deployment (T09)", "Engine pack exchange/restore integration (T10)", "All real-project sizes and OS permission combinations" }
        };
        File.WriteAllText(Path.Combine(Output, "summary.json"), JsonSerializer.Serialize(summary, Pretty), new UTF8Encoding(false));
        Console.WriteLine("T07_SUMMARY_JSON=" + JsonSerializer.Serialize(summary));
        return Failures.Count == 0 ? 0 : 1;
    }

    private static MainWindow Open(string pack)
    {
        var window = new MainWindow(new ScanPresentation()) { Title = "T07 실제 엔진 · 자체 fixture 검증" };
        OwnedWindows.Add(window); window.Show(); Pump();
        Await(window.LoadPackAsync(pack));
        Check(window.Presentation.Targets.Count > 0, "Window failed to prepare pack: " + window.Presentation.FailureText);
        return window;
    }
    private static void InputsAndDialog()
    {
        Case("valid folder and verified goal both required by real button", () =>
        {
            string project = Project("input"); Subject(project, "safe.php");
            C<TextBox>("SourceFolderTextBox").Text = project; Pump();
            Check(!C<Button>("ScanButton").IsEnabled, "Goal missing but scan enabled");
            C<ComboBox>("TargetVersionComboBox").SelectedIndex = 0; Pump();
            Check(C<Button>("ScanButton").IsEnabled, "Valid input not enabled");
            C<TextBox>("SourceFolderTextBox").Text = Path.Combine(Area, "missing"); Pump();
            Check(!C<Button>("ScanButton").IsEnabled, "Missing input enabled");
            Model.SelectedTarget = new PhpTarget("99.0", "not-verified", []); Pump();
            Check(Model.SelectedTarget is null && !C<Button>("ScanButton").IsEnabled, "Unverified goal accepted");
        });
        Case("native OS folder picker selects a folder without command entry", () =>
        {
            string initial = Project("picker-initial"); string selected = Project("폴더 선택 & actual source"); Subject(selected, "safe.php");
            Model.SourceDirectory = initial; Pump();
            var owner = new WindowInteropHelper(Window).Handle;
            var worker = Task.Run(() => NativePicker.Select(owner, selected));
            C<Button>("FolderBrowseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            long hwnd = Await(worker);
            Check(Path.GetFullPath(Model.SourceDirectory).TrimEnd('\\').Equals(selected.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase), "Native selection not reflected in source textbox: " + Model.SourceDirectory);
            Check(C<TextBox>("SourceFolderTextBox").Text == Model.SourceDirectory, "Selected folder binding stale");
            NativeDialogs.Add(new { action = "select", dialogHwnd = hwnd.ToString("X", CultureInfo.InvariantCulture), selected, verifiedBy = "Native modal dialog and resulting product textbox" });
            C<ComboBox>("TargetVersionComboBox").SelectedIndex = 0; Pump();
            RunScan("native-picker-selected-folder", selected, Pack.Targets[0].Version, true);
            Check(Model.Status == ScanStatus.Completed && Model.Issues.Count == 0, Model.FailureText);
        });
        Case("native OS folder picker cancel preserves current source and results", () =>
        {
            string before = Model.SourceDirectory; string summary = Model.SummaryText; int issues = Model.Issues.Count;
            var owner = new WindowInteropHelper(Window).Handle;
            var worker = Task.Run(() => NativePicker.Cancel(owner));
            C<Button>("FolderBrowseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            long hwnd = Await(worker);
            Check(Model.SourceDirectory == before && Model.SummaryText == summary && Model.Issues.Count == issues, "Cancelled picker changed inputs/results");
            NativeDialogs.Add(new { action = "cancel", dialogHwnd = hwnd.ToString("X", CultureInfo.InvariantCulture), preservedInput = true });
        });
    }
    private static void FixtureCases()
    {
        using var definitions = JsonDocument.Parse(File.ReadAllText(Path.Combine(Repo, "tests/fixtures/cases.json")));
        foreach (var definition in definitions.RootElement.GetProperty("cases").EnumerateArray())
        {
            string relative = definition.GetProperty("file").GetString()!;
            foreach (var target in Pack.Targets)
            {
                var expectation = definition.GetProperty("targets").GetProperty(target.Version);
                Case("real GUI fixture " + Path.GetFileName(relative) + " @ " + target.Version, () =>
                {
                    string project = Project("fixture-" + Guid.NewGuid().ToString("N"));
                    File.Copy(Path.Combine(Repo, "tests/fixtures", relative.Replace('/', Path.DirectorySeparatorChar)), Path.Combine(project, Path.GetFileName(relative)));
                    RunScan("fixture/" + relative, project, target.Version, true);
                    Check(Model.Status == ScanStatus.Completed && Model.SelectedFileCountText == "1" && Model.ProcessedFileCountText == "1", Model.FailureText);
                    string kind = expectation.GetProperty("expectation").GetString()!;
                    if (kind == "finding")
                    {
                        string prefix = expectation.GetProperty("source_prefix").GetString()!; int line = expectation.GetProperty("line").GetInt32();
                        Check(Model.Issues.Any(i => i.Source.StartsWith(prefix, StringComparison.Ordinal) && i.Line == line), "Expected actual engine finding absent");
                    }
                    else if (kind == "no_findings") Check(Model.Issues.Count == 0 && Model.ErrorCountText == "0" && Model.WarningCountText == "0", "Unexpected findings");
                    if (relative.EndsWith("mysql_removed.php", StringComparison.Ordinal) && target == Pack.Targets[0]) Capture("real-engine-findings", "T07 실제 PHPCompatibility 엔진 · 자체 fixture");
                });
            }
        }
    }
    private static void ActualProtectionCases()
    {
        Case("real zero findings and empty folder have different UI meanings", () =>
        {
            string project = Project("normal-zero"); Subject(project, "safe.php");
            RunScan("normal-zero", project, Pack.Targets[0].Version, true);
            Check(Model.Status == ScanStatus.Completed && Model.SummaryText == "정적 검사에서 문제를 발견하지 못했습니다.", "Zero wording overstates compatibility");
            string empty = Project("empty");
            RunScan("empty", empty, Pack.Targets[0].Version, true);
            Check(Model.Status == ScanStatus.NoFiles && Model.SelectedFileCountText == "0" && Model.Issues.Count == 0 && Model.SummaryText.Contains("통과를 뜻하지", StringComparison.Ordinal), "Empty looked compatible");
        });
        Case("real recursive toggle selects php/inc and clears stale results", () =>
        {
            string p = Project("recursive"); Subject(p, "a.PHP"); Subject(p, "b.InC"); Subject(p, "ignored.txt", "plain text"); Subject(p, "nested/removed.php", "<?php mysql_query('bad');\n");
            RunScan("non-recursive", p, Pack.Targets[0].Version, false);
            Check(Model.SelectedFileCountText == "2" && Model.ProcessedFileCountText == "2" && Model.Issues.Count == 0, "Nonrecursive file selection wrong");
            C<CheckBox>("IncludeSubdirectoriesCheckBox").IsChecked = true; Pump();
            Check(Model.Status == ScanStatus.Idle && Model.ProcessedFileCountText == "—", "Input edit kept stale scan");
            RunScan("recursive", p, Pack.Targets[0].Version, true);
            Check(Model.SelectedFileCountText == "3" && Model.ProcessedFileCountText == "3" && Model.Issues.Count > 0, "Recursive file selection wrong");
        });
        Case("real long Korean metacharacter paths and hostile project settings remain read-only and inert", () =>
        {
            string p = Project("한국어 공백 & 프로젝트");
            Subject(p, "매우긴_하위폴더_호환성_검사_경로_공백 & 테스트/더긴_경로_문자_테스트_한국어/문제소스.php", "<?php mysql_query('bad');\n");
            string sentinel = Path.Combine(p, "SHOULD_NOT_EXIST.txt").Replace('\\', '/');
            string poison = "<?php file_put_contents('" + sentinel.Replace("'", "\\'", StringComparison.Ordinal) + "', 'EXECUTED'); throw new Exception('TARGET_EXECUTED');\n";
            Subject(p, "bootstrap.php", poison); Subject(p, "vendor/autoload.php", poison);
            Subject(p, "phpcs.xml", "<ruleset name=\"poison\"><bootstrap>bootstrap.php</bootstrap><rule ref=\"DoesNotExist\"/></ruleset>");
            Subject(p, "composer.json", "{\"scripts\":{\"pre-install-cmd\":\"@php bootstrap.php\"}}");
            Subject(p, "php.ini", "auto_prepend_file=bootstrap.php\n");
            RunScan("long-Korean-hostile-project", p, Pack.Targets[0].Version, true);
            Check(Model.Status == ScanStatus.Completed && Model.SelectedFileCountText == "3" && Model.ProcessedFileCountText == "3" && Model.Issues.Any(i => i.FilePath.Contains("문제소스.php", StringComparison.Ordinal)), Model.FailureText);
            Check(!File.Exists(Path.Combine(p, "SHOULD_NOT_EXIST.txt")), "Source/bootstrap/autoload executed");
            Capture("real-engine-Korean-path", "T07 실제 엔진 · 한국어 경로와 설정 격리 fixture");
        });
        Case("real unreadable source failure is displayed without false zero completion", () =>
        {
            string p = Project("read-denied"); string f = Subject(p, "denied.php"); var before = Snapshot(p);
            string user = Environment.UserDomainName + "\\" + Environment.UserName;
            RunTool("icacls.exe", [f, "/deny", user + ":(R)"]);
            try
            {
                bool denied = false; try { _ = File.ReadAllBytes(f); } catch (UnauthorizedAccessException) { denied = true; }
                Check(denied, "ACL positive control did not deny reading");
                RunScan("read-denied", p, Pack.Targets[0].Version, true, snapshot: false);
                Check(Model.Status == ScanStatus.Failed && Model.ProcessedFileCountText == "미확인" && Model.FailureText.Length > 0, "Unreadable file displayed successful zero");
                Capture("real-engine-failure", "T07 실제 입력 읽기 실패 · 자체 ACL fixture");
            }
            finally { RunTool("icacls.exe", [f, "/remove:d", user]); }
            Unchanged(before, p);
        });
    }
    private static void LargeFixture()
    {
        Case("real 1000-file findings match single-file control and retain every grid item", () =>
        {
            const string content = "<?php\nmysql_query('fixture');\n";
            string control = Project("large-single-file-control"); Subject(control, "control.php", content);
            RunScan("large-single-file-control", control, Pack.Targets[0].Version, true);
            Check(Model.Status == ScanStatus.Completed && Model.SelectedFileCountText == "1" && Model.ProcessedFileCountText == "1", Model.FailureText);
            Check(Model.Issues.Any(i => i.Source.StartsWith("PHPCompatibility.FunctionUse.RemovedFunctions.mysql_query", StringComparison.Ordinal) && i.Line == 2), "Single-file control did not detect the expected removed function at line 2");
            static string Signature(ScanIssue issue) => JsonSerializer.Serialize(issue with { FilePath = "" });
            string[] baseline = Model.Issues.Select(Signature).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            int expectedTotal = checked(baseline.Length * 1000);
            string p = Project("large-1000");
            for (int i = 0; i < 1000; i++) Subject(p, $"group-{i / 100:D2}/source-{i:D4}.php", content);
            RunScan("large-1000", p, Pack.Targets[0].Version, true, maxSeconds: 180);
            Check(Model.Status == ScanStatus.Completed && Model.SelectedFileCountText == "1000" && Model.ProcessedFileCountText == "1000", Model.FailureText);
            var perFile = Model.Issues.GroupBy(i => i.FilePath, StringComparer.OrdinalIgnoreCase).ToArray();
            Check(Model.Issues.Count == expectedTotal && perFile.Length == 1000, "Large result findings omitted or added relative to the single-file control");
            foreach (var group in perFile)
                Check(group.Select(Signature).OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(baseline), "Large per-file full finding signatures differ from single-file control: " + group.Key);
            var grid = C<DataGrid>("IssuesGrid"); Check(grid.Items.Count == expectedTotal && grid.ItemsSource == Model.Issues, "Large grid items lost");
            grid.ScrollIntoView(grid.Items[^1]); Pump();
            Check(grid.ItemContainerGenerator.ContainerFromItem(grid.Items[^1]) is DataGridRow, "Last result cannot be reached");
            Capture("real-engine-large-1000", "T07 실제 엔진 · 1000개 자체 fixture 결과");
        });
    }
    private static void CloseDuringActualScan()
    {
        Case("actual window close awaits real PHP child exit and owned temporary-folder cleanup", () =>
        {
            string p = Project("close-running");
            string text = "<?php\n" + string.Concat(Enumerable.Repeat("mysql_query('fixture');\n", 300));
            for (int i = 0; i < 300; i++) Subject(p, $"file-{i:D4}.php", text);
            var before = Snapshot(p); var tempBefore = TempChildren();
            MainWindow previous = Window; Window = Open(PackPath);
            try
            {
                Configure(p, Pack.Targets[0].Version, true);
                C<Button>("ScanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(Model.Status == ScanStatus.Running, "Scan did not enter Running before close");
                var field = typeof(MainWindow).GetField("_scanService", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var service = field.GetValue(Window) as ScanService ?? throw new InvalidOperationException("Window does not own ScanService");
                var processField = typeof(ScanService).GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Process? child = null;
                Until(() => { child = processField.GetValue(service) as Process; return child is not null; }, 30);
                int pid = child!.Id; string executable = child.StartInfo.FileName;
                Check(Path.GetFullPath(executable).Equals(Pack.PhpExecutable, StringComparison.OrdinalIgnoreCase), "Close test did not observe real packaged PHP");
                var watch = Stopwatch.StartNew(); Window.Close(); Window.Close();
                Until(() => !Window.IsVisible, 30); watch.Stop();
                Check(!ProcessExists(pid), "Owned real PHP child survived closed HWND");
                Check(TempChildren().SetEquals(tempBefore), "Owned scan work folder survived window close");
                Unchanged(before, p);
                Runs.Add(new { name = "close-running-real-engine", origin = "real PHPCompatibility process", childPid = pid, closeMilliseconds = watch.Elapsed.TotalMilliseconds, sourceUnchanged = true, tempFoldersRestored = true });
            }
            finally { if (Window.IsVisible) { Window.Close(); Until(() => !Window.IsVisible, 30); } Window = previous; }
        });
    }
    private static void CloseDuringPackLoad()
    {
        Case("close during asynchronous pack preparation prevents post-close service ownership", () =>
        {
            var window = new MainWindow(new ScanPresentation()); OwnedWindows.Add(window); window.Show(); Pump();
            var load = window.LoadPackAsync(PackPath);
            window.Close(); window.Close();
            Until(() => !window.IsVisible, 30); Await(load);
            var service = typeof(MainWindow).GetField("_scanService", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
            Check(service is null, "Pack loading attached a service after closing began");
            Check(window.Presentation.Targets.Count == 0, "Closed preparation published late target data");
        });
    }
    private static void SyntheticPartial()
    {
        Case("synthetic partial engine output travels through actual button as incomplete findings", () =>
        {
            string copy = CopyPack("synthetic-partial-pack"); string p = Project("synthetic-partial-subject"); string file = Subject(p, "subject.php");
            string payload = JsonSerializer.Serialize(new { totals = new { errors = 1, warnings = 0, fixable = 0 }, files = new Dictionary<string, object> { [file] = new { errors = 1, warnings = 0, messages = new[] { new { message = "T07 synthetic partial helper — not actual compatibility finding", source = "T07.Synthetic.Partial", severity = 5, type = "ERROR", line = 1, column = 1, fixable = false } } } } });
            PatchEntry(copy, "<?php echo base64_decode('" + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)) + "'); exit(255);\n");
            var old = Window; Window = Open(copy); Window.Title = "T07 합성 도구 출력 · 실제 호환성 결과 아님";
            try
            {
                RunScan("synthetic-partial", p, Pack.Targets[0].Version, true, origin: "synthetic test-owned tool output; not PHPCompatibility findings");
                Check(Model.Status == ScanStatus.Failed && Model.Issues.Count == 1 && Model.ProcessedFileCountText == "미확인" && Model.SummaryText.Contains("일부 결과", StringComparison.Ordinal), "Partial findings hidden or looked complete");
                Check(C<DataGrid>("IssuesGrid").Items.Count == 1 && Model.FailureText.Length > 0, "Partial failure binding missing");
                Capture("synthetic-partial-failure", "T07 합성 도구 출력 · 실제 호환성 검사 결과 아님");
            }
            finally { Window.Close(); Until(() => !Window.IsVisible, 30); Window = old; }
        });
    }

    private static void Configure(string project, string version, bool recursive)
    {
        C<TextBox>("SourceFolderTextBox").Text = project;
        C<ComboBox>("TargetVersionComboBox").SelectedItem = Model.Targets.Single(t => t.Version == version);
        C<CheckBox>("IncludeSubdirectoriesCheckBox").IsChecked = recursive; Pump();
        Check(C<Button>("ScanButton").IsEnabled, "Valid scan button disabled: " + Model.InputMessage);
    }
    private static void RunScan(string name, string project, string version, bool recursive, bool snapshot = true, int maxSeconds = 60, string origin = "actual fixed PHPCompatibility engine")
    {
        Configure(project, version, recursive);
        var before = snapshot ? Snapshot(project) : null;
        var watch = Stopwatch.StartNew(); var heartbeat = Stopwatch.StartNew(); double maxDispatcherGapMs = 0;
        long peakManagedWorkingSet = 0, peakChildWorkingSet = 0; int pulses = 0;
        C<Button>("ScanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Model.Status == ScanStatus.Running && !C<Button>("ScanButton").IsEnabled && !C<TextBox>("SourceFolderTextBox").IsEnabled && !C<ComboBox>("TargetVersionComboBox").IsEnabled, "Running state did not lock inputs immediately");
        // Bypass disabled-button input routing once to verify the handler guard too.
        C<Button>("ScanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Until(() => Model.Status != ScanStatus.Running, maxSeconds, () =>
        {
            maxDispatcherGapMs = Math.Max(maxDispatcherGapMs, heartbeat.Elapsed.TotalMilliseconds); heartbeat.Restart(); pulses++;
            using var current = Process.GetCurrentProcess(); current.Refresh(); peakManagedWorkingSet = Math.Max(peakManagedWorkingSet, current.WorkingSet64);
            var service = typeof(MainWindow).GetField("_scanService", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(Window);
            var child = typeof(ScanService).GetField("_process", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(service) as Process;
            try { if (child is not null && !child.HasExited) { child.Refresh(); peakChildWorkingSet = Math.Max(peakChildWorkingSet, child.WorkingSet64); } }
            catch (InvalidOperationException) { }
        });
        watch.Stop();
        Check(Model.Status is ScanStatus.Completed or ScanStatus.NoFiles or ScanStatus.Failed, "Nonterminal result");
        Check(C<Button>("ScanButton").IsEnabled && C<TextBox>("SourceFolderTextBox").IsEnabled, "Inputs did not unlock after result");
        Check(C<DataGrid>("IssuesGrid").Items.Count == Model.Issues.Count, "Grid/result issue count differs");
        Check(C<TextBlock>("StatusTextBlock").Text == Model.StatusText && C<TextBlock>("ProcessedFileCountTextBlock").Text == Model.ProcessedFileCountText, "Result bindings stale");
        Check(Model.Issues.Count(i => i.Type == "ERROR").ToString(CultureInfo.InvariantCulture) == Model.ErrorCountText && Model.Issues.Count(i => i.Type == "WARNING").ToString(CultureInfo.InvariantCulture) == Model.WarningCountText, "Displayed engine totals differ from raw findings");
        if (before is not null) Unchanged(before, project);
        Runs.Add(new { name, origin, version, recursive, status = Model.Status.ToString(), selected = Model.SelectedFileCountText, processed = Model.ProcessedFileCountText, errors = Model.ErrorCountText, warnings = Model.WarningCountText, findings = Model.Issues.Count, milliseconds = watch.Elapsed.TotalMilliseconds, dispatcherPulses = pulses, maxObservedDispatcherGapMilliseconds = maxDispatcherGapMs, sampledPeakGuiWorkingSetBytes = peakManagedWorkingSet, sampledPeakChildWorkingSetBytes = peakChildWorkingSet, sampleFindings = Model.Issues.Take(2).ToArray(), sourceSnapshotVerified = snapshot });
    }
    private static string Project(string name) { string p = Path.Combine(Area, name); Directory.CreateDirectory(p); return p; }
    private static string Subject(string p, string name, string text = "<?php echo 'safe';\n") { string f = Path.Combine(p, name); Directory.CreateDirectory(Path.GetDirectoryName(f)!); File.WriteAllText(f, text, new UTF8Encoding(false)); return f; }
    private static SortedDictionary<string, string> Snapshot(string root)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
            result[Path.GetRelativePath(root, p)] = Directory.Exists(p) ? "directory" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
        return result;
    }
    private static void Unchanged(SortedDictionary<string, string> before, string project) => Check(before.SequenceEqual(Snapshot(project)), "Source file/directory list or SHA-256 changed");
    private static HashSet<string> TempChildren() { string p = Path.Combine(Path.GetTempPath(), "DK-PHP-Compatibility-Checker"); return Directory.Exists(p) ? Directory.GetDirectories(p).ToHashSet(StringComparer.OrdinalIgnoreCase) : new(StringComparer.OrdinalIgnoreCase); }
    private static bool ProcessExists(int pid) { try { using var p = Process.GetProcessById(pid); return !p.HasExited; } catch (ArgumentException) { return false; } }
    private static string CopyPack(string name)
    {
        string copy = Project(name);
        foreach (var d in Directory.GetDirectories(PackPath, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(copy, Path.GetRelativePath(PackPath, d)));
        foreach (var f in Directory.GetFiles(PackPath, "*", SearchOption.AllDirectories)) File.Copy(f, Path.Combine(copy, Path.GetRelativePath(PackPath, f)));
        return copy;
    }
    private static void PatchEntry(string pack, string helper)
    {
        string path = Path.Combine(pack, "manifest.json"); var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        string entry = manifest["engine"]!["entryPoint"]!.GetValue<string>(); string full = Path.Combine(pack, entry.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(full, helper, new UTF8Encoding(false)); string digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant();
        manifest["engine"]!["files"]![entry] = digest;
        string report = manifest["verification"]!["reportPath"]!.GetValue<string>(); string evidencePath = Path.Combine(pack, report.Replace('/', Path.DirectorySeparatorChar));
        var evidence = JsonNode.Parse(File.ReadAllText(evidencePath))!; evidence["engineFiles"]![entry] = digest;
        File.WriteAllText(evidencePath, evidence.ToJsonString(), new UTF8Encoding(false));
        manifest["verification"]!["files"]![report] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(evidencePath))).ToLowerInvariant();
        File.WriteAllText(path, manifest.ToJsonString(), new UTF8Encoding(false));
    }
    private static T C<T>(string name) where T : DependencyObject => Window.FindName(name) as T ?? throw new InvalidOperationException("Named control missing: " + name);
    private static void Case(string name, Action action) { try { action(); Passed.Add(name); Console.WriteLine("PASS " + name); } catch (Exception ex) { Failures.Add(new { name, error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex.Message); } }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Pump() { var frame = new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false)); Dispatcher.PushFrame(frame); }
    private static void Until(Func<bool> condition, int seconds = 60, Action? sample = null)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition() && DateTime.UtcNow < end) { Pump(); sample?.Invoke(); Thread.Sleep(10); }
        Check(condition(), "Timed out waiting for WPF/scan state"); Pump();
    }
    private static void Await(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); Pump(); }
    private static T Await<T>(Task<T> task) { Until(() => task.IsCompleted); T value = task.GetAwaiter().GetResult(); Pump(); return value; }
    private static void RunTool(string executable, string[] arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string a in arguments) info.ArgumentList.Add(a);
        using var p = Process.Start(info)!; string output = p.StandardOutput.ReadToEnd(); string error = p.StandardError.ReadToEnd(); p.WaitForExit(); Check(p.ExitCode == 0, executable + " failed: " + output + error);
    }
    private static string SdkVersion() { using var p = Process.Start(new ProcessStartInfo("dotnet", "--version") { RedirectStandardOutput = true, UseShellExecute = false, WorkingDirectory = Repo })!; string text = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); return text; }
    private static void Capture(string name, string label)
    {
        Window.UpdateLayout(); var content = (FrameworkElement)Window.Content;
        int width = (int)Math.Ceiling(content.ActualWidth + content.Margin.Left + content.Margin.Right);
        int height = (int)Math.Ceiling(content.ActualHeight + content.Margin.Top + content.Margin.Bottom);
        var client = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); client.Render(content);
        var visual = new DrawingVisual(); const int banner = 34;
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, width, height + banner));
            dc.DrawText(new FormattedText(label, CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight, new Typeface("Malgun Gothic"), 13, Brushes.DarkSlateBlue, 1), new Point(10, 8));
            dc.DrawRectangle(Window.Background ?? SystemColors.WindowBrush, null, new Rect(0, banner, width, height)); dc.DrawImage(client, new Rect(0, banner, width, height));
        }
        var rendered = new RenderTargetBitmap(width, height + banner, 96, 96, PixelFormats.Pbgra32); rendered.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
        string f = Path.Combine(Output, name + ".png"); using (var stream = File.Create(f)) encoder.Save(stream);
        Screenshots.Add(new { file = name + ".png", label, width, height = height + banner, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))).ToLowerInvariant(), visibleHwnd = IsWindowVisible(new WindowInteropHelper(Window).Handle) });
    }
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
}
