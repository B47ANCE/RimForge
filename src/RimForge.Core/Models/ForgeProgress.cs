namespace RimForge.Core.Models;

public sealed record ForgeProgress(
    ForgePhase Phase,
    string TechnicalMessage,
    double OverallProgress,
    double PhaseProgress,
    int Completed = 0,
    int Total = 0)
{
    public int OverallPercentage => ClampPercentage(OverallProgress);
    public int PhasePercentage => ClampPercentage(PhaseProgress);

    private static int ClampPercentage(double value) =>
        (int)Math.Round(Math.Clamp(value, 0d, 1d) * 100d);
}
