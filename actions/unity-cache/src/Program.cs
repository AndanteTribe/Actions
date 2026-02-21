using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

var phase = args.Length > 0 ? args[0] : "main";
try
{
    if (phase == "post")
        await RunPostAsync();
    else
        await RunMainAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[unity-cache] {ex.Message}");
    Environment.Exit(1);
}

// -----------------------------------------------------------------------
// Main phase: resolve project info, restore cache from the cache service,
// save state so the post phase knows what to do.
// -----------------------------------------------------------------------
async Task RunMainAsync()
{
    var projectPath = Env("INPUT_UNITY_PROJECT_PATH");
    if (string.IsNullOrEmpty(projectPath))
    {
        Console.Error.WriteLine("[unity-cache] INPUT_UNITY_PROJECT_PATH is not set.");
        Environment.Exit(1);
        return;
    }

    // Inside a Docker container action the workspace is bind-mounted at
    // /github/workspace and GITHUB_WORKSPACE is set to that path.
    // Resolve relative paths against it so the directory name is consistent
    // with what the composite action produced on the runner.
    var workspace = Env("GITHUB_WORKSPACE") ?? Directory.GetCurrentDirectory();
    var fullProjectPath = Path.GetFullPath(Path.Combine(workspace, projectPath));

    if (!Directory.Exists(fullProjectPath))
    {
        Console.Error.WriteLine($"[unity-cache] Unity project path not found: {fullProjectPath}");
        Environment.Exit(1);
        return;
    }

    var failOnCacheMiss = IsTrue("INPUT_FAIL_ON_CACHE_MISS");
    var lookupOnly = IsTrue("INPUT_LOOKUP_ONLY");

    var projectName = Path.GetFileName(fullProjectPath);
    var cacheKey = $"unity-cache-{projectName}";
    var libraryPath = Path.Combine(fullProjectPath, "Library");

    // Compute a version string that is stable across runs of this Docker action.
    var version = ComputeVersion(libraryPath);

    Console.WriteLine($"[unity-cache] key: {cacheKey}");

    // Persist state for the post phase.
    AppendState("CACHE_KEY", cacheKey);
    AppendState("CACHE_VERSION", version);
    AppendState("LIBRARY_PATH", libraryPath);
    AppendState("FULL_PROJECT_PATH", fullProjectPath);
    AppendState("LOOKUP_ONLY", lookupOnly ? "true" : "false");

    var (cacheHit, archiveUrl) = await QueryCacheAsync(cacheKey, version);

    AppendState("CACHE_HIT", cacheHit ? "true" : "false");

    if (!cacheHit && failOnCacheMiss)
    {
        Console.Error.WriteLine($"[unity-cache] Cache not found for key: {cacheKey}");
        Environment.Exit(1);
        return;
    }

    if (cacheHit && !lookupOnly && archiveUrl != null)
    {
        Console.WriteLine("[unity-cache] Restoring cache…");
        await DownloadAndExtractAsync(archiveUrl, fullProjectPath);
        Console.WriteLine("[unity-cache] Cache restored.");
    }

    AppendOutput("cache-hit-unity", cacheHit ? "true" : "false");
    AppendOutput("cache-hit-nuget", "false");
}

// -----------------------------------------------------------------------
// Post phase: save cache when the main phase did not produce a cache hit.
// -----------------------------------------------------------------------
async Task RunPostAsync()
{
    var cacheHit = IsTrue("STATE_CACHE_HIT");
    var lookupOnly = IsTrue("STATE_LOOKUP_ONLY");

    if (cacheHit || lookupOnly)
    {
        Console.WriteLine("[unity-cache] Skipping save (cache hit or lookup-only).");
        return;
    }

    var cacheKey = Env("STATE_CACHE_KEY");
    var version = Env("STATE_CACHE_VERSION");
    var libraryPath = Env("STATE_LIBRARY_PATH");
    var fullProjectPath = Env("STATE_FULL_PROJECT_PATH");

    if (string.IsNullOrEmpty(cacheKey) || string.IsNullOrEmpty(version)
        || string.IsNullOrEmpty(libraryPath) || string.IsNullOrEmpty(fullProjectPath))
    {
        Console.WriteLine("[unity-cache] Missing state; skipping save.");
        return;
    }

    if (!Directory.Exists(libraryPath))
    {
        Console.WriteLine($"[unity-cache] Library directory not found: {libraryPath}; skipping save.");
        return;
    }

    Console.WriteLine($"[unity-cache] Saving cache: {cacheKey}");
    await SaveCacheAsync(cacheKey, version, libraryPath, fullProjectPath);
    Console.WriteLine("[unity-cache] Cache saved.");
}

