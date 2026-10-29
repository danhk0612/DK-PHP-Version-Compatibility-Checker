using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using Dk.PhpCompatibilityChecker.Models;

namespace Dk.PhpCompatibilityChecker.Services;

/// <summary>Owns one isolated, read-only engine invocation at a time.</summary>
public sealed class ScanService : IAsyncDisposable
{
    private readonly string _packDirectory;
    private readonly object _gate = new();
    private readonly CancellationTokenSource _lifetime = new();
    private Task<ScanResult>? _active;
    private Task? _disposeTask;
    private Process? _process;
    private bool _disposed;

    public ScanService(string packDirectory) => _packDirectory = Path.GetFullPath(packDirectory);

    public Task<ScanResult> ScanAsync(ScanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_active is { IsCompleted: false })
                throw new InvalidOperationException("검사가 진행 중이므로 중복 실행할 수 없습니다.");
            _active = Task.Run(() => RunAsync(request, _lifetime.Token));
            return _active;
        }
    }

    private async Task<ScanResult> RunAsync(ScanRequest request, CancellationToken cancellationToken)
    {
        EnginePack? pack = null;
        IReadOnlyList<string> selected = Array.Empty<string>();
        IReadOnlyList<string> excluded = Array.Empty<string>();
        string? work = null;
        var ownsWork = false;
        ScanResult result;
        string? cleanupFailure = null;
        int? engineExitCode = null;
        string? engineError = null;
        try
        {
            var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.SourceDirectory));
            if (Overlaps(source, _packDirectory))
                throw new IOException("검사 소스와 엔진 팩 폴더가 겹칩니다.");
            SourceFileEnumerator.RejectReparseAncestors(source);
            pack = await new EnginePackLoader().LoadAsync(_packDirectory, cancellationToken).ConfigureAwait(false);
            if (!pack.Targets.Any(t => t.Version == request.TargetPhpVersion))
                throw new InvalidDataException("현재 엔진 팩으로 검증되지 않은 목표 PHP 버전입니다.");
            work = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "DK-PHP-Compatibility-Checker", Guid.NewGuid().ToString("N")));
            if (work.Any(char.IsControl) || Overlaps(work, source) || Overlaps(work, pack.RootDirectory))
                throw new IOException("임시 작업 폴더와 소스 또는 엔진 팩 폴더가 겹치거나 올바르지 않습니다.");
            SourceFileEnumerator.RejectReparseAncestors(work);
            var physicalSource = SourceFileEnumerator.PhysicalDirectoryPath(source);
            var physicalPack = SourceFileEnumerator.PhysicalDirectoryPath(pack.RootDirectory);
            var physicalWork = SourceFileEnumerator.PhysicalDirectoryPath(work, allowMissing: true);
            if (PhysicalOverlaps(physicalSource, physicalPack) || PhysicalOverlaps(physicalWork, physicalSource)
                || PhysicalOverlaps(physicalWork, physicalPack))
                throw new IOException("물리 경로 기준으로 소스·엔진 팩·임시 작업 폴더가 겹칩니다.");
            var enumeration = new SourceFileEnumerator().Enumerate(source, request.IncludeSubdirectories, cancellationToken);
            selected = enumeration.Files;
            excluded = enumeration.ExcludedPaths;
            if (selected.Count == 0)
                return new ScanResult(request, ScanStatus.NoFiles, 0, 0, 0, 0, Array.Empty<ScanIssue>(), pack.EngineInfo, null)
                { ExcludedPaths = excluded };
            if (Directory.Exists(work) || File.Exists(work))
                throw new IOException("임시 작업 경로가 이미 존재합니다.");
            Directory.CreateDirectory(work);
            ownsWork = true;
            SourceFileEnumerator.RejectReparseAncestors(work);
            var fileList = Path.Combine(work, "files.txt");
            await File.WriteAllLinesAsync(fileList, selected, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            // Recheck the approved inputs immediately before starting the external reader.
            foreach (var file in selected)
                SourceFileEnumerator.RejectReparseAncestors(file);
            var start = new ProcessStartInfo
            {
                FileName = pack.PhpExecutable,
                WorkingDirectory = pack.EngineDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(false, true),
                StandardErrorEncoding = Encoding.UTF8
            };
            // PHPCS checks extension keys case-sensitively. Preserve only the
            // actual spellings already approved by our php/inc-only enumerator.
            var extensions = new[] { "php", "inc" }
                .Concat(selected.Select(file => Path.GetExtension(file)[1..]))
                .Distinct(StringComparer.Ordinal);
            foreach (var argument in new[] { "-n", pack.PhpcsEntryPoint, "--standard=" + pack.RulesetPath,
                "--runtime-set", "testVersion", request.TargetPhpVersion, "--report=json", "-q", "--no-cache",
                "--ignore-annotations", "--extensions=" + string.Join(",", extensions), "--file-list=" + fileList })
                start.ArgumentList.Add(argument);
            start.Environment["PHPRC"] = "";
            start.Environment["PHP_INI_SCAN_DIR"] = "";
            using var process = new Process { StartInfo = start };
            lock (_gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                process.Start();
                _process = process;
            }
            // Drain raw streams concurrently; decode only after both pipes are closed.
            // A decoder failure must not leave a full pipe blocking the child process.
            var output = ReadBytesAsync(process.StandardOutput.BaseStream);
            var error = ReadBytesAsync(process.StandardError.BaseStream);
            try
            {
                try
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    Kill(process);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                    await Task.WhenAll(output, error).ConfigureAwait(false);
                    throw;
                }
                await Task.WhenAll(output, error).ConfigureAwait(false);
                engineExitCode = process.ExitCode;
                engineError = new UTF8Encoding(false, true).GetString(await error.ConfigureAwait(false));
                cancellationToken.ThrowIfCancellationRequested();
                result = new PhpcsReportParser().Parse(request, selected, pack.EngineInfo, engineExitCode.Value,
                    new UTF8Encoding(false, true).GetString(await output.ConfigureAwait(false)), engineError);
            }
            finally
            {
                lock (_gate)
                    if (ReferenceEquals(_process, process))
                        _process = null;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or Win32Exception or OperationCanceledException or NotSupportedException)
        {
            result = new ScanResult(request, ScanStatus.Failed, selected.Count, null, 0, 0,
                Array.Empty<ScanIssue>(), pack?.EngineInfo,
                ex is OperationCanceledException ? "종료 요청으로 검사가 완료되지 않았습니다." : ex.Message)
            { EngineExitCode = engineExitCode, EngineStandardError = engineError };
        }
        finally
        {
            if (ownsWork && work is not null && Directory.Exists(work))
            {
                try { Directory.Delete(work, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { cleanupFailure = "임시 작업 파일 정리에 실패했습니다: " + ex.Message; }
            }
        }
        result = result with { ExcludedPaths = excluded };
        if (cleanupFailure is not null)
            result = result with { Status = ScanStatus.Failed,
                FailureMessage = string.IsNullOrEmpty(result.FailureMessage) ? cleanupFailure : result.FailureMessage + " " + cleanupFailure };
        return result;
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is null)
            {
                _disposed = true;
                _lifetime.Cancel();
                if (_process is not null)
                    Kill(_process);
                _disposeTask = DisposeCoreAsync(_active);
            }
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync(Task<ScanResult>? active)
    {
        try
        {
            if (active is not null)
                await active.ConfigureAwait(false);
        }
        finally { _lifetime.Dispose(); }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (Win32Exception) when (process.HasExited) { }
    }

    private static async Task<byte[]> ReadBytesAsync(Stream stream)
    {
        using var output = new MemoryStream();
        await stream.CopyToAsync(output).ConfigureAwait(false);
        return output.ToArray();
    }

    private static bool PhysicalOverlaps(string first, string second)
    {
        first = first.TrimEnd('\\');
        second = second.TrimEnd('\\');
        return first.Equals(second, StringComparison.OrdinalIgnoreCase)
            || first.StartsWith(second + "\\", StringComparison.OrdinalIgnoreCase)
            || second.StartsWith(first + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Overlaps(string first, string second)
    {
        first = Path.TrimEndingDirectorySeparator(Path.GetFullPath(first));
        second = Path.TrimEndingDirectorySeparator(Path.GetFullPath(second));
        static string Prefix(string path) => Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
        return first.Equals(second, StringComparison.OrdinalIgnoreCase)
            || first.StartsWith(Prefix(second), StringComparison.OrdinalIgnoreCase)
            || second.StartsWith(Prefix(first), StringComparison.OrdinalIgnoreCase);
    }
}
