using System;
using System.IO;
var unityCacheHit = Environment.GetEnvironmentVariable("INPUT_UNITY-CACHE-HIT") ?? "false";
var nugetCacheHit = Environment.GetEnvironmentVariable("INPUT_NUGET-CACHE-HIT") ?? "false";

var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
if (!string.IsNullOrEmpty(output))
{
    using var writer = File.AppendText(output);

    writer.Write("cache-hit-unity=");
    writer.WriteLine(unityCacheHit);
    writer.Write("cache-hit-nuget=");
    writer.WriteLine(nugetCacheHit);
}