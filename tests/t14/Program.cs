using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Length != 2)
        {
            Console.Error.WriteLine("Usage on Windows x64: T14.Verification <derived T10 packs> <fresh output>");
            return 2;
        }

        if (args[0] == "--startup")
        {
            var pack = await EnginePackInstaller.LoadInstalledAsync(args[1])
                ?? throw new InvalidOperationException("No installed pack after restart");
            Console.WriteLine(JsonSerializer.Serialize(new { pack.PackId, Targets = Targets(pack) }));
            return 0;
        }

        var packs = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        Check(!Directory.Exists(output) && !File.Exists(output), "T14 requires fresh output");
        Directory.CreateDirectory(output);

        var passed = new List<string>();
        string? failure = null;
        try
        {
            await VerifyAsync(packs, output, passed);
            await GuiCases.Verify(packs, output);
            passed.Add("WPF download confirmation and restart");
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
            Console.Error.WriteLine(failure);
        }

        var report = new
        {
            scopePassed = failure is null,
            passed,
            failure,
            runId = Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),
            sourceSha = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            scope = "Windows installer service, controlled HTTPS responses, derived verified packs, independent startup and actual scan"
        };
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), json);
        Console.WriteLine("T14_SUMMARY_JSON=" + JsonSerializer.Serialize(report));
        return failure is null ? 0 : 1;
    }

    private static async Task VerifyAsync(string packs, string output, List<string> passed)
    {
        var inputBefore = Snapshot(packs);
        var managed = Path.Combine(output, "managed");
        var subject = Path.Combine(output, "한글 & subject");
        Directory.CreateDirectory(subject);
        await File.WriteAllTextAsync(Path.Combine(subject, "fixture.php"), "<?php\nmysql_query('test');\n");
        var subjectBefore = Snapshot(subject);
        var pointer = Path.Combine(managed, "active.json");

        var reduced = await PackFixture.CreateAsync(packs, output, "reduced");
        var full = await PackFixture.CreateAsync(packs, output, "full");
        var retainedTarget = Targets(reduced.Pack)[^1];
        var addedTarget = Targets(full.Pack).Except(Targets(reduced.Pack)).Last();

        Check(await EnginePackInstaller.LoadInstalledAsync(managed) is null, "Fresh root already has an active pack");
        var initial = await InstallAsync(reduced, managed, subject);
        Check(initial.PackId == reduced.Pack.PackId, "First installation returned the wrong pack");
        await VerifyStartupAsync(managed, reduced.Pack);
        passed.Add("Initial download verifies and loads in a new process");

        var previousPointer = await File.ReadAllBytesAsync(pointer);
        var managedBefore = Snapshot(managed);
        var originalPackFiles = Snapshot(initial.RootDirectory);
        await RejectAsync(full with { ArchiveHash = WrongHash(full.ArchiveHash) }, "wrong archive SHA-256");

        var traversalPath = Path.Combine(output, "traversal.zip");
        File.Copy(full.ArchivePath, traversalPath);
        using (var zip = ZipFile.Open(traversalPath, ZipArchiveMode.Update))
        using (var writer = new StreamWriter(zip.CreateEntry("runtime/../../../../t14-escaped.txt").Open()))
            writer.Write("must not extract");
        await RejectAsync(full with { ArchivePath = traversalPath, ArchiveHash = PackFixture.Hash(traversalPath) }, "ZIP path traversal");
        Check(!Directory.EnumerateFiles(output, "t14-escaped.txt", SearchOption.AllDirectories).Any(),
            "Traversal entry escaped the ZIP extraction root");

        await RejectAsync(full with { Targets = [.. full.Targets, new("99.0", ["Unverified"])] },
            "catalog targets differing from the verified manifest");
        await RejectAsync(full with { ManifestHash = WrongHash(full.ManifestHash) }, "wrong manifest SHA-256");

        var overlapTransport = full.CreateTransport();
        using (var installer = full.CreateInstaller(Path.Combine(subject, "managed"), overlapTransport))
        {
            var offer = (await installer.ReadCatalogAsync()).Single();
            await ExpectAsync<IOException>(() => installer.InstallAsync(offer, subject), "managed folder inside scan source");
            Check(overlapTransport.AssetRequests == 0 && subjectBefore.SequenceEqual(Snapshot(subject)),
                "Overlapping path wrote to the scan source or requested the asset");
            passed.Add("Managed folder inside the scan source is rejected before transfer or writes");
        }

        using (var cancellation = new CancellationTokenSource())
        await using (var blockedStream = new CancelableAssetStream(full.ArchivePath))
        {
            using var installer = full.CreateInstaller(managed, full.CreateTransport(() => blockedStream));
            var offer = (await installer.ReadCatalogAsync()).Single();
            var installation = installer.InstallAsync(offer, subject, cancellation.Token);
            try
            {
                var winner = await Task.WhenAny(blockedStream.WaitingForRead, installation)
                    .WaitAsync(TimeSpan.FromSeconds(20));
                if (winner == installation)
                    await installation;
                Check(winner != installation, "Installation completed before cancellation");
            }
            finally
            {
                cancellation.Cancel();
            }
            await ExpectAsync<OperationCanceledException>(() => installation.WaitAsync(TimeSpan.FromSeconds(20)),
                "cancellation during transfer");
            await CheckPreservedAsync();
            passed.Add("Cancellation during transfer preserves active pointer, pack and source");
        }

        await using var runningScanner = new ScanService(initial.RootDirectory);
        VerifyScan(await runningScanner.ScanAsync(new ScanRequest(subject, retainedTarget, true)), reduced.Pack.PackId);
        var installed = await InstallAsync(full, managed, subject);
        Check(installed.PackId == full.Pack.PackId, "Update returned the wrong pack");
        Check(!(await File.ReadAllBytesAsync(pointer)).SequenceEqual(previousPointer), "Update did not change the pointer");
        Check((await new EnginePackLoader().LoadAsync(initial.RootDirectory)).PackId == reduced.Pack.PackId,
            "Installation replaced the existing pack directory");
        Check(originalPackFiles.SequenceEqual(Snapshot(initial.RootDirectory)), "Installation changed previous pack files");
        VerifyScan(await runningScanner.ScanAsync(new ScanRequest(subject, retainedTarget, true)), reduced.Pack.PackId);
        Check((await runningScanner.ScanAsync(new ScanRequest(subject, addedTarget, true))).Status == ScanStatus.Failed,
            "A pre-existing scan service began accepting the newly installed target");
        await VerifyStartupAsync(managed, full.Pack);
        var next = await EnginePackInstaller.LoadInstalledAsync(managed)
            ?? throw new InvalidOperationException("No selected pack");
        await using var nextScanner = new ScanService(next.RootDirectory);
        VerifyScan(await nextScanner.ScanAsync(new ScanRequest(subject, addedTarget, true)), full.Pack.PackId);
        passed.Add("Next startup loads the new target and the existing scanner retains its original pack");

        var updatedPointer = await File.ReadAllBytesAsync(pointer);
        var installedManifest = Path.Combine(next.RootDirectory, "manifest.json");
        var originalManifest = await File.ReadAllBytesAsync(installedManifest);
        try
        {
            await File.AppendAllTextAsync(installedManifest, "\n");
            await VerifyStartupAsync(managed, reduced.Pack);
            Check(updatedPointer.SequenceEqual(await File.ReadAllBytesAsync(pointer)),
                "Falling back changed the activation pointer");
            passed.Add("A corrupted current pack falls back to the previous verified pack on startup");
        }
        finally
        {
            await File.WriteAllBytesAsync(installedManifest, originalManifest);
        }

        var expandedPointer = await File.ReadAllBytesAsync(pointer);
        using (var installer = reduced.CreateInstaller(managed))
        {
            var offer = (await installer.ReadCatalogAsync()).Single();
            await ExpectAsync<InvalidDataException>(() => installer.InstallAsync(offer, subject), "target downgrade");
        }
        Check(expandedPointer.SequenceEqual(await File.ReadAllBytesAsync(pointer)), "Downgrade changed the pointer");
        passed.Add("An older offer cannot drop a verified installed target");

        Check(subjectBefore.SequenceEqual(Snapshot(subject)), "Scan source changed");
        Check(inputBefore.SequenceEqual(Snapshot(packs)), "Derived input packs changed");
        passed.Add("Source and derived input packs remain byte-identical");

        async Task RejectAsync(PackFixture asset, string label)
        {
            await ExpectAsync<InvalidDataException>(() => InstallAsync(asset, managed, subject), label);
            await CheckPreservedAsync();
            passed.Add("Rejected " + label + " and preserved active pointer, pack and source");
        }

        async Task CheckPreservedAsync()
        {
            Check((await File.ReadAllBytesAsync(pointer)).SequenceEqual(previousPointer), "Active pointer changed");
            Check(managedBefore.SequenceEqual(Snapshot(managed)), "Managed pack files changed");
            Check(!Directory.EnumerateDirectories(managed, ".stage-*").Any(), "Installer left a staging directory");
            Check(subjectBefore.SequenceEqual(Snapshot(subject)), "Scan source changed");
            Check((await EnginePackInstaller.LoadInstalledAsync(managed))?.PackId == reduced.Pack.PackId,
                "The previous pack is no longer usable");
        }
    }

    private static async Task<EnginePack> InstallAsync(PackFixture asset, string managed, string subject)
    {
        var transport = asset.CreateTransport();
        using var installer = asset.CreateInstaller(managed, transport);
        var offer = (await installer.ReadCatalogAsync()).Single();
        Check(offer.PackId == asset.Pack.PackId, "Catalog pack ID differs from fixture");
        try
        {
            return await installer.InstallAsync(offer, subject);
        }
        finally
        {
            Check(transport.AssetRequests == 1, "Expected one controlled asset request");
        }
    }

    private static async Task VerifyStartupAsync(string managed, EnginePack expected)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Unknown test host executable");
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(typeof(Program).Assembly.Location);
        info.ArgumentList.Add("--startup");
        info.ArgumentList.Add(managed);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start child process");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw;
        }

        Check(process.ExitCode == 0, "Startup failed: " + await stderr);
        using var report = JsonDocument.Parse(await stdout);
        Check(report.RootElement.GetProperty("PackId").GetString() == expected.PackId, "Startup selected the wrong pack");
        var actualTargets = report.RootElement.GetProperty("Targets").EnumerateArray().Select(item => item.GetString());
        Check(actualTargets.SequenceEqual(Targets(expected)), "Startup loaded the wrong targets");
    }

    private static void VerifyScan(ScanResult result, string packId)
    {
        Check(result.Status == ScanStatus.Completed && result.ProcessedFileCount == 1,
            "Actual scan failed: " + result.FailureMessage);
        Check(result.EngineInfo?.PackId == packId, "Scan used the wrong pack");
        Check(result.Issues.Any(issue =>
            issue.Source.StartsWith("PHPCompatibility.FunctionUse.RemovedFunctions.mysql_query", StringComparison.Ordinal)),
            "Actual scan missed the removed function");
    }

    private static async Task ExpectAsync<TException>(Func<Task> action, string label) where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(TException).Name + " for " + label);
    }

    private static string[] Targets(EnginePack pack) => pack.Targets.Select(target => target.Version).ToArray();

    private static SortedDictionary<string, string> Snapshot(string root)
    {
        var snapshot = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            snapshot.Add(Path.GetRelativePath(root, directory) + "/", "");
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            snapshot.Add(Path.GetRelativePath(root, file), PackFixture.Hash(file));
        return snapshot;
    }

    private static string WrongHash(string hash) => (hash[0] == '0' ? "1" : "0") + hash[1..];

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CancelableAssetStream(string path) : Stream
    {
        private readonly FileStream _file = File.OpenRead(path);
        private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _sentFirstChunk;

        public Task WaitingForRead => _waiting.Task;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sentFirstChunk)
            {
                _sentFirstChunk = true;
                return await _file.ReadAsync(buffer[..Math.Min(buffer.Length, 32)], cancellationToken);
            }

            _waiting.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _file.Dispose();
            base.Dispose(disposing);
        }
    }
}
