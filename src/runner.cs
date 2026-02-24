using System;
using System.Diagnostics;
foreach (var file in args)
{
    Console.WriteLine($"Running {file}");
    using var process = Process.Start(new ProcessStartInfo("dotnet", ["run", "-c", "Release", file])
    {
        UseShellExecute = false,
    }) ?? throw new InvalidOperationException($"Failed to start process for {file}");
    process.WaitForExit();
    if (process.ExitCode != 0)
        Environment.Exit(process.ExitCode);
}
