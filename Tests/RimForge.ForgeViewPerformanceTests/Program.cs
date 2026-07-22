using System.Diagnostics;
using RimForge.App.Features.ForgeView;
using RimForge.Core.Models;
using RimForge.Core.Services;

var count = ForgeGraphPerformanceBudgets.RepresentativeNodeCount;
var nodes = Enumerable.Range(0, count)
    .Select(index => new DependencyGraphNode(
        $"fixture-{index:D4}",
        $"Representative Mod {index:D4}",
        $"rimforge.fixture.{index:D4}",
        "RimForge",
        null,
        null,
        null,
        ["1.6"],
        null,
        index % 17 == 0 ? ModHealthStatus.Warning : ModHealthStatus.Healthy))
    .ToArray();
var edges = new List<DependencyGraphEdge>(count * 2);
for (var index = 1; index < count; index++)
{
    edges.Add(new DependencyGraphEdge(
        nodes[index].PackageId!,
        nodes[index - 1].PackageId!,
        DependencyRelationshipType.Required,
        "Representative dependency",
        1,
        ["performance-fixture"]));
    if (index >= 10 && index % 5 == 0)
        edges.Add(new DependencyGraphEdge(
            nodes[index].PackageId!,
            nodes[index - 10].PackageId!,
            DependencyRelationshipType.Optional,
            "Representative optional integration",
            1,
            ["performance-fixture"]));
}

var queryService = new ForgeGraphQueryService();
_ = queryService.Execute(new DependencyGraphModel(nodes[..10], edges.Take(9).ToArray()), new ForgeGraphQuery());
var queryTimer = Stopwatch.StartNew();
var query = queryService.Execute(new DependencyGraphModel(nodes, edges), new ForgeGraphQuery());
queryTimer.Stop();
Require(query.Nodes.Count == count && query.Edges.Count == edges.Count, "Representative query lost graph topology.");
Require(queryTimer.Elapsed <= ForgeGraphPerformanceBudgets.QueryBudget,
    $"Representative query exceeded {ForgeGraphPerformanceBudgets.QueryBudget.TotalMilliseconds:0} ms: {queryTimer.Elapsed.TotalMilliseconds:0.0} ms.");

var layout = ForgeGraphCanvas.BenchmarkLayout(query.Nodes, query.Edges);
Require(layout.Positions == count, "Representative layout did not position every node.");
Require(layout.Elapsed <= ForgeGraphPerformanceBudgets.LayoutBudget,
    $"Representative layout exceeded {ForgeGraphPerformanceBudgets.LayoutBudget.TotalMilliseconds:0} ms: {layout.Elapsed.TotalMilliseconds:0.0} ms.");

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try
{
    _ = ForgeGraphCanvas.BenchmarkLayout(query.Nodes, query.Edges, cancelled.Token);
    throw new InvalidOperationException("Cancelled representative layout completed instead of observing cancellation.");
}
catch (OperationCanceledException)
{
}

Console.WriteLine($"ForgeView performance fixture passed: {count} nodes, {edges.Count} edges, query {queryTimer.Elapsed.TotalMilliseconds:0.0} ms, layout {layout.Elapsed.TotalMilliseconds:0.0} ms.");

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
