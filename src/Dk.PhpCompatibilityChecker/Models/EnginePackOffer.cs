namespace Dk.PhpCompatibilityChecker.Models;

public sealed record EnginePackOffer(
    string PackId,
    int PackSchemaVersion,
    string ReportProfile,
    Uri AssetUrl,
    Uri SourceUrl,
    long ArchiveBytes,
    string ArchiveSha256,
    string ManifestSha256,
    string PhpRuntimeVersion,
    IReadOnlyDictionary<string, string> EngineVersions,
    IReadOnlyList<EnginePackOfferTarget> Targets);

public sealed record EnginePackOfferTarget(string Version, IReadOnlyList<string> Limitations);
