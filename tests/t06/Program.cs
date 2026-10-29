using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    private static readonly List<object> Windows = [];
    private static string Repo = "";
    private static string Area = "";
    private static string Output = "";
    private static MainWindow? Window;
    private static EnginePack Pack = null!;
    private static ScanPresentation Model = null!;
    private static string Project = "";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || !OperatingSystem.IsWindows()
            || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Console.Error.WriteLine("Actual Windows x64 required: <repoRoot> <runtimePack>");
            return 2;
        }
        Repo = Path.GetFullPath(args[0]);
        Output = Path.Combine(Repo, "artifacts/t06");
        Area = Path.Combine(Path.GetTempPath(), "dk-t06-verification-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(Area);
        Project = Path.Combine(Area, "한글 폴더 & source");
        Directory.CreateDirectory(Project);
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            Case("actual WPF window is visible and responsive on Windows", () =>
            {
                Model = new ScanPresentation();
                Window = new MainWindow(Model) { Title = "T06 검증용 데이터 — 실제 검사 결과 아님" };
                Window.Show(); Pump();
                var handle = new WindowInteropHelper(Window).Handle;
                Check(handle != IntPtr.Zero && IsWindowVisible(handle) && Window.IsVisible, "Actual WPF HWND is not visible");
                using var current = Process.GetCurrentProcess();
                Check(current.Responding, "Window process is not responding");
                Check(Window.ActualWidth >= Window.MinWidth && Window.ActualHeight >= Window.MinHeight, "Initial window dimensions invalid");
                Windows.Add(new { hwnd = handle.ToInt64().ToString("X", CultureInfo.InvariantCulture), width = Window.ActualWidth, height = Window.ActualHeight, visible = true, responding = true });
            });
            if (Window is not null)
            {
                Case("initial presentation has no invented supported versions or findings", () =>
                {
                    Check(Model.Targets.Count == 0 && Model.SelectedTarget is null && Model.Issues.Count == 0, "Initial mock versions/results leaked into product");
                    Check(Model.IncludeSubdirectories && Control<CheckBox>("IncludeSubdirectoriesCheckBox").IsChecked == true, "Recursive default is not enabled");
                    Check(!Model.CanPrepareScan && !Control<Button>("ScanButton").IsEnabled, "Unprepared scan available");
                    Check(Control<TextBlock>("StatusTextBlock").Text == Model.StatusText && Model.Status == ScanStatus.Idle, "Initial status binding incorrect");
                });
                Case("verified pack targets are loaded into non-editable version control", () =>
                {
                    Pack = Await(new EnginePackLoader().LoadAsync(Path.GetFullPath(args[1])));
                    Await(Window.LoadPackAsync(Path.GetFullPath(args[1]))); Pump();
                    var combo = Control<ComboBox>("TargetVersionComboBox");
                    Check(!combo.IsEditable, "Version control permits arbitrary text");
                    Check(Model.Targets.Select(t => t.Version).SequenceEqual(Pack.Targets.Select(t => t.Version)), "Model target list differs from verified pack");
                    Check(combo.Items.Cast<PhpTarget>().Select(t => t.Version).SequenceEqual(Pack.Targets.Select(t => t.Version)), "Visible version list differs from verified pack");
                    Check(Model.SelectedTarget is null, "Initial goal was silently selected");
                });
                if (Pack is not null)
                {
                    InputCases();
                    ResultCases();
                    PackFailureCases(args[1]);
                }
                Case("window closes normally without starting a PHP scan", () =>
                {
                    var handle = new WindowInteropHelper(Window).Handle;
                    Window.Close(); Pump();
                    Check(!Window.IsVisible && !IsWindow(handle), "Closed WPF window survived");
                    Window = null;
                });
                Case("default startup reports missing pack without fake targets", () =>
                {
                    var defaultPack = Path.Combine(AppContext.BaseDirectory, "tools/runtime");
                    Check(!Directory.Exists(defaultPack), "Harness default pack precondition unexpectedly exists");
                    var startup = new MainWindow { Title = "T06 검증용 시작 화면 — 실제 검사 결과 아님" };
                    try
                    {
                        startup.Show();
                        Until(() => startup.Presentation.Status == ScanStatus.Failed);
                        Check(startup.Presentation.Targets.Count == 0 && startup.Presentation.SelectedTarget is null, "Missing default pack exposed fake support");
                        Check(!string.IsNullOrWhiteSpace(startup.Presentation.FailureText), "Missing pack failure hidden");
                        Capture("startup-missing-pack", startup);
                    }
                    finally { startup.Close(); Pump(); }
                });
            }
        }
        catch (Exception ex)
        {
            Failures.Add(new { name = "verification setup or orchestration", error = ex.ToString() });
        }
        finally
        {
            Window?.Close();
            application.Shutdown();
            try { Directory.Delete(Area, recursive: true); }
            catch (Exception ex) { Failures.Add(new { name = "managed fixture cleanup", error = ex.ToString() }); }
        }
        var summary = new
        {
            environment = new { os = Environment.OSVersion.VersionString, architecture = RuntimeInformation.ProcessArchitecture.ToString(), sdk = SdkVersion(), runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"), sourceSha = Environment.GetEnvironmentVariable("GITHUB_SHA") },
            packId = Pack?.PackId,
            targets = Pack?.Targets.Select(t => t.Version),
            passed = Passed,
            failures = Failures,
            windows = Windows,
            screenshots = Screenshots,
            scopePassed = Failures.Count == 0,
            scope = "Actual WPF HWND and presentation/binding tests using explicitly labelled test data; no ScanService execution or GUI engine integration.",
            unverified = new[] { "OS native folder-picker modal interaction", "Real scan button/engine and close-event integration (T07)", "Clean Windows 11/CRT deployment (T09)", "Engine pack exchange/restore integration (T10)" }
        };
        File.WriteAllText(Path.Combine(Output, "summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        Console.WriteLine("T06_SUMMARY_JSON=" + JsonSerializer.Serialize(summary));
        return Failures.Count == 0 ? 0 : 1;
    }

    private static void InputCases()
    {
        Case("folder and goal are both required; unknown folders are rejected", () =>
        {
            Model.SourceDirectory = ""; Model.SelectedTarget = null; Pump();
            Check(!Model.CanPrepareScan && !string.IsNullOrWhiteSpace(Model.InputMessage), "Missing inputs accepted");
            Control<TextBox>("SourceFolderTextBox").Text = Project; Pump();
            Check(Model.SourceDirectory == Project && !Model.CanPrepareScan, "Source text binding or goal requirement incorrect");
            Control<ComboBox>("TargetVersionComboBox").SelectedItem = Model.Targets[0]; Pump();
            Check(Model.CanPrepareScan && ReferenceEquals(Model.SelectedTarget, Model.Targets[0]), "Valid goal/folder not recognized");
            Model.SourceDirectory = Path.Combine(Area, "missing"); Pump();
            Check(!Model.CanPrepareScan && !string.IsNullOrWhiteSpace(Model.InputMessage), "Missing folder accepted");
            Model.SourceDirectory = Project; Pump();
            Check(Model.CanPrepareScan, "Valid folder was not restored");
        });
        Case("unverified goal object cannot become selected", () =>
        {
            Model.SelectedTarget = new PhpTarget("99.0", "unverified", Array.Empty<string>()); Pump();
            Check(Model.SelectedTarget is null && !Model.CanPrepareScan && Control<ComboBox>("TargetVersionComboBox").SelectedItem is null, "Unverified goal was accepted");
            Model.SelectedTarget = Model.Targets[0]; Pump();
        });
        Case("recursive checkbox is two-way and scan remains intentionally disconnected", () =>
        {
            var checkbox = Control<CheckBox>("IncludeSubdirectoriesCheckBox");
            checkbox.IsChecked = false; Pump(); Check(!Model.IncludeSubdirectories, "Checkbox did not update recursion input");
            checkbox.IsChecked = true; Pump(); Check(Model.IncludeSubdirectories, "Recursive input did not restore");
            Check(Model.CanPrepareScan && !Control<Button>("ScanButton").IsEnabled, "T06 prematurely enabled a real scan");
            Capture("ready-inputs");
        });
        Case("running locks input controls and clears stale results", () =>
        {
            string original = Model.SourceDirectory; var target = Model.SelectedTarget; bool recursive = Model.IncludeSubdirectories;
            Model.ShowRunning(); Pump();
            Check(Model.Status == ScanStatus.Running && !Model.CanPrepareScan && !Model.CanEditInputs, "Running status did not lock input");
            foreach (var name in new[] { "SourceFolderTextBox", "FolderBrowseButton", "TargetVersionComboBox", "IncludeSubdirectoriesCheckBox" })
                Check(!Control<Control>(name).IsEnabled, "Running control remained editable: " + name);
            Model.SourceDirectory = "changed"; Model.SelectedTarget = null; Model.IncludeSubdirectories = !recursive; Pump();
            Check(Model.SourceDirectory == original && ReferenceEquals(Model.SelectedTarget, target) && Model.IncludeSubdirectories == recursive, "Running input setters changed execution context");
            Check(Model.Issues.Count == 0 && Model.ProcessedFileCountText == "미확인", "Running displayed stale findings/count");
            Check(Control<TextBlock>("StatusTextBlock").Text.Contains("중", StringComparison.Ordinal), "Visible running status missing");
            Model.ApplyResult(Result(ScanStatus.Completed, 1, 1)); Pump();
        });
    }

    private static void ResultCases()
    {
        Case("completed zero findings has limited static-analysis meaning", () =>
        {
            Model.ApplyResult(Result(ScanStatus.Completed, 2, 2)); Pump();
            Check(Model.Status == ScanStatus.Completed && Model.SummaryText.Contains("발견하지 못", StringComparison.Ordinal), "Zero findings falsely described as full compatibility");
            Check(Model.LimitationsText.Contains("보장하지", StringComparison.Ordinal), "Runtime limitation missing");
            AssertCounts("2", "2", "0", "0"); AssertTextBindings();
            Check(Control<DataGrid>("IssuesGrid").Items.Count == 0, "Zero report retained old findings");
            Capture("completed-zero");
        });
        Case("Korean long paths/messages and same-line findings remain intact in read-only grid", () =>
        {
            var issues = LongIssues();
            Model.ApplyResult(Result(ScanStatus.Completed, 2, 2, issues)); Pump();
            var grid = Control<DataGrid>("IssuesGrid");
            Check(grid.IsReadOnly && !grid.CanUserAddRows && grid.Items.Count == 2, "Result grid editable, collapsed or incomplete");
            Check(Model.Issues[0] == issues[0] && Model.Issues[1] == issues[1] && Model.Issues[0].Line == Model.Issues[1].Line, "Original independent findings changed");
            Check(grid.Columns.Any(c => Equals(c.Header, "파일")) || grid.Columns.Any(c => Equals(c.Header, "파일 경로")), "File column missing");
            grid.ScrollIntoView(grid.Items[0]); grid.UpdateLayout(); Pump();
            var text = Descendants<TextBlock>(grid).Where(t => t.Text == issues[0].Message).ToArray();
            Check(text.Length > 0 && text.Any(t => t.TextWrapping == TextWrapping.Wrap), "Long message is not visibly wrapping in the grid");
            Check(Descendants<TextBlock>(grid).Any(t => t.Text == issues[0].FilePath), "Long Korean path not present in grid visuals");
            AssertCounts("2", "2", "1", "1"); AssertTextBindings();
            Capture("completed-findings");
        });
        Case("NoFiles differs from completed zero and is not a compatibility pass", () =>
        {
            Model.ApplyResult(Result(ScanStatus.NoFiles, 0, 0)); Pump();
            Check(Model.StatusText.Contains("대상 없음", StringComparison.Ordinal) && Model.SummaryText.Contains("통과", StringComparison.Ordinal), "NoFiles meaning is missing");
            AssertCounts("0", "0", "0", "0");
            Check(Control<DataGrid>("IssuesGrid").Items.Count == 0, "NoFiles retained findings");
            Capture("no-files");
        });
        Case("failure with unknown processed count never substitutes selected count", () =>
        {
            Model.ApplyResult(Result(ScanStatus.Failed, 17, null, failure: "검증용: 파일을 읽을 수 없습니다.")); Pump();
            Check(Model.ProcessedFileCountText == "미확인" && Model.FailureText.Contains("읽을", StringComparison.Ordinal), "Failure count/reason hidden");
            AssertCounts("17", "미확인", "0", "0"); AssertTextBindings();
            Check(Control<TextBlock>("FailureTextBlock").Text == Model.FailureText, "Visible failure reason differs");
            Capture("failed-unknown");
        });
        Case("partial failed findings and excluded paths are displayed as incomplete", () =>
        {
            var excluded = new[] { Path.Combine(Project, "제외된 링크"), Path.Combine(Project, "cloud-placeholder.php") };
            var result = Result(ScanStatus.Failed, 10, 1, LongIssues(), "검증용 부분 실패: 나머지 파일을 처리하지 못했습니다.") with { ExcludedPaths = excluded };
            Model.ApplyResult(result); Pump();
            Check(Model.Status == ScanStatus.Failed && Model.Issues.Count == 2 && Model.SummaryText.Contains("일부", StringComparison.Ordinal), "Partial data looked completed or vanished");
            Check(Control<DataGrid>("IssuesGrid").Items.Count == 2 && Control<ListBox>("ExcludedPathsList").Items.Count == 2, "Partial/excluded paths missing from bindings");
            Check(Model.ExcludedPaths.SequenceEqual(excluded), "Excluded paths changed");
            var expander = Descendants<Expander>(Window!).Single(e => Equals(e.Header, "제외된 경로"));
            expander.IsExpanded = true; Pump();
            Check(Control<ListBox>("ExcludedPathsList").IsVisible, "Excluded paths are not actually visible when expanded");
            AssertCounts("10", "1", "1", "1"); AssertTextBindings();
            Capture("failed-partial");
            expander.IsExpanded = false; Pump();
        });
        Case("input edits clear stale findings, exclusions and failure diagnostics", () =>
        {
            var other = Path.Combine(Area, "other-source"); Directory.CreateDirectory(other);
            Control<TextBox>("SourceFolderTextBox").Text = other; Pump();
            Check(Model.Status == ScanStatus.Idle && Model.Issues.Count == 0 && Model.ExcludedPaths.Count == 0 && Model.FailureText == "", "Input edit retained old result context");
            Check(Control<DataGrid>("IssuesGrid").Items.Count == 0 && Control<ListBox>("ExcludedPathsList").Items.Count == 0 && Control<TextBlock>("FailureTextBlock").Text == "", "Visible stale rows/failure survived edit");
            Model.SourceDirectory = Project; Pump();
        });
        Case("result request restores the associated visible inputs", () =>
        {
            Model.SourceDirectory = Path.Combine(Area, "other-source"); Model.IncludeSubdirectories = false; Pump();
            Model.ApplyResult(Result(ScanStatus.Completed, 1, 1)); Pump();
            Check(Model.SourceDirectory == Project && Control<TextBox>("SourceFolderTextBox").Text == Project && Model.IncludeSubdirectories, "Result/request context mismatch");
        });
        Case("minimum-size window keeps result area measurable and readable", () =>
        {
            Model.ApplyResult(Result(ScanStatus.Completed, 2, 2, LongIssues())); Pump();
            double width = Window!.Width, height = Window.Height;
            try
            {
                Window.Width = Window.MinWidth; Window.Height = Window.MinHeight; Pump();
                Capture("minimum-size");
                var grid = Control<DataGrid>("IssuesGrid");
                Check(grid.ActualWidth >= 400 && grid.ActualHeight >= 60, "Result area collapsed at minimum window size");
                foreach (var name in new[] { "SourceFolderTextBox", "TargetVersionComboBox", "IssuesGrid", "LimitationsTextBlock" })
                    AssertReachable(name);
                AssertStatusFullyVisible();
            }
            finally { Window.Width = width; Window.Height = height; Pump(); }
        });
        Case("long failure and limits remain reachable with exclusions open at minimum size", () =>
        {
            double width = Window!.Width, height = Window.Height;
            var expander = Descendants<Expander>(Window).Single(e => Equals(e.Header, "제외된 경로"));
            try
            {
                var verifiedTarget = Pack.Targets[0];
                var longTarget = verifiedTarget with { Limitations = verifiedTarget.Limitations.Concat(new[] {
                    string.Concat(Enumerable.Repeat("검증용 긴 한계 설명: 실제 실행 결과가 아닌 화면 표시 데이터를 사용합니다. ", 8)) }).ToArray() };
                Model.SetPack(Pack with { Targets = new[] { longTarget } });
                var failure = string.Concat(Enumerable.Repeat("검증용 긴 실패 원인: 파일 읽기 실패로 전체 검사를 완료하지 못했습니다. ", 6));
                Model.ApplyResult(Result(ScanStatus.Failed, 10, 1, LongIssues(), failure) with
                    { ExcludedPaths = new[] { Path.Combine(Project, "제외된 링크"), Path.Combine(Project, "두번째 제외 경로") } });
                expander.IsExpanded = true;
                Window.Width = Window.MinWidth; Window.Height = Window.MinHeight; Pump();
                Capture("minimum-long-diagnostics");
                foreach (var name in new[] { "SourceFolderTextBox", "TargetVersionComboBox", "IssuesGrid", "FailureTextBlock", "ExcludedPathsList", "LimitationsTextBlock" })
                    AssertReachable(name);
                AssertStatusFullyVisible();
            }
            finally
            {
                expander.IsExpanded = false; Model.SetPack(Pack);
                Window.Width = width; Window.Height = height; Pump();
            }
        });
    }

    private static void PackFailureCases(string packDirectory)
    {
        Case("visible target list uses only the supplied verified pack data", () =>
        {
            Model.SelectedTarget = null;
            var subset = Pack with { Targets = Pack.Targets.Take(1).ToArray() };
            Model.SetPack(subset); Pump();
            Check(Model.Targets.Count == 1 && Control<ComboBox>("TargetVersionComboBox").Items.Count == 1, "View hardcodes target versions instead of supplied data");
            Check(Model.Targets[0].Version == subset.Targets[0].Version, "Supplied goal data changed");
            Await(Window!.LoadPackAsync(Path.GetFullPath(packDirectory))); Pump();
            Check(Model.Targets.Count == Pack.Targets.Count, "Real verified target list did not restore");
        });
        Case("missing pack clears targets and displays an honest preparation failure", () =>
        {
            Await(Window!.LoadPackAsync(Path.Combine(Area, "missing-pack"))); Pump();
            Check(Model.Status == ScanStatus.Failed && Model.Targets.Count == 0 && Model.SelectedTarget is null && !Model.CanPrepareScan, "Missing pack retained usable support/input state");
            Check(!string.IsNullOrWhiteSpace(Control<TextBlock>("FailureTextBlock").Text), "Missing pack reason invisible");
        });
        Case("invalid manifest clears targets instead of inventing support", () =>
        {
            var bad = Path.Combine(Area, "invalid-pack"); Directory.CreateDirectory(bad); File.WriteAllText(Path.Combine(bad, "manifest.json"), "{}");
            Await(Window!.LoadPackAsync(bad)); Pump();
            Check(Model.Status == ScanStatus.Failed && Model.Targets.Count == 0 && !Control<Button>("ScanButton").IsEnabled, "Invalid pack appeared usable");
            Check(Control<ComboBox>("TargetVersionComboBox").Items.Count == 0 && !string.IsNullOrWhiteSpace(Model.FailureText), "Invalid pack failure hidden");
            Await(Window.LoadPackAsync(Path.GetFullPath(packDirectory))); Pump();
            Check(Model.Status == ScanStatus.Idle && Model.Targets.Count == Pack.Targets.Count && Model.Issues.Count == 0, "Valid pack did not restore idle presentation");
        });
    }

    private static ScanResult Result(ScanStatus status, int selected, int? processed,
        IReadOnlyList<ScanIssue>? issues = null, string? failure = null)
    {
        issues ??= Array.Empty<ScanIssue>();
        return new ScanResult(new ScanRequest(Project, Pack.Targets[0].Version, true), status,
            selected, processed, issues.Count(i => i.Type == "ERROR"), issues.Count(i => i.Type == "WARNING"), issues, Pack.EngineInfo, failure);
    }
    private static ScanIssue[] LongIssues()
    {
        string file = Path.Combine(Project, string.Concat(Enumerable.Repeat("긴경로_한국어_공백 & ", 5)), "very_long_source_filename_compatibility.php");
        string message = "검증용 문제 설명입니다. 실제 엔진 검사가 아닌 화면 표시 테스트이며 원문 메시지를 줄바꿈하여 모두 확인할 수 있어야 합니다. " + new string('A', 80);
        return [new ScanIssue(file, 123, 7, "ERROR", 5, "Test.Rule.One", message, false),
                new ScanIssue(file, 123, 19, "WARNING", 5, "Test.Rule.Two", "검증용 별도 규칙: 같은 줄의 독립 문제도 유지합니다.", false)];
    }
    private static void AssertCounts(string selected, string processed, string errors, string warnings)
    {
        Check(Model.SelectedFileCountText == selected && Model.ProcessedFileCountText == processed && Model.ErrorCountText == errors && Model.WarningCountText == warnings, "Presentation counts differ from supplied result");
        Check(Control<TextBlock>("SelectedFileCountTextBlock").Text == selected
            && Control<TextBlock>("ProcessedFileCountTextBlock").Text == processed
            && Control<TextBlock>("ErrorCountTextBlock").Text == errors
            && Control<TextBlock>("WarningCountTextBlock").Text == warnings, "Visible count bindings differ from presentation");
    }
    private static void AssertTextBindings()
    {
        Check(Control<TextBlock>("StatusTextBlock").Text == Model.StatusText, "Status binding differs");
        Check(Control<TextBlock>("SummaryTextBlock").Text == Model.SummaryText, "Summary binding differs");
        Check(Control<TextBlock>("FailureTextBlock").Text == Model.FailureText, "Failure binding differs");
        Check(Control<TextBlock>("LimitationsTextBlock").Text == Model.LimitationsText, "Limitations binding differs");
    }
    private static T Control<T>(string name) where T : DependencyObject =>
        Window?.FindName(name) as T ?? throw new InvalidOperationException("Named UI element missing or wrong type: " + name);
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private static void AssertReachable(string name)
    {
        var element = Control<FrameworkElement>(name);
        Check(element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0, "Important control is not visible: " + name);
        FrameworkElement owner = element;
        // A bounded scroll viewport is acceptable for lengthy diagnostics. Check
        // its frame rather than requiring the entire scrollable text to fit.
        for (DependencyObject? parent = VisualTreeHelper.GetParent(element); parent is not null && !ReferenceEquals(parent, Window!.Content); parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer scroll && scroll.IsVisible && (scroll.ScrollableHeight > 0 || scroll.ScrollableWidth > 0))
            { owner = scroll; break; }
        }
        var client = (FrameworkElement)Window!.Content;
        var bounds = owner.TransformToAncestor(client).TransformBounds(new Rect(owner.RenderSize));
        Check(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= client.ActualWidth + 1 && bounds.Bottom <= client.ActualHeight + 1,
            $"Important control/viewport outside client: {name} bounds={bounds}, client={client.ActualWidth}x{client.ActualHeight}");
    }
    private static void AssertStatusFullyVisible()
    {
        // The short status is essential: unlike lengthy diagnostics, access by
        // scrolling is insufficient. Its own complete rectangle must be visible.
        var status = Control<TextBlock>("StatusTextBlock");
        var client = (FrameworkElement)Window!.Content;
        Check(status.IsVisible && status.ActualWidth > 0 && status.ActualHeight > 0
            && status.ActualHeight + 1 >= status.DesiredSize.Height, "Status text is collapsed or vertically truncated");
        var bounds = status.TransformToAncestor(client).TransformBounds(new Rect(status.RenderSize));
        var clientBounds = new Rect(0, 0, client.ActualWidth, client.ActualHeight);
        clientBounds.Inflate(1, 1);
        Check(clientBounds.Contains(bounds), "Status text itself extends outside the window client: " + bounds);
        for (DependencyObject? parent = VisualTreeHelper.GetParent(status); parent is not null && !ReferenceEquals(parent, client); parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollContentPresenter viewport)
            {
                var visible = viewport.TransformToAncestor(client).TransformBounds(new Rect(viewport.RenderSize));
                visible.Inflate(1, 1);
                Check(visible.Contains(bounds), "Status text is clipped by a scroll viewport: " + bounds);
            }
        }
    }
    private static void Case(string name, Action action)
    {
        try { action(); Passed.Add(name); Console.WriteLine("PASS " + name); }
        catch (Exception ex) { Failures.Add(new { name, error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
        Window?.UpdateLayout();
    }
    private static void Until(Func<bool> condition)
    {
        var stop = DateTime.UtcNow.AddSeconds(15);
        while (!condition() && DateTime.UtcNow < stop) { Pump(); Thread.Sleep(10); }
        Check(condition(), "Timed out waiting for WPF state"); Pump();
    }
    private static void Await(Task task)
    {
        Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); Pump();
    }
    private static T Await<T>(Task<T> task)
    {
        Until(() => task.IsCompleted); var result = task.GetAwaiter().GetResult(); Pump(); return result;
    }
    private static void Capture(string name, MainWindow? target = null)
    {
        target ??= Window ?? throw new InvalidOperationException("No capture window");
        target.UpdateLayout();
        Check(target.IsVisible && IsWindowVisible(new WindowInteropHelper(target).Handle), "Cannot capture an invisible WPF window");
        var content = (FrameworkElement)target.Content;
        // Render preserves the root visual's layout offset. Include its outer
        // margins in the canvas so the right/bottom client edges are not clipped.
        int width = Math.Max(1, (int)Math.Ceiling(content.ActualWidth + content.Margin.Left + content.Margin.Right));
        int height = Math.Max(1, (int)Math.Ceiling(content.ActualHeight + content.Margin.Top + content.Margin.Bottom));
        var client = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        client.Render(content);
        const int banner = 34;
        var drawing = new DrawingVisual();
        using (var dc = drawing.RenderOpen())
        {
            dc.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, width, height + banner));
            dc.DrawText(new FormattedText("T06 검증용 데이터 · 실제 검사 결과 아님", CultureInfo.GetCultureInfo("ko-KR"), FlowDirection.LeftToRight,
                new Typeface("Malgun Gothic"), 13, Brushes.DarkSlateBlue, 1.0), new Point(10, 8));
            dc.DrawRectangle(target.Background ?? SystemColors.WindowBrush, null, new Rect(0, banner, width, height));
            dc.DrawImage(client, new Rect(0, banner, width, height));
        }
        var rendered = new RenderTargetBitmap(width, height + banner, 96, 96, PixelFormats.Pbgra32); rendered.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(rendered));
        string file = name + ".png";
        using (var stream = File.Create(Path.Combine(Output, file))) encoder.Save(stream);
        Screenshots.Add(new { file, width, height = height + banner, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Output, file)))).ToLowerInvariant(), labelledTestData = true, visibleHwnd = true });
    }
    private static string SdkVersion()
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", "--version") { WorkingDirectory = Repo, UseShellExecute = false, RedirectStandardOutput = true });
        if (process is null) return "unavailable";
        string version = process.StandardOutput.ReadToEnd().Trim(); process.WaitForExit(); return version;
    }
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr handle);
}
