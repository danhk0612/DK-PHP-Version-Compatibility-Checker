namespace Dk.PhpCompatibilityChecker.Models;

public sealed record PhpTarget(
    string Version,
    string VerificationRef,
    IReadOnlyList<string> Limitations);
