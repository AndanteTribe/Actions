using System;
using System.IO;
var projectPath = Environment.GetEnvironmentVariable("UNITY_PROJECT_PATH");
if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
{
    Console.Error.WriteLine("The UNITY_PROJECT_PATH environment variable is either not set or an invalid path.");
    Environment.Exit(1);
}

var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
var combined =
    Path.IsPathRooted(projectPath) || string.IsNullOrEmpty(workspace)
        ? projectPath
        : Path.Combine(workspace, projectPath);

projectPath = Path.GetFullPath(combined);

if (!Directory.Exists(projectPath))
{
    Console.Error.WriteLine("The UNITY_PROJECT_PATH environment variable is either not set or an invalid path.");
    Environment.Exit(1);
}

var name = new DirectoryInfo(projectPath).Name;

var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(output))
{
    using var writer = File.AppendText(output);

    writer.Write("name=");
    writer.WriteLine(name);
}