// -----------------------------------------------------------------------
// Cache service helpers
// -----------------------------------------------------------------------

/// <summary>
/// Computes the cache version in the same way as @actions/cache does on
/// Linux with zstd compression, so that caches built by this action are
/// self-consistent across runs.
/// </summary>
string ComputeVersion(string libraryPath)
{
    var components = new[] { libraryPath, "Linux", "zstd" };
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", components)));
    return Convert.ToHexString(hash).ToLowerInvariant();
}

async Task<(bool hit, string? url)> QueryCacheAsync(string key, string version)
{
    using var client = TryCreateApiClient();
    if (client == null)
    {
        Console.WriteLine("[unity-cache] Cache service not available.");
        return (false, null);
    }

    var response = await client.GetAsync(
        $"cache?keys={Uri.EscapeDataString(key)}&version={version}");

    if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
        return (false, null);

    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine($"[unity-cache] Cache query returned {(int)response.StatusCode}.");
        return (false, null);
    }

    var json = await response.Content.ReadAsStringAsync();
    var entry = JsonSerializer.Deserialize<CacheEntry>(json, jsonOptions);
    return (entry?.ArchiveLocation != null, entry?.ArchiveLocation);
}

async Task DownloadAndExtractAsync(string archiveUrl, string extractBasePath)
{
    var tempFile = Path.Combine(Path.GetTempPath(), $"unity-cache-{Guid.NewGuid()}.tar.zst");
    try
    {
        using var http = new HttpClient();
        using var response =
            await http.GetAsync(archiveUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using (var fs = File.Create(tempFile))
        {
            await response.Content.CopyToAsync(fs);
        }

        Console.WriteLine($"[unity-cache] Downloaded {new FileInfo(tempFile).Length:N0} bytes.");
        await RunAsync("tar",
            "-C", extractBasePath, "--use-compress-program=zstd", "-xf", tempFile);
    }
    finally
    {
        if (File.Exists(tempFile)) File.Delete(tempFile);
    }
}

async Task SaveCacheAsync(
    string key, string version, string libraryPath, string projectPath)
{
    var tempFile = Path.Combine(Path.GetTempPath(), $"unity-cache-{Guid.NewGuid()}.tar.zst");
    try
    {
        Console.WriteLine("[unity-cache] Creating archive…");
        await RunAsync("tar",
            "-C", projectPath, "--use-compress-program=zstd", "-cf", tempFile, "Library");

        var fileSize = new FileInfo(tempFile).Length;
        Console.WriteLine($"[unity-cache] Archive size: {fileSize:N0} bytes.");

        using var client = TryCreateApiClient();
        if (client == null)
        {
            Console.WriteLine("[unity-cache] Cache service not available; skipping save.");
            return;
        }

        // --- Reserve --------------------------------------------------
        var reserveBody =
            JsonSerializer.Serialize(new { key, version, cacheSize = fileSize });
        var reserveResponse = await client.PostAsync("caches",
            new StringContent(reserveBody, Encoding.UTF8, "application/json"));

        if (!reserveResponse.IsSuccessStatusCode)
        {
            var err = await reserveResponse.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"[unity-cache] Reserve failed: {err}");
            return;
        }

        var reserveJson = await reserveResponse.Content.ReadAsStringAsync();
        var reserved = JsonSerializer.Deserialize<ReserveCacheResponse>(reserveJson, jsonOptions);
        if (reserved?.CacheId == null || reserved.CacheId <= 0)
        {
            Console.Error.WriteLine("[unity-cache] Invalid reserve response.");
            return;
        }

        Console.WriteLine($"[unity-cache] Reserved cache ID: {reserved.CacheId}.");

        // --- Upload in chunks -----------------------------------------
        var rawChunkSize = Env("INPUT_UPLOAD_CHUNK_SIZE");
        var chunkSize = int.TryParse(rawChunkSize, out var parsed) && parsed > 0
            ? parsed
            : 32 * 1024 * 1024; // default 32 MB
        await using var fs = File.OpenRead(tempFile);
        var buffer = new byte[chunkSize];
        long offset = 0;

        while (offset < fileSize)
        {
            var bytesRead = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length));
            if (bytesRead == 0) break;

            var request = new HttpRequestMessage(HttpMethod.Patch,
                $"caches/{reserved.CacheId}");
            request.Content = new ByteArrayContent(buffer, 0, bytesRead);
            request.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.TryAddWithoutValidation(
                "Content-Range", $"bytes {offset}-{offset + bytesRead - 1}/*");

            var patchResponse = await client.SendAsync(request);
            if (!patchResponse.IsSuccessStatusCode)
            {
                var err = await patchResponse.Content.ReadAsStringAsync();
                Console.Error.WriteLine($"[unity-cache] Chunk upload failed: {err}");
                return;
            }

            offset += bytesRead;
        }

        // --- Commit ---------------------------------------------------
        var commitBody = JsonSerializer.Serialize(new { size = fileSize });
        var commitResponse = await client.PostAsync($"caches/{reserved.CacheId}",
            new StringContent(commitBody, Encoding.UTF8, "application/json"));

        if (!commitResponse.IsSuccessStatusCode)
        {
            var err = await commitResponse.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"[unity-cache] Commit failed: {err}");
        }
    }
    finally
    {
        if (File.Exists(tempFile)) File.Delete(tempFile);
    }
}

