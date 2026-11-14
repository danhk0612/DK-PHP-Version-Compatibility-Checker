using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dk.PhpCompatibilityChecker.Models;
using Dk.PhpCompatibilityChecker.Services;

internal sealed record PackFixture(string ArchivePath, string ArchiveHash, string ManifestHash, EnginePack Pack,
    string RuntimeVersion, JsonElement EngineVersions, PackFixture.OfferTarget[] Targets)
{
    private static readonly Uri CatalogUri = new("https://fixture.test/catalog-v1.json");
    private static readonly Uri AssetUri = new("https://fixture.test/pack.zip");

    public EnginePackInstaller CreateInstaller(string managed) => CreateInstaller(managed, CreateTransport());

    internal EnginePackInstaller CreateInstaller(string managed, FixtureTransport transport) =>
        new(managed, CatalogUri, transport);

    internal FixtureTransport CreateTransport(Func<Stream>? openAsset = null) => new(this, openAsset);

    public static async Task<PackFixture> CreateAsync(string packs, string output, string label)
    {
        var root = Path.Combine(packs, label);
        var pack = await new EnginePackLoader().LoadAsync(root);
        var manifestPath = Path.Combine(root, "manifest.json");
        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(manifestPath));
        var runtimeVersion = manifest.RootElement.GetProperty("runtime").GetProperty("version").GetString()!;
        var engineVersions = manifest.RootElement.GetProperty("engine").GetProperty("versions").Clone();
        var path = Path.Combine(output, label + ".zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                zip.CreateEntryFromFile(file, "runtime/" + relative, CompressionLevel.Fastest);
            }
        }
        return new PackFixture(path, Hash(path), Hash(manifestPath), pack, runtimeVersion, engineVersions,
            pack.Targets.Select(target => new OfferTarget(target.Version, target.Limitations)).ToArray());
    }

    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    internal sealed record OfferTarget(string Version, IReadOnlyList<string> Limitations);

    internal sealed class FixtureTransport(PackFixture asset, Func<Stream>? openAsset = null) : HttpMessageHandler
    {
        public int AssetRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Method != HttpMethod.Get)
                throw new InvalidOperationException("Unexpected HTTP method");
            HttpContent content;
            if (request.RequestUri == CatalogUri)
            {
                var catalog = new
                {
                    schemaVersion = 1,
                    packs = new[]
                    {
                        new
                        {
                            packId = asset.Pack.PackId,
                            packSchemaVersion = 1,
                            reportProfile = asset.Pack.ReportProfile,
                            assetUrl = AssetUri.AbsoluteUri,
                            sourceUrl = "https://fixture.test/source.zip",
                            archiveBytes = new FileInfo(asset.ArchivePath).Length,
                            archiveSha256 = asset.ArchiveHash,
                            manifestSha256 = asset.ManifestHash,
                            phpRuntimeVersion = asset.RuntimeVersion,
                            engineVersions = asset.EngineVersions,
                            targets = asset.Targets.Select(target => new { version = target.Version, limitations = target.Limitations })
                        }
                    }
                };
                content = new StringContent(JsonSerializer.Serialize(catalog), Encoding.UTF8, "application/json");
            }
            else
            {
                if (request.RequestUri != AssetUri)
                    throw new InvalidOperationException("Unexpected HTTP destination: " + request.RequestUri);
                AssetRequests++;
                content = new StreamContent(openAsset is null ? File.OpenRead(asset.ArchivePath) : openAsset());
                content.Headers.ContentLength = new FileInfo(asset.ArchivePath).Length;
                content.Headers.ContentType = new("application/zip");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = content
            });
        }
    }
}
