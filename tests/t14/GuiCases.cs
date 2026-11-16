using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Dk.PhpCompatibilityChecker;
using Dk.PhpCompatibilityChecker.Models;

internal static class GuiCases
{
    internal static async Task Verify(string packs, string output)
    {
        var fixtureArea = Path.Combine(output, "gui-fixture");
        Directory.CreateDirectory(fixtureArea);
        var fixture = await PackFixture.CreateAsync(packs, fixtureArea, "full");
        var managed = Path.Combine(output, "gui-managed");
        var bundled = Path.Combine(packs, "reduced");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { Run(fixture, bundled, managed); completion.SetResult(); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromMinutes(2));
    }

    private static void Run(PackFixture fixture, string bundled, string managed)
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        MainWindow? window = null;
        try
        {
            window = new MainWindow(new ScanPresentation(), bundled, managed, fixture.CreateInstaller(managed));
            window.Show();
            Until(() => window.Presentation.Targets.Count > 0);
            var before = window.Presentation.Targets.Select(target => target.Version).ToArray();
            var button = (Button)window.FindName("ManageEngineButton");
            if (!button.IsEnabled || before.Length >= fixture.Pack.Targets.Count)
                throw new InvalidOperationException("Reduced bundled pack and engine management entry were not shown.");
            Exception? managerFailure = null;
            // ShowDialog starts a nested dispatcher. Exercise its real controls on that dispatcher.
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                EngineManagementWindow? manager = null;
                try
                {
                    manager = Application.Current.Windows.OfType<EngineManagementWindow>().Single(w => w.IsVisible);
                    if (!((TextBox)manager.FindName("EngineDetailsTextBox")).Text.Contains(fixture.Pack.EngineInfo.PhpCompatibilityVersion)
                        || !((TextBox)manager.FindName("EngineTargetsTextBox")).Text.Contains(before[0]))
                        throw new InvalidOperationException("Active engine and verified target information were not visible.");

                    var confirmation = Task.Run(() => ConfirmDownload(new WindowInteropHelper(manager).Handle));
                    ((Button)manager.FindName("CheckEngineUpdateButton"))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Until(() => confirmation.IsCompleted);
                    confirmation.GetAwaiter().GetResult();
                    var status = (TextBlock)manager.FindName("EngineUpdateStatusTextBlock");
                    Until(() => (manager.RestartRequired && ((Button)manager.FindName("CloseEngineManagementButton")).IsEnabled)
                        || (status.Text.Contains("못했습니다", StringComparison.Ordinal) && ((Button)manager.FindName("CloseEngineManagementButton")).IsEnabled));
                    if (!manager.RestartRequired || !status.Text.Contains("다시 실행", StringComparison.Ordinal)
                        || !before.SequenceEqual(window!.Presentation.Targets.Select(target => target.Version)))
                        throw new InvalidOperationException("Management window update failed: " + status.Text);
                    manager.Close();
                }
                catch (Exception ex)
                {
                    managerFailure = ex;
                    if (manager?.IsVisible == true)
                        manager.Close();
                }
            }));
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (managerFailure is not null)
                throw managerFailure;
            window.Close();
            Until(() => !window.IsVisible);

            // Omit the installer (no catalog transport exists) to verify offline startup.
            window = new MainWindow(new ScanPresentation(), bundled, managed, null);
            window.Show();
            Until(() => window.Presentation.Targets.Count > 0);
            if (!window.Presentation.Targets.Select(target => target.Version)
                .SequenceEqual(fixture.Pack.Targets.Select(target => target.Version)))
                throw new InvalidOperationException("A new window did not activate the installed verified targets.");
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
        IntPtr dialog = IntPtr.Zero;
        var deadline = DateTime.UtcNow.AddSeconds(25);
        while (dialog == IntPtr.Zero && DateTime.UtcNow < deadline)
        {
            EnumWindows((window, _) =>
            {
                var name = new StringBuilder(32);
                GetClassName(window, name, name.Capacity);
                if (IsWindowVisible(window) && GetWindow(window, 4) == owner && name.ToString() == "#32770")
                { dialog = window; return false; }
                return true;
            }, IntPtr.Zero);
            if (dialog == IntPtr.Zero) Thread.Sleep(25);
        }
        if (dialog == IntPtr.Zero)
            throw new InvalidOperationException("Download confirmation did not appear.");
        var yes = GetDlgItem(dialog, 6);
        if (yes == IntPtr.Zero || !PostMessage(yes, 0x00f5, IntPtr.Zero, IntPtr.Zero))
        {
            PostMessage(dialog, 0x0010, IntPtr.Zero, IntPtr.Zero);
            throw new InvalidOperationException("Download confirmation Yes button could not be activated.");
        }
    }

    private delegate bool EnumWindow(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, IntPtr state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetDlgItem(IntPtr hwnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
