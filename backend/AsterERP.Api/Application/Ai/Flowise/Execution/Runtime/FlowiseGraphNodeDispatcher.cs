namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class FlowiseGraphNodeDispatcher(FlowiseRuntimeNodeClassifier classifier)
{
    internal FlowiseGraphExecutionState Validate(IReadOnlyList<FlowiseRuntimeNode> orderedNodes)
    {
        var state = new FlowiseGraphExecutionState();
        var cursor = new FlowiseGraphCursor(orderedNodes);
        while (cursor.TryMoveNext(out var node))
        {
            if (node is null)
            {
                break;
            }

            var kind = classifier.Classify(node);
            if (kind == FlowiseRuntimeNodeKind.Unsupported)
            {
                state.Terminate(node.Id, kind, "UNSUPPORTED_NODE_TYPE", $"Unsupported Flowise node type: {node.NodeType}.");
                break;
            }

            state.Record(new FlowiseGraphNodeExecution(node.Id, kind, "READY"));
        }

        return state;
    }

    internal async Task<FlowiseGraphExecutionState> DispatchAsync(
        IReadOnlyList<FlowiseRuntimeNode> orderedNodes,
        Func<FlowiseRuntimeNode, FlowiseRuntimeNodeKind, CancellationToken, Task<object?>> executeAsync,
        Func<FlowiseRuntimeNode, bool>? shouldSkip = null,
        CancellationToken cancellationToken = default)
    {
        var state = new FlowiseGraphExecutionState();
        var cursor = new FlowiseGraphCursor(orderedNodes);
        while (cursor.TryMoveNext(out var node))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is null || state.IsTerminated)
            {
                break;
            }

            var kind = classifier.Classify(node);
            if (kind == FlowiseRuntimeNodeKind.Unsupported)
            {
                state.Terminate(node.Id, kind, "UNSUPPORTED_NODE_TYPE", $"Unsupported Flowise node type: {node.NodeType}.");
                break;
            }

            if (shouldSkip?.Invoke(node) == true)
            {
                state.Record(new FlowiseGraphNodeExecution(node.Id, kind, "SKIPPED"));
                continue;
            }

            try
            {
                var output = await executeAsync(node, kind, cancellationToken);
                state.Record(new FlowiseGraphNodeExecution(node.Id, kind, "FINISHED", output));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                state.Terminate(node.Id, kind, "FLOWISE_NODE_EXECUTION_FAILED", ex.Message);
                break;
            }
        }

        return state;
    }
}
