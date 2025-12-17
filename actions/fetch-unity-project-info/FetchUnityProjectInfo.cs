using System;
using System.IO;
var projectPath = Environment.GetEnvironmentVariable("INPUT_UNITY_PROJECT_PATH");
if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
{
    Console.Error.WriteLine("The INPUT_UNITY_PROJECT_PATH environment variable is either not set or an invalid path.");
    Environment.Exit(1);
}

var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
if (string.IsNullOrEmpty(workspace))
{
    Console.Error.WriteLine("The GITHUB_WORKSPACE environment variable is not set.");
    Environment.Exit(1);
}
// このファイルをactionsで実行するとき、カレントディレクトリはactions/unity-cacheになるので、GITHUB_WORKSPACEを使ってUnityのプロジェクトパスを取得しなければいけない
var name = Path.GetFileName(Path.GetFullPath(Path.Combine(workspace, projectPath)));

var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(output))
{
    using var writer = File.AppendText(output);

    writer.Write("name=");
    writer.WriteLine(name);
}