using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Dk.PhpCompatibilityChecker;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

internal static class GuiCases
{
    private static string _stage = "not started";
    private static void Stage(string value) { Volatile.Write(ref _stage, value); Console.WriteLine("T14_GUI_STAGE=" + value); }
    internal static async Task Verify(string packs, string output)
    {
        var fixtureArea = Path.Combine(output, "gui-fixture");
        Directory.CreateDirectory(fixtureArea);
        var fixture = await PackFixture.CreateAsync(packs, fixtureArea, "full");
        var managed = Path.Combine(output, "gui-managed");
        var bundled = Path.Combine(packs, "reduced");
        var bundledPack = await new EnginePackLoader().LoadAsync(bundled);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Run(fixture, bundled, bundledPack, managed); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromMinutes(2)); }
        catch (TimeoutException ex) { throw new TimeoutException("T14 WPF stage timed out: " + Volatile.Read(ref _stage), ex); }
    }

    private static void Run(PackFixture fixture, string bundled, EnginePack activePack, string managed)
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        MainWindow? window = null;
        try
        {
            Stage("main-window-start");
            window = new MainWindow(new ScanPresentation(), bundled, managed, fixture.CreateInstaller(managed));
            window.Show();
            Until(() => window.Presentation.Targets.Count > 0);
            Stage("main-window-loaded");
            var before = window.Presentation.Targets.Select(target => target.Version).ToArray();
            var button = (Button)window.FindName("ManageEngineButton");
            if (!button.IsEnabled || before.Length >= fixture.Pack.Targets.Count)
                throw new InvalidOperationException("Reduced bundled pack and engine management entry were not shown.");

            // The modal MainWindow entry is wired in production; the verification
            // exercises the same manager as a visible independent WPF window to
            // avoid nesting the verification dispatcher inside ShowDialog.

            // Exercise the actual WPF manager controls as a separate visible
            // window, without blocking the same verification dispatcher.
            using (var installer = fixture.CreateInstaller(managed))
            {
                var manager = new EngineManagementWindow(activePack, installer, "", bundled) { Owner = window };
                manager.Show();
                Stage("manager-visible");
                Until(() => manager.IsVisible);
                if (!((TextBox)manager.FindName("EngineDetailsTextBox")).Text.Contains(activePack.EngineInfo.PhpCompatibilityVersion)
                    || !((TextBox)manager.FindName("EngineTargetsTextBox")).Text.Contains(before[0]))
                    throw new InvalidOperationException("Current engine/version information is not visible.");

                var confirmation = Task.Run(() => ConfirmDownload(new WindowInteropHelper(manager).Handle));
                Stage("manager-update-click-and-confirm");
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                    ((Button)manager.FindName("CheckEngineUpdateButton"))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent))));
                Until(() => confirmation.IsCompleted);
                confirmation.GetAwaiter().GetResult();
                Stage("manager-confirmed");

                var status = (TextBlock)manager.FindName("EngineUpdateStatusTextBlock");
                Until(() => (manager.RestartRequired || status.Text.Contains("못했습니다", StringComparison.Ordinal))
                    && ((Button)manager.FindName("CloseEngineManagementButton")).IsEnabled);
                if (!manager.RestartRequired || !status.Text.Contains("다시 실행", StringComparison.Ordinal)
                    || !before.SequenceEqual(window.Presentation.Targets.Select(target => target.Version)))
                    throw new InvalidOperationException("Engine management download failed: " + status.Text);
                Stage("manager-update-applied");
                manager.Close();
                Until(() => !manager.IsVisible);
                Stage("manager-closed");
            }
            window.Close();
            Until(() => !window.IsVisible);
            Stage("first-main-closed");

            // Omit the installer (no catalog transport exists) to verify offline startup.
            window = new MainWindow(new ScanPresentation(), bundled, managed, null);
            window.Show();
            Until(() => window.Presentation.Targets.Count > 0);
            if (!window.Presentation.Targets.Select(target => target.Version)
                .SequenceEqual(fixture.Pack.Targets.Select(target => target.Version)))
                throw new InvalidOperationException("A new window did not activate the installed verified targets.");
            Stage("restart-targets-confirmed");
        }
        finally
        {
            if (window?.IsVisible == true) { window.Close(); Until(() => !window.IsVisible); }
            application.Shutdown();
        }
    }

    private static void Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(35);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        }
        if (!condition()) throw new TimeoutException("T14 GUI did not reach the expected state.");
    }

    private static void ConfirmDownload(IntPtr owner)
    {
        Console.WriteLine("T14_GUI_CONFIRM_SEARCH owner=" + owner + " pid=" + Environment.ProcessId);
        IntPtr dialog = IntPtr.Zero;
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (dialog == IntPtr.Zero && DateTime.UtcNow < deadline)
        {
            EnumWindows((window, _) =>
            {
                var name = new StringBuilder(32);
                GetClassName(window, name, name.Capacity);
                GetWindowThreadProcessId(window, out var processId);
                if (IsWindowVisible(window) && processId == (uint)Environment.ProcessId && name.ToString() == "#32770")
                {
                    dialog = window;
                    Console.WriteLine("T14_GUI_CONFIRM_FOUND hwnd=" + dialog + " owner=" + GetWindow(dialog, 4));
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (dialog == IntPtr.Zero) Thread.Sleep(25);
        }
        if (dialog == IntPtr.Zero)
        {
            Console.Error.WriteLine("T14_GUI_CONFIRM_MISSING visible process windows:");
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var processId);
                if (processId == (uint)Environment.ProcessId && IsWindowVisible(window))
                {
                    var name = new StringBuilder(96);
                    GetClassName(window, name, name.Capacity);
                    var title = new StringBuilder(192);
                    GetWindowText(window, title, title.Capacity);
                    Console.Error.WriteLine("T14_GUI_WINDOW hwnd=" + window + " class=" + name + " title=" + title);
                }
                return true;
            }, IntPtr.Zero);
            throw new InvalidOperationException("Download confirmation did not appear.");
        }
        // Direct WM_COMMAND/IDYES is more reliable than a synthetic child-button
        // BM_CLICK on Windows Server desktop runners; this is test-only automation.
        if (!PostMessage(dialog, 0x0111, (IntPtr)6, IntPtr.Zero))
            throw new InvalidOperationException("Unable to send IDYES to engine confirmation dialog.");
        Console.WriteLine("T14_GUI_CONFIRM_SENT_YES");
    }

    private delegate bool EnumWindow(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr hwnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