/// <summary>Returns a pre-configured HttpClient aimed at the cache service, or
/// null when the required environment variables are absent.</summary>
HttpClient? TryCreateApiClient()
{
    var cacheUrl = Env("ACTIONS_CACHE_URL");
    var token = Env("ACTIONS_RUNTIME_TOKEN");
    if (string.IsNullOrEmpty(cacheUrl) || string.IsNullOrEmpty(token))
        return null;

    var baseUrl = cacheUrl.TrimEnd('/') + "/_apis/artifactcache/";
    var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", token);
    client.DefaultRequestHeaders.Accept.Add(
        MediaTypeWithQualityHeaderValue.Parse("application/json;api-version=6.0-preview.1"));
    return client;
}

// -----------------------------------------------------------------------
// Process helper
// -----------------------------------------------------------------------
async Task RunAsync(string command, params string[] arguments)
{
    var psi = new ProcessStartInfo(command)
    {
        UseShellExecute = false,
        RedirectStandardError = true,
    };
    foreach (var arg in arguments)
        psi.ArgumentList.Add(arg);

    var process = Process.Start(psi)
        ?? throw new Exception($"Failed to start: {command}");
    var stderr = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
        throw new Exception(
            $"`{command} {string.Join(" ", arguments)}` failed (exit {process.ExitCode}): {stderr}");
}

// -----------------------------------------------------------------------
// Utility helpers
// -----------------------------------------------------------------------
static bool IsTrue(string envVar) =>
    "true".Equals(Environment.GetEnvironmentVariable(envVar),
        StringComparison.OrdinalIgnoreCase);

static string? Env(string name) => Environment.GetEnvironmentVariable(name);

static void AppendState(string name, string value)
{
    var file = Env("GITHUB_STATE");
    if (!string.IsNullOrEmpty(file))
        File.AppendAllText(file, $"{name}={value}\n");
}

static void AppendOutput(string name, string value)
{
    var file = Env("GITHUB_OUTPUT");
    if (!string.IsNullOrEmpty(file))
        File.AppendAllText(file, $"{name}={value}\n");
}

// -----------------------------------------------------------------------
// JSON models
// -----------------------------------------------------------------------
internal record CacheEntry
{
    [JsonPropertyName("archiveLocation")]
    public string? ArchiveLocation { get; init; }

    [JsonPropertyName("cacheKey")]
    public string? CacheKey { get; init; }
}

internal record ReserveCacheResponse
{
    [JsonPropertyName("cacheId")]
    public int? CacheId { get; init; }
}
