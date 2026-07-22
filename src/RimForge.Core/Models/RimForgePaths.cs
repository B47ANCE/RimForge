namespace RimForge.Core.Models;

/// <summary>
/// Centralized path contract for RimForge source assets and generated runtime data.
/// RepositoryRoot is source-only. Unless the user supplies an absolute output path,
/// mutable application data is stored under %LOCALAPPDATA%\RimForge.
/// </summary>
public sealed record RimForgePathLayout(
    string RepositoryRoot,
    string LocalApplicationDataRoot,
    string CuratedDatabaseRoot,
    string OutputRoot,
    string ProfilesRoot,
    string CacheRoot,
    string GeneratedDatabaseRoot,
    string UserDatabaseRoot,
    string ReportsRoot,
    string LogsRoot,
    string TempRoot,
    string ExportsRoot,
    string SessionsRoot,
    string DiagnosticsRoot)
{
    public static RimForgePathLayout Create(string repositoryRoot, string? outputFolder = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var root = Path.GetFullPath(repositoryRoot);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
            localAppData = Path.GetTempPath();

        var applicationDataRoot = Path.GetFullPath(Path.Combine(localAppData, "RimForge"));
        var configuredOutput = string.IsNullOrWhiteSpace(outputFolder) ? "Output" : outputFolder.Trim();

        // Absolute paths remain an explicit user override. Relative legacy values such
        // as "Output" now resolve beneath LocalApplicationDataRoot, never the repository.
        var output = Path.IsPathRooted(configuredOutput)
            ? Path.GetFullPath(configuredOutput)
            : Path.GetFullPath(Path.Combine(applicationDataRoot, configuredOutput));

        return new RimForgePathLayout(
            root,
            applicationDataRoot,
            Path.Combine(root, "Database.Curated"),
            output,
            Path.Combine(output, "Profiles"),
            Path.Combine(output, "Cache"),
            Path.Combine(output, "Database", "Generated"),
            Path.Combine(output, "Database", "User"),
            Path.Combine(output, "Reports"),
            Path.Combine(output, "Logs"),
            Path.Combine(output, "Temp"),
            Path.Combine(output, "Exports"),
            Path.Combine(output, "Sessions"),
            Path.Combine(output, "Diagnostics"));
    }

    public static string? FindRepositoryRoot(string startDirectory)
    {
        if (string.IsNullOrWhiteSpace(startDirectory)) return null;
        var current = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RimForge.sln")) ||
                File.Exists(Path.Combine(current.FullName, "Config.json")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        return null;
    }

    public static string ResolveRepositoryRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        return FindRepositoryRoot(AppContext.BaseDirectory)
            ?? FindRepositoryRoot(currentDirectory)
            ?? Path.GetFullPath(currentDirectory);
    }

    public IEnumerable<string> CuratedDatabaseCandidates(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        yield return Path.Combine(CuratedDatabaseRoot, fileName);
        yield return Path.Combine(AppContext.BaseDirectory, "Database.Curated", fileName);
        yield return Path.Combine(AppContext.BaseDirectory, fileName);
    }

    public void EnsureGeneratedDirectories()
    {
        foreach (var directory in new[]
        {
            LocalApplicationDataRoot,
            OutputRoot,
            ProfilesRoot,
            CacheRoot,
            GeneratedDatabaseRoot,
            UserDatabaseRoot,
            ReportsRoot,
            LogsRoot,
            TempRoot,
            ExportsRoot,
            SessionsRoot,
            DiagnosticsRoot
        })
        {
            Directory.CreateDirectory(directory);
        }
    }
}
