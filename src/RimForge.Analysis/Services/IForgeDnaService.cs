using RimForge.Analysis.Models;
using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

public interface IForgeDnaService
{
    ForgeDnaSnapshot Current { get; }

    Task<ForgeDnaSnapshot> AnalyzeAsync(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? currentLoadOrder = null,
        string? targetRimWorldVersion = null,
        IReadOnlyList<ForgeEvidenceContribution>? evidence = null,
        IProgress<ForgeDnaProgress>? progress = null,
        CancellationToken cancellationToken = default);

    void Invalidate(string? packageId = null);
}

public sealed record ForgeDnaProgress(
    string Stage,
    string Detail,
    int Completed,
    int Total)
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Completed / Total, 0, 1);
}
