using RimForge.Protocol;

namespace RimForge.Companion.Host;

public sealed record CompanionHostOptions(
    string ForgeSessionId,
    string StateRoot,
    string PipeName,
    string? PlayerLogPath = null,
    int? RimWorldProcessId = null)
{
    public static CompanionHostOptions Parse(IReadOnlyList<string> arguments)
    {
        string? Value(string name)
        {
            var index = arguments.IndexOf(name);
            return index >= 0 && index + 1 < arguments.Count ? arguments[index + 1] : null;
        }

        var sessionId = Value("--session") ?? throw new ArgumentException("--session is required.");
        var stateRoot = Value("--state-root") ?? throw new ArgumentException("--state-root is required.");
        var processText = Value("--rimworld-pid");
        return new CompanionHostOptions(
            sessionId,
            Path.GetFullPath(stateRoot),
            Value("--pipe") ?? ProtocolConstants.PipeName,
            Value("--player-log"),
            int.TryParse(processText, out var processId) ? processId : null);
    }
}

internal static class ArgumentListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
            if (values[index].Equals(value, StringComparison.OrdinalIgnoreCase)) return index;
        return -1;
    }
}
