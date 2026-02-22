using System;
using System.IO;
using System.Linq;

var localCachePath = Environment.GetEnvironmentVariable("LOCAL_CACHE_PATH");
if (string.IsNullOrEmpty(localCachePath))
{
    Console.Error.WriteLine("The LOCAL_CACHE_PATH environment variable is not set.");
    Environment.Exit(1);
}

var sourcePath = Environment.GetEnvironmentVariable("SOURCE_PATH");
if (string.IsNullOrEmpty(sourcePath))
{
    Console.Error.WriteLine("The SOURCE_PATH environment variable is not set.");
    Environment.Exit(1);
}

var cacheKey = Environment.GetEnvironmentVariable("CACHE_KEY");
if (string.IsNullOrEmpty(cacheKey))
{
    Console.Error.WriteLine("The CACHE_KEY environment variable is not set.");
    Environment.Exit(1);
}

// Create local cache base directory with .gitignore
Directory.CreateDirectory(localCachePath);
var gitignorePath = Path.Combine(localCachePath, ".gitignore");
if (!File.Exists(gitignorePath))
{
    File.WriteAllText(gitignorePath, "*\n");
}

// Create the specific cache directory for this project
var cacheDir = Path.Combine(localCachePath, cacheKey);
Directory.CreateDirectory(cacheDir);

// Check if cache has content
var cacheHit = Directory.EnumerateFileSystemEntries(cacheDir).Any();

// Handle existing source directory (real directory, not a symlink)
var sourceInfo = new DirectoryInfo(sourcePath);
if (sourceInfo.Exists && sourceInfo.LinkTarget is null)
{
    if (cacheHit)
    {
        // Cache already populated - remove real source dir to be replaced by symlink
        Directory.Delete(sourcePath, recursive: true);
    }
    else
    {
        // No cache yet - move existing source dir into local cache
        Directory.Delete(cacheDir, recursive: true);
        Directory.Move(sourcePath, cacheDir);
    }
}

// Create symlink from source path to local cache directory
if (!Path.Exists(sourcePath))
{
    Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
    Directory.CreateSymbolicLink(sourcePath, cacheDir);
}

// Write output
var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(output))
{
    using var writer = File.AppendText(output);
    writer.Write("cache-hit=");
    writer.WriteLine(cacheHit.ToString().ToLower());
}
