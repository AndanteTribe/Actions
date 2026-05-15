var root = args.Length > 0 ? args[0] : "CodeCoverage";
var summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

if (!String.IsNullOrEmpty(summaryPath))
{
    Console.Error.WriteLine($"::error::No summary path specified: Environment.GetEnvironmentVariable(\"GITHUB_STEP_SUMMARY\")");
    return;
}

if (!Directory.Exists(root))
{
    Console.Error.WriteLine($"::warning::Directory not found: {root}");
    return;
}

foreach (var file in Directory.EnumerateFiles(root, "Summary.md", SearchOption.AllDirectories))
{
    var content = File.ReadAllText(file);
    if (summaryPath is not null)
        File.AppendAllText(summaryPath, content);
}