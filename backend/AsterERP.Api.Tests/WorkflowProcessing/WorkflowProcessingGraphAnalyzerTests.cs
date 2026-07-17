using AsterERP.Workflow.Processing.Analysis;
using AsterERP.Workflow.Processing.Definitions;
using Xunit;

namespace AsterERP.Api.Tests.WorkflowProcessing;

public sealed class WorkflowProcessingGraphAnalyzerTests
{
    [Fact]
    public void TopologicalSort_ReturnsStableOrderForDag()
    {
        var analyzer = new WorkflowProcessingGraphAnalyzer();

        var result = analyzer.TopologicalSort(Dag());

        Assert.True(result.Succeeded);
        Assert.Equal(["start", "load", "score", "finish"], result.NodeIds);
    }

    [Fact]
    public void FindPaths_StopsAtVisitedNodesForCyclicGraphs()
    {
        var analyzer = new WorkflowProcessingGraphAnalyzer();
        var definition = new WorkflowProcessingDefinition
        {
            Id = "cyclic",
            Name = "Cyclic",
            RequiresAcyclicGraph = false,
            Nodes =
            [
                Node("a"),
                Node("b"),
                Node("c")
            ],
            Edges =
            [
                Edge("a-b", "a", "b"),
                Edge("b-a", "b", "a"),
                Edge("b-c", "b", "c")
            ]
        };

        var paths = analyzer.FindPaths(definition, "a", "c", maxDepth: 6, limit: 20);

        var path = Assert.Single(paths);
        Assert.Equal(["a", "b", "c"], path.NodeIds);
        Assert.Equal(["a-b", "b-c"], path.EdgeIds);
    }

    [Fact]
    public void AnalyzeImpact_ReturnsDownstreamNodesAndEdges()
    {
        var analyzer = new WorkflowProcessingGraphAnalyzer();

        var result = analyzer.AnalyzeImpact(Dag(), "load", maxDepth: 3, limit: 20);

        Assert.Equal("load", result.RootNodeId);
        Assert.Equal(["score", "finish"], result.NodeIds);
        Assert.Equal(["load-score", "score-finish"], result.EdgeIds);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void Diff_ReturnsAddedRemovedAndChangedMembers()
    {
        var analyzer = new WorkflowProcessingGraphAnalyzer();
        var baseline = Dag();
        var candidate = Dag();
        candidate.Nodes.RemoveAll(node => node.Id == "finish");
        candidate.Nodes.Add(new WorkflowProcessingNode { Id = "audit", Name = "Audit" });
        candidate.Nodes.Single(node => node.Id == "score").Name = "Score Changed";
        candidate.Edges.RemoveAll(edge => edge.Id == "score-finish");
        candidate.Edges.Add(Edge("score-audit", "score", "audit"));

        var diff = analyzer.Diff(baseline, candidate);

        Assert.Equal(["audit"], diff.AddedNodeIds);
        Assert.Equal(["finish"], diff.RemovedNodeIds);
        Assert.Equal(["score"], diff.ChangedNodeIds);
        Assert.Equal(["score-audit"], diff.AddedEdgeIds);
        Assert.Equal(["score-finish"], diff.RemovedEdgeIds);
    }

    private static WorkflowProcessingDefinition Dag() => new()
    {
        Id = "dag",
        Name = "DAG",
        Nodes =
        [
            Node("finish"),
            Node("score"),
            Node("load"),
            Node("start")
        ],
        Edges =
        [
            Edge("start-load", "start", "load"),
            Edge("load-score", "load", "score"),
            Edge("score-finish", "score", "finish")
        ]
    };

    private static WorkflowProcessingNode Node(string id) => new() { Id = id, Name = id };

    private static WorkflowProcessingEdge Edge(string id, string from, string to) => new()
    {
        Id = id,
        FromNodeId = from,
        ToNodeId = to
    };
}
