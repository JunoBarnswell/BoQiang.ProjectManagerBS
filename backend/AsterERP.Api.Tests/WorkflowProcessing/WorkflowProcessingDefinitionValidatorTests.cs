using AsterERP.Workflow.Processing.Definitions;
using AsterERP.Workflow.Processing.Analysis;
using AsterERP.Workflow.Processing.Validation;
using Xunit;

namespace AsterERP.Api.Tests.WorkflowProcessing;

public sealed class WorkflowProcessingDefinitionValidatorTests
{
    [Fact]
    public void Validate_ReturnsIssuesForEmptyDefinition()
    {
        var validator = new WorkflowProcessingDefinitionValidator(new WorkflowProcessingGraphAnalyzer());

        var result = validator.Validate(new WorkflowProcessingDefinition());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.ErrorCode == "DefinitionIdRequired");
        Assert.Contains(result.Issues, issue => issue.ErrorCode == "DefinitionNameRequired");
        Assert.Contains(result.Issues, issue => issue.ErrorCode == "GraphEmpty");
    }

    [Fact]
    public void Validate_DetectsDanglingEdgesAndDuplicateNodes()
    {
        var validator = new WorkflowProcessingDefinitionValidator(new WorkflowProcessingGraphAnalyzer());
        var definition = new WorkflowProcessingDefinition
        {
            Id = "wf",
            Name = "Workflow",
            Nodes =
            [
                Node("start"),
                Node("start")
            ],
            Edges =
            [
                Edge("e1", "start", "missing")
            ]
        };

        var result = validator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.ErrorCode == "NodeIdDuplicate");
        Assert.Contains(result.Issues, issue => issue.ErrorCode == "EdgeToNodeMissing");
    }

    [Fact]
    public void Validate_DetectsDagCyclesWhenAcyclicRequired()
    {
        var validator = new WorkflowProcessingDefinitionValidator(new WorkflowProcessingGraphAnalyzer());
        var definition = new WorkflowProcessingDefinition
        {
            Id = "wf",
            Name = "Workflow",
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

        var result = validator.Validate(definition);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.ErrorCode == "GraphCycleDetected");
    }

    private static WorkflowProcessingNode Node(string id) => new() { Id = id, Name = id };

    private static WorkflowProcessingEdge Edge(string id, string from, string to) => new()
    {
        Id = id,
        FromNodeId = from,
        ToNodeId = to
    };
}
