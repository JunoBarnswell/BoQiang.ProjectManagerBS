using AsterERP.Workflow.Processing.Analysis;
using AsterERP.Workflow.Processing.Definitions;
using AsterERP.Workflow.Processing.Planning;
using Xunit;

namespace AsterERP.Api.Tests.WorkflowProcessing;

public sealed class WorkflowProcessingExecutionPlannerTests
{
    [Fact]
    public void Plan_GroupsIndependentNodesIntoParallelBatches()
    {
        var planner = new WorkflowProcessingExecutionPlanner(new WorkflowProcessingGraphAnalyzer());
        var definition = new WorkflowProcessingDefinition
        {
            Id = "parallel",
            Name = "Parallel",
            Nodes =
            [
                Node("start"),
                Node("extract-a"),
                Node("extract-b"),
                Node("merge"),
                Node("finish")
            ],
            Edges =
            [
                Edge("start-a", "start", "extract-a"),
                Edge("start-b", "start", "extract-b"),
                Edge("a-merge", "extract-a", "merge"),
                Edge("b-merge", "extract-b", "merge"),
                Edge("merge-finish", "merge", "finish")
            ]
        };

        var plan = planner.Plan(definition);

        Assert.True(plan.Succeeded);
        Assert.Collection(
            plan.Batches,
            batch => Assert.Equal(["start"], batch.NodeIds),
            batch => Assert.Equal(["extract-a", "extract-b"], batch.NodeIds),
            batch => Assert.Equal(["merge"], batch.NodeIds),
            batch => Assert.Equal(["finish"], batch.NodeIds));
    }

    [Fact]
    public void Plan_ReturnsCycleNodesWhenDefinitionIsCyclic()
    {
        var planner = new WorkflowProcessingExecutionPlanner(new WorkflowProcessingGraphAnalyzer());
        var definition = new WorkflowProcessingDefinition
        {
            Id = "cyclic",
            Name = "Cyclic",
            Nodes =
            [
                Node("a"),
                Node("b")
            ],
            Edges =
            [
                Edge("a-b", "a", "b"),
                Edge("b-a", "b", "a")
            ]
        };

        var plan = planner.Plan(definition);

        Assert.False(plan.Succeeded);
        Assert.Equal(["a", "b"], plan.CycleNodeIds);
    }

    private static WorkflowProcessingNode Node(string id) => new() { Id = id, Name = id };

    private static WorkflowProcessingEdge Edge(string id, string from, string to) => new()
    {
        Id = id,
        FromNodeId = from,
        ToNodeId = to
    };
}
