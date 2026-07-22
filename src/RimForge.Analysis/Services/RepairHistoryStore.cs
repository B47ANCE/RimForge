using System.Text.Json;
using RimForge.Analysis.Models;

namespace RimForge.Analysis.Services;

public sealed class RepairHistoryStore
{
    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public IReadOnlyList<RepairHistoryEntry> Load(string path)
    {
        if (!File.Exists(path)) return Array.Empty<RepairHistoryEntry>();
        try
        {
            return JsonSerializer.Deserialize<RepairHistoryEntry[]>(File.ReadAllText(path), _options)
                ?? Array.Empty<RepairHistoryEntry>();
        }
        catch
        {
            return Array.Empty<RepairHistoryEntry>();
        }
    }

    public void Append(string path, RepairHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var history = Load(path).Append(entry).OrderByDescending(item => item.Started).ToArray();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(history, _options));
        File.Move(temp, path, true);
    }
}
