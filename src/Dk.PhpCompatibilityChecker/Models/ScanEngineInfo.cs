namespace Dk.PhpCompatibilityChecker.Models;

public sealed record ScanEngineInfo(
    string PackId,
    string PhpRuntimeVersion,
    string PhpCompatibilityVersion,
    string PhpCodeSnifferVersion,
    string PhpcsUtilsVersion);
