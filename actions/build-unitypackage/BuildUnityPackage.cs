using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Unityを使わずに .unitypackage を生成する。
// .unitypackage の実体は gzip 圧縮された tar アーカイブで、アセットごとに
// 「<guid>/asset」「<guid>/asset.meta」「<guid>/pathname」というエントリを持つ。
// guid は対応する .meta ファイルの「guid:」行から取得する。

var assetsInput = Environment.GetEnvironmentVariable("PACKAGE_PATH");
if (string.IsNullOrWhiteSpace(assetsInput))
{
    Console.Error.WriteLine("The ASSETS environment variable is not set.");
    Environment.Exit(1);
}

var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
if (string.IsNullOrEmpty(workspace))
{
    Console.Error.WriteLine("The GITHUB_WORKSPACE environment variable is not set.");
    Environment.Exit(1);
}

var outputInput = Environment.GetEnvironmentVariable("OUTPUT_PATH");
if (string.IsNullOrWhiteSpace(outputInput))
{
    outputInput = "output.unitypackage";
}
var outputPath = Path.GetFullPath(Path.Combine(workspace, outputInput));

var rootInput = Environment.GetEnvironmentVariable("ROOT_PATH");
if (string.IsNullOrWhiteSpace(rootInput))
{
    rootInput = ".";
}
// pathname (パッケージ内に記録される相対パス) を算出する基準ディレクトリ。
var rootPath = Path.GetFullPath(Path.Combine(workspace, rootInput));

// 入力は改行/カンマ区切りで複数のファイル・ディレクトリを受け取る。
var entries = assetsInput
    .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
    .Select(s => s.Trim())
    .Where(s => s.Length > 0)
    .ToArray();

var guidRegex = new Regex(@"^guid:\s*([0-9a-fA-F]+)", RegexOptions.Multiline);

// 対象アセットの絶対パスを収集する (ディレクトリは再帰的に展開し、フォルダ自身も含める)。
var assetPaths = new SortedSet<string>(StringComparer.Ordinal);
foreach (var entry in entries)
{
    var full = Path.GetFullPath(Path.Combine(workspace, entry));
    if (Directory.Exists(full))
    {
        assetPaths.Add(full.TrimEnd(Path.DirectorySeparatorChar));
        foreach (var path in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            assetPaths.Add(path);
        }
    }
    else if (File.Exists(full))
    {
        assetPaths.Add(full);
    }
    else
    {
        Console.Error.WriteLine($"Asset path not found: {entry}");
        Environment.Exit(1);
    }
}

if (assetPaths.Count == 0)
{
    Console.Error.WriteLine("No assets were resolved from the ASSETS input.");
    Environment.Exit(1);
}

var outputDir = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(outputDir))
{
    Directory.CreateDirectory(outputDir);
}

var added = 0;
using (var fileStream = File.Create(outputPath))
using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
using (var tarWriter = new TarWriter(gzipStream, TarEntryFormat.Pax))
{
    foreach (var assetPath in assetPaths)
    {
        var metaPath = assetPath + ".meta";
        if (!File.Exists(metaPath))
        {
            Console.Error.WriteLine($"Missing .meta file for asset (skipped): {assetPath}");
            continue;
        }

        var metaContent = File.ReadAllText(metaPath);
        var match = guidRegex.Match(metaContent);
        if (!match.Success)
        {
            Console.Error.WriteLine($"Could not find a guid in meta file (skipped): {metaPath}");
            continue;
        }
        var guid = match.Groups[1].Value;

        // pathname は rootPath からの相対パス。Unity の慣習に合わせて区切りは '/' に統一する。
        var relative = Path.GetRelativePath(rootPath, assetPath).Replace(Path.DirectorySeparatorChar, '/');

        // <guid>/ ディレクトリエントリ
        WriteDirectory(tarWriter, $"{guid}/");

        var isDirectory = Directory.Exists(assetPath);
        if (!isDirectory)
        {
            // <guid>/asset : ファイル本体 (フォルダの場合は出力しない)
            WriteFile(tarWriter, $"{guid}/asset", File.ReadAllBytes(assetPath));
        }

        // <guid>/asset.meta : .meta の内容
        WriteFile(tarWriter, $"{guid}/asset.meta", Encoding.UTF8.GetBytes(metaContent));

        // <guid>/pathname : プロジェクト内パス
        WriteFile(tarWriter, $"{guid}/pathname", Encoding.UTF8.GetBytes(relative));

        added++;
        Console.WriteLine($"Packed: {relative} ({guid})");
    }
}

if (added == 0)
{
    Console.Error.WriteLine("No assets were packed (no valid .meta files found).");
    Environment.Exit(1);
}

Console.WriteLine($"Created {outputPath} with {added} asset(s).");

var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(githubOutput))
{
    using var writer = File.AppendText(githubOutput);
    writer.Write("package-path=");
    writer.WriteLine(outputPath);
}

static void WriteDiectory(TarWriter writer, string name)
{
    var entry = new PaxTarEntry(TarEntryType.Directory, name)
    {
        Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
               UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
               UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
    };
    writer.WriteEntry(entry);
}

static void WriteFile(TarWriter writer, string name, byte[] content)
{
    var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
    {
        Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
               UnixFileMode.GroupRead | UnixFileMode.OtherRead,
        DataStream = new MemoryStream(content),
    };
    writer.WriteEntry(entry);
}
