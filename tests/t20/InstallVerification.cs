using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Dk.PhpCompatibilityChecker;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

if (args.Length == 4 && args[0] == "--check-installed")
{
    var active = await EnginePackInstaller.LoadInstalledAsync(args[1]);
    if (active is null || active.PackId != args[2] ||
        !active.Targets.Select(x => x.Version).SequenceEqual(args[3].Split(',')))
        throw new InvalidDataException("A separate process did not load the installed engine pack.");
    return;
}
if (args.Length == 4 && args[0] == "--check-restored")
{
    if (await EnginePackInstaller.LoadInstalledAsync(args[1]) is not null)
        throw new InvalidDataException("A separate process still sees the managed engine after restore.");
    var bundledAfterRestore = await new EnginePackLoader().LoadAsync(args[2]);
    if (bundledAfterRestore.PackId != args[3])
        throw new InvalidDataException("A separate process cannot load the unchanged bundled engine.");
    return;
}
if (args.Length == 6 && (args[0] == "--check-wpf-installed" || args[0] == "--check-wpf-restored"))
{
    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var worker = new Thread(() =>
    {
        try
        {
            VerifyWpfStartup(args[1], args[2], args[3], args[4], args[5].Split(','));
            completion.SetResult();
        }
        catch (Exception ex) { completion.SetException(ex); }
    });
    worker.SetApartmentState(ApartmentState.STA);
    worker.Start();
    await completion.Task.WaitAsync(TimeSpan.FromMinutes(1));
    return;
}
if (args.Length != 3)
    throw new ArgumentException("Usage: T20.InstallVerification <runtime> <engine-candidate> <output>");
var manifestPath = Path.Combine(args[0], "manifest.json");
var candidateRoot = args[1];
var output = args[2];
using var manifestDoc = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath));
using var candidateDoc = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(candidateRoot, "engine-candidate.json")));
var manifest = manifestDoc.RootElement;
var candidate = candidateDoc.RootElement;
if (!candidate.GetProperty("scopePassed").GetBoolean() || candidate.GetProperty("publicReleaseApproved").GetBoolean())
    throw new InvalidDataException("Only an unpublished, locally verified engine candidate may be tested.");
var archive = Path.Combine(candidateRoot, candidate.GetProperty("archive").GetString()!);
var expectedTargets = manifest.GetProperty("targets").EnumerateArray()
    .Select(row => row.GetProperty("version").GetString()!).ToArray();

// The sequence is a controlled fixture value, not a real releaseSequence or GitHub publication.
var offer = new
{
    packId = manifest.GetProperty("packId").GetString(),
    releaseSequence = 1,
    packSchemaVersion = 1,
    reportProfile = manifest.GetProperty("reportProfile").GetString(),
    assetUrl = "https://fixture.test/engine.zip",
    sourceUrl = "https://fixture.test/source.zip",
    archiveBytes = candidate.GetProperty("archiveBytes").GetInt64(),
    archiveSha256 = candidate.GetProperty("archiveSha256").GetString(),
    manifestSha256 = candidate.GetProperty("manifestSha256").GetString(),
    phpRuntimeVersion = manifest.GetProperty("runtime").GetProperty("version").GetString(),
    engineVersions = manifest.GetProperty("engine").GetProperty("versions").Clone(),
    targets = manifest.GetProperty("targets").EnumerateArray()
        .Select(row => new
        {
            version = row.GetProperty("version").GetString(),
            limitations = row.GetProperty("limitations").Clone()
        }).ToArray()
};
var catalog = JsonSerializer.Serialize(new { schemaVersion = 2, packs = new[] { offer } });
var managed = Path.Combine(output, "installed");
if (Directory.Exists(output))
    throw new IOException("Preserve earlier engine installation evidence; use a new directory.");
Directory.CreateDirectory(output);
using var installer = new EnginePackInstaller(managed,
    new Uri("https://fixture.test/catalog-v2.json"), new LocalTransport(catalog, archive));
var offers = await installer.ReadCatalogAsync();
if (offers.Count != 1)
    throw new InvalidDataException("Expected one controlled engine candidate.");
var installed = await installer.InstallAsync(offers[0], "");
if (installed.PackId != offer.packId ||
    !installed.Targets.Select(x => x.Version).SequenceEqual(expectedTargets))
    throw new InvalidDataException("Installed candidate identity or PHP targets differ.");
var restarted = await EnginePackInstaller.LoadInstalledAsync(managed);
if (restarted is null || restarted.PackId != offer.packId ||
    !restarted.Targets.Select(x => x.Version).SequenceEqual(expectedTargets))
    throw new InvalidDataException("Installed candidate was not available on a new loader.");
await VerifyInSeparateProcessAsync("--check-installed", managed, offer.packId!, string.Join(",", expectedTargets));
await VerifyInSeparateProcessAsync("--check-wpf-installed", managed, args[0], installed.RootDirectory,
    installed.PackId, string.Join(",", expectedTargets));
