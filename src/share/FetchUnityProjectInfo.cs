using System;
using System.IO;
var projectPath = Environment.GetEnvironmentVariable("UNITY_PROJECT_PATH");
if (string.IsNullOrEmpty(projectPath) || !Directory.Exists(projectPath))
{
    Console.Error.WriteLine("The UNITY_PROJECT_PATH environment variable is either not set or an invalid path.");
    Environment.Exit(1);
}

var name = Path.GetFileName(Path.GetDirectoryName(projectPath.AsSpan()));

var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(output))
{
    using var writer = File.AppendText(output);

    writer.Write("name=");
    writer.WriteLine(name);
}