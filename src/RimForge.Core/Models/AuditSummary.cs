namespace RimForge.Core.Models;
public sealed record AuditSummary(int ModCount,int MissingDependencies,int Cycles,int MissingMetadata,DateTimeOffset? Generated)
{
 public static AuditSummary Empty { get; } = new(0,0,0,0,null);
}
