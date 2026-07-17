using System.Text.Json;
using AsterERP.Api.Application.Ai.Flowise;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class FlowiseGraphExecutionRuntimeTests
{
    [Fact]
    public async Task Dispatcher_UsesCursorOrder_AndStopsAfterFailure()
    {
        var classifier = new FlowiseRuntimeNodeClassifier();
        var dispatcher = new FlowiseGraphNodeDispatcher(classifier);
        var nodes = new[]
        {
            Node("a", "directReplyAgentflow"),
            Node("b", "httpAgentflow"),
            Node("c", "llmAgentflow")
        };
        var visited = new List<string>();

        var state = await dispatcher.DispatchAsync(nodes, (node, _, _) =>
        {
            visited.Add(node.Id);
            if (node.Id == "b")
            {
                throw new InvalidOperationException("failed");
            }

            return Task.FromResult<object?>(node.Id);
        });

        Assert.Equal(new[] { "a", "b" }, visited);
        Assert.Equal("FINISHED", state.Executions["a"].Status);
        Assert.Equal("FAILED", state.Executions["b"].Status);
        Assert.DoesNotContain("c", state.Executions.Keys);
    }

    [Fact]
    public async Task Dispatcher_RecordsSkippedNodes_AndUnknownTypesExplicitly()
    {
        var classifier = new FlowiseRuntimeNodeClassifier();
        var dispatcher = new FlowiseGraphNodeDispatcher(classifier);
        var nodes = new[]
        {
            Node("condition", "conditionAgentflow"),
            Node("skipped", "directReplyAgentflow"),
            Node("unknown", "futureNode")
        };

        var state = await dispatcher.DispatchAsync(nodes, (_, _, _) => Task.FromResult<object?>(null), node => node.Id == "skipped");

        Assert.Equal("SKIPPED", state.Executions["skipped"].Status);
        Assert.Equal("FAILED", state.Executions["unknown"].Status);
        Assert.Equal("UNSUPPORTED_NODE_TYPE", state.Executions["unknown"].ErrorCode);
    }

    [Fact]
    public void Classifier_CoversExistingRuntimeNodeKinds()
    {
        var classifier = new FlowiseRuntimeNodeClassifier();

        Assert.Equal(FlowiseRuntimeNodeKind.RuntimeDataModel, classifier.Classify(Node("runtime", "runtime-data-model")));
        Assert.Equal(FlowiseRuntimeNodeKind.Http, classifier.Classify(Node("http", "httpAgentflow")));
        Assert.Equal(FlowiseRuntimeNodeKind.ExecuteFlow, classifier.Classify(Node("flow", "executeFlowAgentflow")));
        Assert.Equal(FlowiseRuntimeNodeKind.CustomFunction, classifier.Classify(Node("fn", "customFunctionAgentflow")));
        Assert.Equal(FlowiseRuntimeNodeKind.Llm, classifier.Classify(Node("llm", "llmAgentflow")));
        Assert.Equal(FlowiseRuntimeNodeKind.Agent, classifier.Classify(Node("agent", "agentAgentflow")));
        Assert.Equal(FlowiseRuntimeNodeKind.DirectReply, classifier.Classify(Node("reply", "directReplyAgentflow")));
        Assert.Equal(FlowiseRuntimeNodeKind.HumanInput, classifier.Classify(Node("human", "human-input")));
        Assert.Equal(FlowiseRuntimeNodeKind.Iteration, classifier.Classify(Node("iteration", "iteration")));
        Assert.Equal(FlowiseRuntimeNodeKind.Loop, classifier.Classify(Node("loop", "loop")));
    }

    private static FlowiseRuntimeNode Node(string id, string nodeType) => new()
    {
        Id = id,
        NodeType = nodeType,
        DisplayName = nodeType,
        Data = new Dictionary<string, JsonElement>()
    };
}
