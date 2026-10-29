namespace Dk.PhpCompatibilityChecker.Models;

public sealed record ScanResult(
    ScanRequest Request,
    ScanStatus Status,
    int SelectedFileCount,
    int? ProcessedFileCount,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ScanIssue> Issues,
    ScanEngineInfo? EngineInfo,
    string? FailureMessage)
{
    public IReadOnlyList<string> ExcludedPaths { get; init; } = Array.Empty<string>();
    public int? EngineExitCode { get; init; }
    public string? EngineStandardError { get; init; }
}
