using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Unityを使わずに .unitypackage を生成する。
// .unitypackage の実体は gzip 圧縮された tar アーカイブで、アセットごとに
// 「<guid>/asset」「<guid>/asset.meta」「<guid>/pathname」というエントリを持つ。
// guid は対応する .meta ファイルの「guid:」行から取得する。

var assetsInput = GetRequiredEnv("PACKAGE_PATH");
var workspace = GetRequiredEnv("GITHUB_WORKSPACE");

var outputInput = GetEnvOrDefault("OUTPUT_PATH", "output.unitypackage");
var outputPath = Path.GetFullPath(Path.Combine(workspace, outputInput));

// pathname (パッケージ内に記録される相対パス) を算出する基準パス。
var rootInput = GetEnvOrDefault("ROOT_PATH", assetsInput);
var rootPath = Path.GetFullPath(Path.Combine(workspace, rootInput));

// 入力を改行/カンマ区切りに変換
var entries = assetsInput
    .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

// 対象アセットの絶対パスを収集。重複排除と決定的な順序のため SortedSet を使う。
var assetPaths = new SortedSet<string>(StringComparer.Ordinal);
foreach (var entry in entries)
{
    var full = Path.GetFullPath(Path.Combine(workspace, entry));
    if (Directory.Exists(full))
    {
        assetPaths.Add(full.TrimEnd(Path.DirectorySeparatorChar));
        foreach (var path in Directory.EnumerateFileSystemEntries(full, "*", SearchOption.AllDirectories))
        {
            // .meta はアセット本体とペアで扱うため、ここでは収集しない。
            if (!path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                assetPaths.Add(path);
            }
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
    Console.Error.WriteLine("No assets were resolved from the PACKAGE_PATH input.");
    Environment.Exit(1);
}

// 並列で.metaを収集し、guidを抜き出す。
// 元のソート順を保つため index 付き配列へ格納し、書き込みは直列で行う。
var ordered = assetPaths.ToArray();
var items = new PackItem?[ordered.Length];
await Parallel.ForEachAsync(Enumerable.Range(0, ordered.Length), async (i, ct) =>
{
    var assetPath = ordered[i];
    var metaPath = assetPath + ".meta";
    if (!File.Exists(metaPath))
    {
        Console.Error.WriteLine($"Missing .meta file for asset (skipped): {assetPath}");
        return;
    }

    var metaBytes = await File.ReadAllBytesAsync(metaPath, ct);
    var guid = TryGetGuid(metaBytes);
    if (guid is null)
    {
        Console.Error.WriteLine($"Could not find a guid in meta file (skipped): {metaPath}");
        return;
    }

    items[i] = new PackItem(assetPath, guid, metaBytes);
});

var outputDir = Path.GetDirectoryName(outputPath);
if (!string.IsNullOrEmpty(outputDir))
{
    Directory.CreateDirectory(outputDir);
}

// tarへ変換
var added = 0;
using (var fileStream = File.Create(outputPath))
using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
// TarWriterのFormatがPAXだと毎回バイト数が変わるらしい（プロセスIDを埋め込むので）
// そのためUstarを使用してみた
using (var tarWriter = new TarWriter(gzipStream, TarEntryFormat.Ustar))
{
    foreach (var entry in items)
    {
        // .meta が無い / guid 不明だった要素は null なのでスキップ。
        if (entry is not { } item)
        {
            continue;
        }

        var relative = Path.GetRelativePath(rootPath, item.AssetPath).Replace(Path.DirectorySeparatorChar, '/');

        // ディレクトリ
        WriteDirectory(tarWriter, $"{item.Guid}/");

        // guid asset : ファイル本体 ディレクトリだけの場合は存在しない。
        if (!Directory.Exists(item.AssetPath))
        {
            WriteFileEntry(tarWriter, $"{item.Guid}/asset", File.OpenRead(item.AssetPath));
        }

        // guid asset.meta : .meta の内容 (元のバイト列をそのまま保持)
        WriteFileEntry(tarWriter, $"{item.Guid}/asset.meta", new MemoryStream(item.MetaBytes, writable: false));

        // guid /pathname : プロジェクト内パス
        WriteFileEntry(tarWriter, $"{item.Guid}/pathname", new MemoryStream(Encoding.UTF8.GetBytes(relative)));

        added++;
        Console.WriteLine($"Packed: {relative} ({item.Guid})");
    }
}

if (added == 0)
{
    Console.Error.WriteLine("No assets were packed (no valid .meta files found).");
    Environment.Exit(1);
}

Console.WriteLine($"Created {outputPath} with {added} asset(s).");

var githubOutput = GetRequiredEnv("GITHUB_OUTPUT");
using var writer = File.AppendText(githubOutput);
writer.WriteLine($"package-path={outputPath}");

// 必須の環境変数を取得する。未設定ならエラーを出して終了する。
// actionsのUtilとして抜き出しても良いかも？
static string GetRequiredEnv(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        Console.Error.WriteLine($"The {name} environment variable is not set.");
        Environment.Exit(1);
    }
    return value!;
}

// 環境変数を取得する。未設定なら既定値を返す。
// actionsのUtilとして抜き出しても良いかも？
static string GetEnvOrDefault(string name, string fallback)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.IsNullOrEmpty(value) ? fallback : value;
}

// .meta の中から「guid:」行の値を探す。見つからなければ null。
static string? TryGetGuid(byte[] metaBytes)
{
    foreach (var rawLine in Encoding.UTF8.GetString(metaBytes).AsSpan().EnumerateLines())
    {
        var line = rawLine.Trim();
        if (line.StartsWith("guid:", StringComparison.Ordinal))
        {
            // "guid:" 以降の最初のトークンを guid とみなす。
            var value = line["guid:".Length..].Trim();
            var end = value.IndexOfAny(" \t#");
            if (end >= 0)
            {
                value = value[..end];
            }
            if (!value.IsEmpty)
            {
                return value.ToString();
            }
        }
    }
    return null;
}

static void WriteDirectory(TarWriter writer, string name)
{
    var entry = new UstarTarEntry(TarEntryType.Directory, name)
    {
        Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
               UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
               UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
        // mtime を固定し、同一入力なら常に同じ .unitypackage が出力されるようにする (再現性 / キャッシュ向き)。
        ModificationTime = DateTimeOffset.UnixEpoch,
    };
    writer.WriteEntry(entry);
}



static void WriteFileEntry(TarWriter writer, string name, Stream content)
{
    using (content)
    {
        var entry = new UstarTarEntry(TarEntryType.RegularFile, name)
        {
            Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                   UnixFileMode.GroupRead | UnixFileMode.OtherRead,
            // mtime を固定し、出力を再現可能にする。（らしい）
            ModificationTime = DateTimeOffset.UnixEpoch,
            DataStream = content,
        };
        writer.WriteEntry(entry);
    }
}

// .metaごとの要素
readonly record struct PackItem(string AssetPath, string Guid, byte[] MetaBytes);
