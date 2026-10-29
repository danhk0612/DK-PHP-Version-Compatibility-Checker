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
    string? FailureMessage);
