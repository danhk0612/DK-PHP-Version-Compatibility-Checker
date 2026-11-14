namespace Dk.PhpCompatibilityChecker.Models;

public sealed record EnginePack(
    string RootDirectory,
    string PackId,
    string ReportProfile,
    string PhpExecutable,
    string PhpcsEntryPoint,
    string RulesetPath,
    string EngineDirectory,
    IReadOnlyList<PhpTarget> Targets,
    ScanEngineInfo EngineInfo);
