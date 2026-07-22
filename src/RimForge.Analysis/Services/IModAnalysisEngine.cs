using RimForge.Analysis.Models;
using RimForge.Core.Models;

namespace RimForge.Analysis.Services;

public interface IModAnalysisEngine
{
    Task<ModAnalysisResult> AnalyzeAsync(
        ModAnalysisRequest request,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default);

    void InvalidateCache(string? inputFingerprint = null);

    ModAnalysisSnapshot Analyze(
        IReadOnlyList<ModRecord> mods,
        IReadOnlyList<string>? currentLoadOrder = null,
        string? targetRimWorldVersion = null,
        IReadOnlyList<UserLoadOrderLock>? lockedPositions = null);
}
