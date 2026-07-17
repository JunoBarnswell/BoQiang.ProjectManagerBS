using AsterERP.Contracts.Ai.Flowise;
using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseExecutionSnapshotBuilder(
    FlowiseRuntimeNodeClassifier nodeClassifier,
    FlowiseRuntimeNodeDataReader nodeDataReader,
    FlowiseExecutionOrderPlanner executionOrderPlanner,
    FlowiseAgentFlowEventBuilder agentFlowEventBuilder)
{
    internal IReadOnlyList<FlowiseAgentExecutedNodeDto> BuildSkippedNodeSnapshots(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executedNodes,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions)
    {
        if (branchDecisions is null || branchDecisions.Count == 0)
        {
            return [];
        }

        var executedIds = executedNodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var skippedIds = flowData.Edges
            .Where(edge => branchDecisions.TryGetValue(edge.Source, out var decision) &&
                !string.Equals(edge.Target, decision.TargetNodeId, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(decision.SourceHandle) || !string.Equals(edge.SourceHandle, decision.SourceHandle, StringComparison.OrdinalIgnoreCase)))
            .Select(edge => edge.Target)
            .Where(id => !executedIds.Contains(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return flowData.Nodes
            .Where(node => skippedIds.Contains(node.Id))
            .Select(node => BuildNodeExecutionSnapshot(flowData, node, "SKIPPED", branchDecisions: branchDecisions))
            .ToList();
    }

    internal IReadOnlyList<FlowiseAgentExecutedNodeDto> BuildExecutedData(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        IReadOnlyList<RuntimeDataModelNodeResult>? runtimeModelResults = null,
        IReadOnlyList<DirectReplyNodeResult>? directReplyResults = null,
        IReadOnlyList<HttpNodeResult>? httpResults = null,
        IReadOnlyList<ExecuteFlowNodeResult>? executeFlowResults = null,
        IReadOnlyList<CustomFunctionNodeResult>? customFunctionResults = null,
        IReadOnlyList<LlmNodeResult>? llmResults = null,
        IReadOnlyList<AgentNodeResult>? agentResults = null,
        FlowiseRuntimeNode? stoppedNode = null,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions = null)
    {
        var runtimeQueues = runtimeModelResults?
            .GroupBy(result => result.NodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => new Queue<RuntimeDataModelNodeResult>(group.OrderBy(item => item.ExecutionIndex)), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, Queue<RuntimeDataModelNodeResult>>(StringComparer.OrdinalIgnoreCase);
        var snapshots = new List<FlowiseAgentExecutedNodeDto>();
        foreach (var node in executionOrder)
        {
            if (runtimeQueues.TryGetValue(node.Id, out var queue) && queue.Count > 0)
            {
                snapshots.Add(BuildRuntimeModelExecutionSnapshot(flowData, node, queue.Dequeue()));
                continue;
            }

            snapshots.Add(BuildNodeExecutionSnapshot(
                flowData,
                node,
                stoppedNode is not null && string.Equals(node.Id, stoppedNode.Id, StringComparison.OrdinalIgnoreCase) ? "STOPPED" : "FINISHED",
                runtimeModelResults,
                directReplyResults,
                httpResults,
                executeFlowResults,
                customFunctionResults,
                llmResults,
                agentResults,
                branchDecisions));
        }

        if (runtimeModelResults is null)
        {
            return snapshots;
        }

        var orderedIds = executionOrder.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nodesById = flowData.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var result in runtimeQueues.SelectMany(item => item.Value).Concat(runtimeModelResults.Where(result => result.Iteration is not null || !orderedIds.Contains(result.NodeId))).DistinctBy(result => result.ExecutionIndex))
        {
            if (nodesById.TryGetValue(result.NodeId, out var node))
            {
                snapshots.Add(BuildRuntimeModelExecutionSnapshot(flowData, node, result));
            }
        }

        return snapshots;
    }

    internal FlowiseAgentExecutedNodeDto BuildNodeExecutionSnapshot(
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode node,
        string status,
        IReadOnlyList<RuntimeDataModelNodeResult>? runtimeModelResults = null,
        IReadOnlyList<DirectReplyNodeResult>? directReplyResults = null,
        IReadOnlyList<HttpNodeResult>? httpResults = null,
        IReadOnlyList<ExecuteFlowNodeResult>? executeFlowResults = null,
        IReadOnlyList<CustomFunctionNodeResult>? customFunctionResults = null,
        IReadOnlyList<LlmNodeResult>? llmResults = null,
        IReadOnlyList<AgentNodeResult>? agentResults = null,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions = null)
    {
        var previousNodeIds = IncomingNodeIds(flowData, node.Id);
        var nextNodeIds = executionOrderPlanner.OutgoingEdgesForExecution(flowData, node, branchDecisions)
            .Select(edge => edge.Target)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dataJson = JsonSerializer.Serialize(BuildNodeExecutionData(node, previousNodeIds, nextNodeIds, status, runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults));
        return new FlowiseAgentExecutedNodeDto
        {
            DataJson = dataJson,
            NodeId = node.Id,
            NodeLabel = agentFlowEventBuilder.NormalizeWorkflowDisplayText(node.DisplayName),
            PreviousNodeIds = previousNodeIds,
            Status = status
        };
    }

    internal FlowiseAgentExecutedNodeDto BuildRuntimeModelExecutionSnapshot(
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode node,
        RuntimeDataModelNodeResult runtimeResult)
    {
        var previousNodeIds = IncomingNodeIds(flowData, node.Id);
        var nextNodeIds = flowData.Edges
            .Where(edge => string.Equals(edge.Source, node.Id, StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.Target)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var dataJson = JsonSerializer.Serialize(BuildRuntimeDataModelExecutionData(node, previousNodeIds, nextNodeIds, "FINISHED", runtimeResult));
        return new FlowiseAgentExecutedNodeDto
        {
            DataJson = dataJson,
            NodeId = node.Id,
            NodeLabel = agentFlowEventBuilder.NormalizeWorkflowDisplayText(runtimeResult.NodeLabel),
            PreviousNodeIds = previousNodeIds,
            Status = "FINISHED"
        };
    }

    private object BuildNodeExecutionData(
        FlowiseRuntimeNode node,
        IReadOnlyList<string> previousNodeIds,
        IReadOnlyList<string> nextNodeIds,
        string status,
        IReadOnlyList<RuntimeDataModelNodeResult>? runtimeModelResults,
        IReadOnlyList<DirectReplyNodeResult>? directReplyResults,
        IReadOnlyList<HttpNodeResult>? httpResults,
        IReadOnlyList<ExecuteFlowNodeResult>? executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult>? customFunctionResults,
        IReadOnlyList<LlmNodeResult>? llmResults,
        IReadOnlyList<AgentNodeResult>? agentResults)
    {
        var runtimeResult = runtimeModelResults?.FirstOrDefault(result => string.Equals(result.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));
        var directReplyResult = directReplyResults?.FirstOrDefault(result => string.Equals(result.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));
        var httpResult = httpResults?.FirstOrDefault(result => string.Equals(result.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));
        var executeFlowResult = executeFlowResults?.FirstOrDefault(result => string.Equals(result.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));
        var customFunctionResult = customFunctionResults?.FirstOrDefault(result => string.Equals(result.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));
        var llmResult = llmResults?.FirstOrDefault(result => string.Equals(result.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));
        var agentResult = agentResults?.FirstOrDefault(result => string.Equals(result.NodeId, node.Id, StringComparison.OrdinalIgnoreCase));
        if (httpResult is not null)
        {
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow,
                executionIndex = httpResult.ExecutionIndex,
                input = new
                {
                    http = new
                    {
                        httpResult.Method,
                        httpResult.Url,
                        httpResult.BodyType,
                        httpResult.Body,
                        httpResult.ResponseType
                    }
                },
                output = new
                {
                    http = new
                    {
                        data = httpResult.Data,
                        status = httpResult.Status,
                        statusText = httpResult.StatusText,
                        headers = httpResult.Headers
                    }
                }
            };
        }

        if (executeFlowResult is not null)
        {
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow,
                executionIndex = executeFlowResult.ExecutionIndex,
                input = new
                {
                    messages = new[]
                    {
                        new { role = "user", content = executeFlowResult.Input }
                    },
                    selectedFlowId = executeFlowResult.SelectedFlowId,
                    selectedFlowName = executeFlowResult.SelectedFlowName,
                    returnResponseAs = executeFlowResult.ReturnResponseAs
                },
                output = new
                {
                    content = executeFlowResult.Content,
                    status = executeFlowResult.Status,
                    sourceDocuments = executeFlowResult.SourceDocuments,
                    usedTools = executeFlowResult.UsedTools,
                    agentExecutedData = executeFlowResult.AgentExecutedData
                }
            };
        }

        if (customFunctionResult is not null)
        {
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow,
                executionIndex = customFunctionResult.ExecutionIndex,
                input = new
                {
                    inputVariables = customFunctionResult.InputVariables,
                    code = customFunctionResult.Code
                },
                output = new
                {
                    content = customFunctionResult.Content
                },
                state = new
                {
                    updated = true
                }
            };
        }

        if (llmResult is not null)
        {
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow,
                executionIndex = llmResult.ExecutionIndex,
                input = new
                {
                    messages = llmResult.Messages,
                    returnResponseAs = llmResult.ReturnResponseAs
                },
                output = new
                {
                    content = llmResult.Content,
                    structuredOutput = llmResult.StructuredOutput,
                    timeMetadata = new
                    {
                        start = llmResult.StartedAt,
                        end = llmResult.CompletedAt,
                        delta = (int)Math.Max(0, (llmResult.CompletedAt - llmResult.StartedAt).TotalMilliseconds)
                    }
                },
                state = new
                {
                    updated = true
                }
            };
        }

        if (agentResult is not null)
        {
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow,
                executionIndex = agentResult.ExecutionIndex,
                input = new
                {
                    messages = agentResult.Messages,
                    tools = agentResult.ToolsJson,
                    knowledgeDocumentStores = agentResult.KnowledgeDocumentStoresJson,
                    knowledgeVectorEmbeddings = agentResult.KnowledgeVectorEmbeddingsJson,
                    returnResponseAs = agentResult.ReturnResponseAs
                },
                output = new
                {
                    content = agentResult.Content,
                    structuredOutput = agentResult.StructuredOutput,
                    usedTools = agentResult.UsedTools,
                    sourceDocuments = agentResult.SourceDocuments,
                    timeMetadata = new
                    {
                        start = agentResult.StartedAt,
                        end = agentResult.CompletedAt,
                        delta = (int)Math.Max(0, (agentResult.CompletedAt - agentResult.StartedAt).TotalMilliseconds)
                    }
                },
                state = new
                {
                    updated = true
                }
            };
        }

        if (directReplyResult is not null)
        {
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow,
                input = new
                {
                    message = directReplyResult.Template
                },
                output = new
                {
                    content = directReplyResult.Content
                }
            };
        }

        if (runtimeResult is null && nodeClassifier.IsLoopNode(node))
        {
            var loopOutput = BuildLoopOutput(node);
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow,
                input = new
                {
                    nodeID = loopOutput.NodeId,
                    maxLoopCount = loopOutput.MaxLoopCount
                },
                output = new
                {
                    content = loopOutput.Content,
                    nodeID = loopOutput.NodeId,
                    maxLoopCount = loopOutput.MaxLoopCount,
                    fallbackMessage = loopOutput.FallbackMessage
                }
            };
        }

        if (runtimeResult is null)
        {
            return new
            {
                node.Data,
                nodeId = node.Id,
                nodeType = node.NodeType,
                previousNodeIds,
                nextNodeIds,
                status,
                executedAt = DateTime.UtcNow
            };
        }

        return BuildRuntimeDataModelExecutionData(node, previousNodeIds, nextNodeIds, status, runtimeResult);
    }

    private static object BuildRuntimeDataModelExecutionData(
        FlowiseRuntimeNode node,
        IReadOnlyList<string> previousNodeIds,
        IReadOnlyList<string> nextNodeIds,
        string status,
        RuntimeDataModelNodeResult result) => new
        {
            node.Data,
            nodeId = node.Id,
            nodeType = node.NodeType,
            previousNodeIds,
            nextNodeIds,
            status,
            executedAt = DateTime.UtcNow,
            executionIndex = result.ExecutionIndex,
            input = new
            {
                result.ModelCode,
                result.Request
            },
            output = new
            {
                result.Response.Total,
                result.Response.PageIndex,
                result.Response.PageSize,
                RowCount = result.Response.Rows.Count,
                result.Response.Fields,
                result.Response.Rows
            },
            metrics = new
            {
                Total = result.Response.Total,
                RowCount = result.Response.Rows.Count,
                FieldCount = result.Response.Fields.Count
            },
            iteration = result.Iteration
        };

    private LoopOutput BuildLoopOutput(FlowiseRuntimeNode loopNode)
    {
        var loopBackTo = nodeDataReader.ReadNodeInputString(loopNode.Data, "loopBackToNode") ?? string.Empty;
        var targetNodeId = ResolveLoopTargetNodeId(loopBackTo);
        var targetLabel = ResolveLoopTargetLabel(loopBackTo, targetNodeId);
        var maxLoopCount = ResolveLoopMaxCount(loopNode);
        var fallbackMessage = ResolveRuntimeLiteralString(loopNode.Data, "fallbackMessage");
        return new LoopOutput(
            targetNodeId,
            maxLoopCount,
            string.IsNullOrWhiteSpace(fallbackMessage) ? $"Loop completed after reaching maximum iteration count of {maxLoopCount}." : fallbackMessage,
            string.IsNullOrWhiteSpace(targetNodeId) ? "Loop back" : $"Loop back to {targetLabel} ({targetNodeId})");
    }

    private static string ResolveLoopTargetNodeId(string loopBackToNode)
    {
        if (string.IsNullOrWhiteSpace(loopBackToNode))
        {
            return string.Empty;
        }

        var trimmed = loopBackToNode.Trim();
        var separatorIndex = trimmed.IndexOf('-');
        return separatorIndex <= 0 ? trimmed : trimmed[..separatorIndex];
    }

    private static string ResolveLoopTargetLabel(string loopBackToNode, string targetNodeId)
    {
        if (string.IsNullOrWhiteSpace(loopBackToNode))
        {
            return targetNodeId;
        }

        var trimmed = loopBackToNode.Trim();
        var separatorIndex = trimmed.IndexOf('-');
        return separatorIndex < 0 || separatorIndex == trimmed.Length - 1 ? targetNodeId : trimmed[(separatorIndex + 1)..];
    }

    private int ResolveLoopMaxCount(FlowiseRuntimeNode loopNode)
    {
        var maxLoopCount = nodeDataReader.ReadNodeInputInt(loopNode.Data, "maxLoopCount") ?? nodeDataReader.ReadNodeInputInt(loopNode.Data, "maxLoops") ?? 5;
        return Math.Clamp(maxLoopCount, 1, 100);
    }

    private string? ResolveRuntimeLiteralString(IReadOnlyDictionary<string, JsonElement> data, string propertyName) =>
        nodeDataReader.ReadNodeInputString(data, propertyName) ??
        ReadNestedDataString(data, "inputs", propertyName) ??
        ReadNestedDataString(data, "config", propertyName);

    private static string? ReadNestedDataString(IReadOnlyDictionary<string, JsonElement> data, string parentPropertyName, string propertyName)
    {
        if (!data.TryGetValue(parentPropertyName, out var parent) || parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

    private static IReadOnlyList<string> IncomingNodeIds(FlowiseRuntimeFlowData flowData, string nodeId) =>
        flowData.Edges
            .Where(edge => string.Equals(edge.Target, nodeId, StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
