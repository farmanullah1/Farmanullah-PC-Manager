using Xunit;

namespace PCManager.Tests;

public sealed class AndroidParityTests
{
    [Fact]
    public void Feature_map_has_no_unimplemented_reference_features()
    {
        string path = Path.Combine(
            RepoRoot(),
            "docs",
            "dev",
            "uad-feature-map.md");
        Assert.True(File.Exists(path), path);

        string[] lines = File.ReadAllLines(path);
        string[] featureRows = lines
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Where(line =>
                line.Contains('|') &&
                !line.Contains("Upstream feature", StringComparison.Ordinal) &&
                !line.All(character => character == '|' || character == '-' || char.IsWhiteSpace(character)))
            .Where(line => line.Split('|').Length >= 6)
            .ToArray();

        Assert.NotEmpty(featureRows);
        Assert.All(featureRows, line =>
        {
            string status = line.Split('|')[4].Trim();
            Assert.True(
                !status.Contains("not started", StringComparison.OrdinalIgnoreCase),
                $"Unimplemented UAD feature row: {line}");
        });
    }

    private static string RepoRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "app", "PCManager.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
