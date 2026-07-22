namespace RimForge.Core.Models;

public enum ExternalProfileChangeKind
{
    Created,
    Modified,
    Deleted,
    Renamed
}

public sealed record ExternalProfileChange(
    string Path,
    ExternalProfileChangeKind Kind,
    DateTimeOffset DetectedUtc,
    string? PreviousSha256,
    string? CurrentSha256,
    bool FileExists)
{
    public bool ContentChanged => !string.Equals(PreviousSha256, CurrentSha256, StringComparison.OrdinalIgnoreCase);
}
