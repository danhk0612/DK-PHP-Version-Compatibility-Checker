namespace Dk.PhpCompatibilityChecker.Models;

public sealed record ScanRequest(
    string SourceDirectory,
    string TargetPhpVersion,
    bool IncludeSubdirectories,
    IReadOnlyList<string>? ExcludedPaths = null);
