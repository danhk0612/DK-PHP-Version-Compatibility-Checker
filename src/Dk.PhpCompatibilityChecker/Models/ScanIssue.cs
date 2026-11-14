namespace Dk.PhpCompatibilityChecker.Models;

public sealed record ScanIssue(
    string FilePath,
    int Line,
    int Column,
    string Type,
    int Severity,
    string Source,
    string Message,
    bool Fixable);
