using System.Text.Json;

namespace RimForge.Analysis.Services;

public sealed class IssueIgnoreStore
{
    private readonly string _path;
    private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);

    public IssueIgnoreStore(string path)
    {
        _path = path;
        Load();
    }

    public IReadOnlySet<string> Snapshot() => new HashSet<string>(_ignored, StringComparer.OrdinalIgnoreCase);
    public bool IsIgnored(string issueId) => _ignored.Contains(issueId);

    public void SetIgnored(string issueId, bool ignored)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueId);
        if (_ignored.Contains(issueId) == ignored) return;

        var next = new HashSet<string>(_ignored, StringComparer.OrdinalIgnoreCase);
        if (ignored) next.Add(issueId);
        else next.Remove(issueId);

        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        var json = JsonSerializer.Serialize(
            next.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            new JsonSerializerOptions { WriteIndented = true });
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _path, overwrite: true);
            _ignored.Clear();
            _ignored.UnionWith(next);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            foreach (var id in JsonSerializer.Deserialize<string[]>(File.ReadAllText(_path)) ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(id)) _ignored.Add(id);
        }
        catch (JsonException) { }
        catch (IOException) { }
    }
}