var bundled = await new EnginePackLoader().LoadAsync(args[0]);
if (!bundled.Targets.Select(x => x.Version).SequenceEqual(expectedTargets))
    throw new InvalidDataException("Bundled PHP targets differ from the verified candidate.");
await installer.RestoreBundledAsync("");
if (await EnginePackInstaller.LoadInstalledAsync(managed) is not null)
    throw new InvalidDataException("Restoring the bundled engine left a managed pack active.");
var restoredBundled = await new EnginePackLoader().LoadAsync(args[0]);
if (restoredBundled.PackId != bundled.PackId ||
    !restoredBundled.Targets.Select(x => x.Version).SequenceEqual(expectedTargets))
    throw new InvalidDataException("Bundled engine changed during install or restore.");
await VerifyInSeparateProcessAsync("--check-restored", managed, args[0], bundled.PackId);
await VerifyInSeparateProcessAsync("--check-wpf-restored", managed, args[0], bundled.RootDirectory,
    bundled.PackId, string.Join(",", expectedTargets));
var summary = new
{
    scopePassed = true,
    publicReleaseVerified = false,
    sourceSha256 = candidate.GetProperty("sourceArchiveSha256").GetString(),
    packId = installed.PackId,
    targets = expectedTargets,
    archiveSha256 = candidate.GetProperty("archiveSha256").GetString(),
    installedAndReloaded = true,
    freshProcessInstalledVerified = true,
    freshProcessBundledRestoredVerified = true,
    freshProcessWpfManagedPackVerified = true,
    freshProcessWpfBundledRestoreVerified = true,
    appRestartVerified = false,
    restoredBundledWithoutChangingTargets = true
};
await File.WriteAllTextAsync(Path.Combine(output, "summary.json"),
    JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine("T20_LOCAL_INSTALL_JSON=" + JsonSerializer.Serialize(summary));

// Exercise the real MainWindow pack-selection code on an independent WPF STA
// process, using a test-only managed root; this is NOT the packaged apphost EXE.
static void VerifyWpfStartup(string managedRoot, string bundledRoot, string expectedRoot,
    string expectedPackId, string[] expectedTargets)
{
    var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    SynchronizationContext.SetSynchronizationContext(
        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
    MainWindow? window = null;
    try
    {
        window = new MainWindow(new ScanPresentation(), bundledRoot, managedRoot, null);
        window.Show();
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (window.Presentation.Targets.Count != expectedTargets.Length
            && DateTime.UtcNow < deadline)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
            Thread.Sleep(15);
        }
        if (!window.IsVisible || !window.Presentation.Targets.Select(x => x.Version)
                .SequenceEqual(expectedTargets))
            throw new InvalidDataException("WPF startup did not show the verified target list.");

        var loadedPack = typeof(MainWindow).GetField("_activePack",
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window) as EnginePack;
        if (loadedPack is null || loadedPack.PackId != expectedPackId
            || !string.Equals(Path.GetFullPath(loadedPack.RootDirectory),
                Path.GetFullPath(expectedRoot), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("WPF startup chose the wrong managed or bundled engine root.");

        var summary = (System.Windows.Controls.TextBlock)window.FindName("EngineSummaryTextBlock");
        if (!summary.Text.Contains(loadedPack.EngineInfo.PhpCompatibilityVersion,
                StringComparison.Ordinal))
            throw new InvalidDataException("WPF main screen did not display the loaded engine information.");
        Console.WriteLine("T20_WPF_STARTUP_JSON=" + JsonSerializer.Serialize(new {
            loadedPack.PackId, loadedPack.RootDirectory,
            targets = expectedTargets, managerEnabled = window.IsVisible
        }));
    }
    finally
    {
        window?.Close();
        application.Shutdown();
    }
}

static async Task VerifyInSeparateProcessAsync(params string[] childArgs)
{
    var start = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    foreach (var argument in childArgs)
        start.ArgumentList.Add(argument);
    using var child = Process.Start(start) ?? throw new IOException("Cannot start the verification process.");
    var stdout = child.StandardOutput.ReadToEndAsync();
    var stderr = child.StandardError.ReadToEndAsync();
    await child.WaitForExitAsync();
    if (child.ExitCode != 0)
        throw new InvalidDataException("Separate-process verification failed: " +
            await stdout + " " + await stderr);
}

internal sealed class LocalTransport(string catalog, string zipPath) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HttpContent body;
        if (request.Method != HttpMethod.Get)
            throw new InvalidOperationException("Only controlled GET requests are allowed.");
        if (request.RequestUri?.AbsoluteUri == "https://fixture.test/catalog-v2.json")
            body = new StringContent(catalog, Encoding.UTF8, "application/json");
        else if (request.RequestUri?.AbsoluteUri == "https://fixture.test/engine.zip")
        {
            body = new StreamContent(File.OpenRead(zipPath));
            body.Headers.ContentLength = new FileInfo(zipPath).Length;
        }
        else
            throw new InvalidOperationException("Unexpected transport URL.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = body });
    }
}
