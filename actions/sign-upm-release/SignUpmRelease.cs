#:package Octokit@14.0.0

using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Octokit;

try
{
    var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
    var packagePath = Environment.GetEnvironmentVariable("PACKAGE_PATH");
    var tag = Environment.GetEnvironmentVariable("RELEASE_TAG");
    var organization = Environment.GetEnvironmentVariable("UPM_ORG_ID");
    var token = Environment.GetEnvironmentVariable("GH_TOKEN");
    var repository = Environment.GetEnvironmentVariable("GH_REPO");
    var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");

    ArgumentException.ThrowIfNullOrWhiteSpace(workspace);
    ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
    ArgumentException.ThrowIfNullOrWhiteSpace(tag);
    ArgumentException.ThrowIfNullOrWhiteSpace(organization);
    ArgumentException.ThrowIfNullOrWhiteSpace(token);
    ArgumentException.ThrowIfNullOrWhiteSpace(repository);
    ArgumentException.ThrowIfNullOrWhiteSpace(output);

    var (owner, repositoryName) = ParseRepository(repository);
    var package = Path.GetFullPath(Path.Combine(workspace, packagePath));
    var manifest = await ReadManifest(package);
    ValidateManifest(manifest, tag);

    var client = new GitHubClient(new ProductHeaderValue("sign-upm-release-action"))
    {
        Credentials = new Credentials(token),
    };
    var release = await GetRelease(client, owner, repositoryName, tag);
    var dist = await PackAndSign(package, organization);
    var archive = FindArchive(dist);
    await ValidateArchive(archive, manifest);

    var archiveName = Path.GetFileName(archive);
    await UploadArchive(client, release, archive, archiveName);
    await SaveArchiveAndWriteOutput(workspace, archive, archiveName, output);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}

static (string Owner, string RepositoryName) ParseRepository(string repository)
{
    var repositoryParts = (Span<System.Range>)stackalloc System.Range[3];
    var count = repository.AsSpan().Split(repositoryParts, '/');
    if (count != 2
        || repository.AsSpan(repositoryParts[0]).IsWhiteSpace()
        || repository.AsSpan(repositoryParts[1]).IsWhiteSpace())
    {
        throw new InvalidOperationException("repository must be in owner/name format.");
    }

    return (repository[repositoryParts[0]], repository[repositoryParts[1]]);
}

static async Task<UnityPackageManifest> ReadManifest(string package)
{
    await using var source = File.OpenRead(Path.Combine(package, "package.json"));
    return await JsonSerializer.DeserializeAsync(source, UnityPackageManifestJsonContext.Default.UnityPackageManifest)
        ?? throw new InvalidDataException("Unity package manifest is null.");
}

static void ValidateManifest(UnityPackageManifest manifest, string tag)
{
    if (string.IsNullOrWhiteSpace(manifest.Name) || string.IsNullOrWhiteSpace(manifest.Version))
    {
        throw new InvalidDataException("package.json must contain nonempty name and version strings.");
    }

    if (tag != manifest.Version && !(tag.StartsWith('v') && tag.AsSpan(1).SequenceEqual(manifest.Version)))
    {
        throw new InvalidOperationException("Release tag must match package.json version (optional v prefix).");
    }
}

static Task<Release> GetRelease(GitHubClient client, string owner, string repositoryName, string tag) =>
    client.Repository.Release.Get(owner, repositoryName, tag);

static async Task<string> PackAndSign(string package, string organization)
{
    // パッケージルートが workspace の場合にも、生成物が pack 対象へ混入しない場所を使う。
    var dist = Directory.CreateTempSubdirectory("sign-upm-").FullName;
    var start = new ProcessStartInfo("upm")
    {
        ArgumentList = { "pack", package, "--organization-id", organization, "--destination", dist },
    };

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Unity UPM CLI.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Unity UPM CLI failed with exit code {process.ExitCode}.");
    }

    return dist;
}

static string FindArchive(string dist) =>
    Directory.EnumerateFiles(dist)
        .SingleOrDefault(static path => path.EndsWith(".tgz", StringComparison.Ordinal) || path.EndsWith(".tar.gz", StringComparison.Ordinal)) 
    ?? throw new InvalidOperationException("Signed archive was not generated.");

static async Task ValidateArchive(string archive, UnityPackageManifest expected)
{
    await using var file = File.OpenRead(archive);
    await using var gzip = new GZipStream(file, CompressionMode.Decompress);
    await using var tar = new TarReader(gzip);
    var foundManifest = false;
    var foundAttestation = false;

    while (await tar.GetNextEntryAsync() is { } entry)
    {
        if (entry.Name is not ("package/package.json" or "package/.attestation.p7m"))
        {
            continue;
        }

        if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null)
        {
            throw new InvalidOperationException($"Expected a regular file: {entry.Name}");
        }

        if (entry.Name == "package/package.json")
        {
            if (foundManifest)
            {
                throw new InvalidOperationException("Duplicate package/package.json.");
            }

            var manifest = await JsonSerializer.DeserializeAsync(entry.DataStream, UnityPackageManifestJsonContext.Default.UnityPackageManifest)
                ?? throw new InvalidDataException("Packed Unity package manifest is null.");
            if (manifest.Name != expected.Name || manifest.Version != expected.Version)
            {
                throw new InvalidOperationException("Packed metadata does not match source.");
            }

            foundManifest = true;
        }
        else
        {
            if (foundAttestation || entry.Length == 0)
            {
                throw new InvalidOperationException("Duplicate or empty signing attestation.");
            }

            foundAttestation = true;
        }
    }

    if (!foundManifest || !foundAttestation)
    {
        throw new InvalidOperationException("Archive must contain package/package.json and package/.attestation.p7m.");
    }
}

static async Task UploadArchive(GitHubClient client, Release release, string archive, string archiveName)
{
    // 同名 asset は API が 422 で拒否する。削除・置換や POST の自動再試行はしない。
    await using var stream = File.OpenRead(archive);
    var releaseAssetUpload = new ReleaseAssetUpload
    {
        FileName = archiveName,
        ContentType = "application/gzip",
        RawData = stream,
        Timeout = TimeSpan.FromMinutes(10),
    };
    _ = await client.Repository.Release.UploadAsset(release, releaseAssetUpload);
}

static async Task SaveArchiveAndWriteOutput(string workspace, string archive, string archiveName, string output)
{
    // 後続のホスト step から参照できるよう、完成した生成物を共有 workspace に保存する。
    DefaultInterpolatedStringHandler directoryName = $".sign-upm-{Guid.NewGuid():N}";
    string outputDirectory;
    try
    {
        outputDirectory = Directory.CreateDirectory(Path.Join(workspace, directoryName.Text)).FullName;
    }
    finally
    {
        directoryName.Clear();
    }

    var outputArchive = Path.Combine(outputDirectory, archiveName);
    File.Copy(archive, outputArchive);
    await File.AppendAllTextAsync(output, $"archive-path={Path.GetRelativePath(workspace, outputArchive)}{Environment.NewLine}");
}

internal sealed class UnityPackageManifest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

[JsonSerializable(typeof(UnityPackageManifest))]
internal partial class UnityPackageManifestJsonContext : JsonSerializerContext
{
}
