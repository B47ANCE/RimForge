using System.Text.Json;

namespace RimForge.App.Serialization;

/// <summary>
/// Shared JSON presentation options for RimForge application-owned files.
/// Specialized serializers in other projects retain their own format-specific options.
/// </summary>
internal static class RimForgeJson
{
    internal static JsonSerializerOptions Indented { get; } = new()
    {
        WriteIndented = true
    };
}
