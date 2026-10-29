using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Dk.PhpCompatibilityChecker.Services;

public sealed record SourceFileEnumeration(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> ExcludedPaths);

/// <summary>Enumerates only ordinary source files; never follows reparse points.</summary>
public sealed class SourceFileEnumerator
{
    public SourceFileEnumeration Enumerate(string directory, bool includeSubdirectories,
        CancellationToken cancellationToken = default)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("검사할 소스 폴더가 없습니다.");
        if (root.Any(char.IsControl))
            throw new IOException("소스 경로에 제어 문자가 있습니다.");
        RejectReparseAncestors(root);
        var files = new List<string>();
        var excluded = new List<string>();
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var entry in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Any(char.IsControl))
                    throw new IOException("파일 목록에 안전하게 표현할 수 없는 경로입니다: " + entry);
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    excluded.Add(Path.GetFullPath(entry));
                    continue;
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (includeSubdirectories)
                        pending.Push(entry);
                    continue;
                }
                var extension = Path.GetExtension(entry);
                if (!extension.Equals(".php", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".inc", StringComparison.OrdinalIgnoreCase))
                    continue;
                // Do not silently approve files that cannot be read. Nothing is written.
                using (File.Open(entry, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { }
                files.Add(Path.GetFullPath(entry));
            }
        }
        files.Sort(StringComparer.OrdinalIgnoreCase);
        excluded.Sort(StringComparer.OrdinalIgnoreCase);
        if (files.Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
            throw new IOException("대소문자가 충돌하는 소스 파일 경로가 있습니다.");
        return new SourceFileEnumeration(files.AsReadOnly(), excluded.AsReadOnly());
    }

    // Compare NT device paths rather than drive letters/UNC/SUBST aliases. For a
    // not-yet-created work folder, resolve the nearest existing parent first.
    internal static string PhysicalDirectoryPath(string path, bool allowMissing = false)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("물리 경로 검증에는 Windows가 필요합니다.");
        var current = Path.GetFullPath(path);
        var suffix = new Stack<string>();
        FileAttributes attributes;
        while (true)
        {
            try
            {
                attributes = File.GetAttributes(current);
                break;
            }
            catch (Exception ex) when (allowMissing && (ex is FileNotFoundException or DirectoryNotFoundException))
            {
                var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(current));
                var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
                if (string.IsNullOrEmpty(name) || parent is null)
                    throw new IOException("물리 경로의 기존 상위 폴더를 확인할 수 없습니다.", ex);
                suffix.Push(name);
                current = parent;
            }
        }
        if ((attributes & FileAttributes.Directory) == 0)
            throw new IOException("물리 경로 검증 대상이 폴더가 아닙니다: " + current);
        RejectReparseAncestors(current);
        // OPEN_EXISTING + zero desired access only reads metadata; it cannot create
        // or modify a file. BACKUP_SEMANTICS permits opening a directory handle.
        using var handle = CreateFileW(current, 0, 0x00000007, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new IOException("물리 폴더 경로를 확인할 수 없습니다: " + current,
                new Win32Exception(Marshal.GetLastWin32Error()));
        var buffer = new StringBuilder(512);
        while (true)
        {
            // VOLUME_NAME_NT (0x2), FILE_NAME_NORMALIZED (0x0).
            var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0x2);
            if (length == 0)
                throw new IOException("물리 폴더 경로를 정규화할 수 없습니다: " + current,
                    new Win32Exception(Marshal.GetLastWin32Error()));
            if (length >= buffer.Capacity)
            {
                buffer.Capacity = checked((int)length + 1);
                continue;
            }
            var physical = buffer.ToString().TrimEnd('\\');
            if (physical.Length == 0)
                throw new IOException("정규화된 물리 폴더 경로가 비어 있습니다.");
            foreach (var name in suffix)
                physical += "\\" + name;
            return physical;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path,
        uint pathLength, uint flags);

    internal static void RejectReparseAncestors(string path)
    {
        for (string? current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if (!Directory.Exists(current) && !File.Exists(current))
                continue;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("링크 또는 reparse point를 포함한 경로는 사용할 수 없습니다: " + current);
        }
    }
}
