using System;
using System.Diagnostics;
using System.IO;

string GetRequired(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrEmpty(value))
    {
        Console.Error.WriteLine($"The {name} environment variable is not set.");
        Environment.Exit(1);
    }
    return value!;
}

var unityVersion = GetRequired("UNITY_VERSION");
var targetPlatform = GetRequired("TARGET_PLATFORM");
var editorVersion = Environment.GetEnvironmentVariable("EDITOR_VERSION") ?? "3.2.1";
var unityProjectPath = GetRequired("UNITY_PROJECT_PATH");
var blankProjectPath = Environment.GetEnvironmentVariable("BLANK_PROJECT_PATH") ?? "/tmp/BlankProject";
var containerName = Environment.GetEnvironmentVariable("CONTAINER_NAME") ?? "unity-agent";
var githubWorkspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE") ?? string.Empty;

// Resolve relative project path against GITHUB_WORKSPACE
if (!Path.IsPathRooted(unityProjectPath) && !string.IsNullOrEmpty(githubWorkspace))
    unityProjectPath = Path.Combine(githubWorkspace, unityProjectPath);

var unityImage = $"unityci/editor:{unityVersion}-{targetPlatform}-{editorVersion}";
var imageTag = $"unity-container-action:{unityVersion}-{targetPlatform}-{editorVersion}";

// Build the Unity container image from UnityDockerfile
Console.WriteLine($"Building Unity container image: {imageTag}");
RunCommand("docker", $"build --build-arg UNITY_IMAGE={unityImage} -t {imageTag} -f /app/UnityDockerfile /app");

// Start the Unity container in detached mode
Console.WriteLine($"Starting Unity container: {containerName}");
var containerId = RunCommandOutput(
    "docker",
    $"run -d --name {containerName} " +
    $"-e UNITY_SERIAL -e UNITY_EMAIL -e UNITY_PASSWORD " +
    $"-v \"{blankProjectPath}:/BlankProject\" " +
    $"-v \"{unityProjectPath}:/workspace\" " +
    $"{imageTag}");

containerId = containerId.Trim();
Console.WriteLine($"Container ID: {containerId}");

var githubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(githubOutput))
{
    using var writer = File.AppendText(githubOutput);
    writer.Write("container-id=");
    writer.WriteLine(containerId);
}

static void RunCommand(string command, string args)
{
    var psi = new ProcessStartInfo(command, args)
    {
        UseShellExecute = false,
        RedirectStandardError = true,
    };
    using var process = Process.Start(psi)
        ?? throw new InvalidOperationException($"Failed to start: {command} {args}");
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        if (!string.IsNullOrEmpty(stderr))
            Console.Error.WriteLine(stderr);
        throw new InvalidOperationException($"Command failed (exit code {process.ExitCode}): {command} {args}");
    }
}

static string RunCommandOutput(string command, string args)
{
    var psi = new ProcessStartInfo(command, args)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    using var process = Process.Start(psi)
        ?? throw new InvalidOperationException($"Failed to start: {command} {args}");
    var output = process.StandardOutput.ReadToEnd();
    var stderr = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        if (!string.IsNullOrEmpty(stderr))
            Console.Error.WriteLine(stderr);
        throw new InvalidOperationException($"Command failed (exit code {process.ExitCode}): {command} {args}");
    }
    return output;
}
