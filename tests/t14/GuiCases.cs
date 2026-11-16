using System.IO;
using System.Windows;
using System.Windows.Controls;
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
                var confirmationCount = 0;
                var manager = new EngineManagementWindow(activePack, installer, "", bundled,
                    confirmationForVerification: offer =>
                    {
                        if (offer.PackId != fixture.Pack.PackId)
                            throw new InvalidOperationException("Unexpected unverified engine offer in manager.");
                        return ++confirmationCount > 1;
                    }) { Owner = window };
                manager.Show();
                Stage("manager-visible");
                Until(() => manager.IsVisible);
                if (!((TextBox)manager.FindName("EngineDetailsTextBox")).Text.Contains(activePack.EngineInfo.PhpCompatibilityVersion)
                    || !((TextBox)manager.FindName("EngineTargetsTextBox")).Text.Contains(before[0]))
                    throw new InvalidOperationException("Current engine/version information is not visible.");

                var updateButton = (Button)manager.FindName("CheckEngineUpdateButton");
                var status = (TextBlock)manager.FindName("EngineUpdateStatusTextBlock");
                Stage("manager-approval-declined");
                updateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => status.Text == "다운로드를 시작하지 않았습니다." && updateButton.IsEnabled);
                if (confirmationCount != 1 || Directory.Exists(managed))
                    throw new InvalidOperationException("Unapproved engine download was not refused.");

                Stage("manager-approved-and-installing");
                updateButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Until(() => (manager.RestartRequired || status.Text.Contains("못했습니다", StringComparison.Ordinal))
                    && ((Button)manager.FindName("CloseEngineManagementButton")).IsEnabled);
                if (confirmationCount != 2)
                    throw new InvalidOperationException("Approved update was not confirmed exactly once.");
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

}
