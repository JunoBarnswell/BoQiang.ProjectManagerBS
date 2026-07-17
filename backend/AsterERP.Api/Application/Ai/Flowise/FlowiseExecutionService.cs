using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed partial class FlowiseExecutionService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    IAiModelRouter modelRouter,
    AiKernelChatRuntime chatRuntime,
    IRuntimeDataModelService runtimeDataModelService,
    IFlowiseDocumentStoreService documentStoreService,
    AiKernelFunctionService kernelFunctionService,
    IHttpClientFactory httpClientFactory,
    FlowiseRuntimeNodeClassifier nodeClassifier,
    FlowiseRuntimeNodeDataReader nodeDataReader,
    FlowiseDocumentStoreReferenceParser documentStoreReferenceParser,
    FlowiseAgentToolCallParser agentToolCallParser,
    FlowiseNodeMessageBuilder nodeMessageBuilder,
    FlowiseStructuredOutputBuilder structuredOutputBuilder,
    FlowiseRuntimeFlowDataParser flowDataParser,
    FlowiseAgentFlowEventBuilder agentFlowEventBuilder,
    FlowiseExecutionSnapshotBuilder executionSnapshotBuilder,
    FlowiseConditionEvaluator conditionEvaluator,
    FlowiseExecutionTemplateResolver templateResolver,
    FlowiseKeyValueInputReader keyValueInputReader,
    FlowiseRuntimeNodeInputResolver runtimeNodeInputResolver,
    FlowiseStateUpdateApplier stateUpdateApplier,
    FlowiseExecutionOrderPlanner executionOrderPlanner,
    FlowiseVariableResolver variableResolver,
    FlowiseOutputReferenceResolver outputReferenceResolver,
    FlowiseExecutionResultBuilder executionResultBuilder,
    IFlowiseExecutionTrackingService executionTrackingService,
    FlowisePermissionGuard permissionGuard) : IFlowiseExecutionService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 500;
    private const int HttpNodeTimeoutSeconds = 10;
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GridPageResult<FlowiseExecutionDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseExecutionsView, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseExecutionEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.ResourceName.Contains(keyword) || item.TraceId.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            var workspaceId = query.WorkspaceId.Trim();
            dbQuery = dbQuery.Where(item => item.WorkspaceId == workspaceId);
        }

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
        {
            var flowType = query.ResourceType.Contains("agent", StringComparison.OrdinalIgnoreCase) ? "Agentflow" : "Chatflow";
            dbQuery = dbQuery.Where(item => item.FlowType == flowType);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePageIndex(query.PageIndex), NormalizePageSize(query.PageSize), total);
        return new GridPageResult<FlowiseExecutionDto>
        {
            Total = total.Value,
            Items = rows.Select(FlowiseMapper.MapExecution).ToList()
        };
    }

    public async Task<FlowiseExecutionDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseExecutionsView, PermissionCodes.FlowiseView);
        var entity = await LoadExecutionAsync(id, cancellationToken);
        return FlowiseMapper.MapExecution(entity);
    }

    public async Task<FlowiseExecutionDto> StartAsync(FlowiseExecutionStartRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            throw new ValidationException("缺少 Flowise 资源 Id", ErrorCodes.ParameterInvalid);
        }

        var inputJson = NormalizeJson(request.InputJson);
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await db.Queryable<FlowiseExecutionEntity>()
                .FirstAsync(item => !item.IsDeleted && item.ResourceId == request.ResourceId && item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return FlowiseMapper.MapExecution(existing);
            }
        }

        var chatflow = await db.Queryable<FlowiseChatFlowEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == request.ResourceId.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise ChatFlow 不存在", ErrorCodes.ParameterInvalid);
        EnsureRun(chatflow.Type);

        var workspace = workspaceContext.Resolve();
        var execution = new FlowiseExecutionEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ResourceName = chatflow.Name,
            FlowType = ToFlowType(chatflow.Type),
            Status = "Running",
            InputJson = inputJson,
            OutputJson = "{}",
            TraceId = Guid.NewGuid().ToString("N"),
            StartedAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey
        };
        await db.Insertable(execution).ExecuteCommandAsync(cancellationToken);
        var scheduleLog = await executionTrackingService.CreateScheduleTriggerLogAsync(chatflow, execution, inputJson, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var context = FlowiseExecutionContext.FromRequest(request);
            var result = await ExecuteDefinitionAsync(chatflow, execution.TraceId, context, cancellationToken);
            stopwatch.Stop();
            execution.Status = result.Status;
            execution.ActionJson = result.ActionJson;
            execution.OutputJson = JsonSerializer.Serialize(result);
            execution.SourceDocumentsJson = JsonSerializer.Serialize(result.SourceDocuments);
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
        }
        catch (ValidationException ex)
        {
            stopwatch.Stop();
            execution.Status = "Failed";
            execution.ErrorCode = ex.Code.ToString();
            execution.ErrorMessage = ex.Message;
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
        }

        await db.Updateable(execution).ExecuteCommandAsync(cancellationToken);
        await executionTrackingService.CompleteScheduleTriggerLogAsync(scheduleLog, execution, cancellationToken);
        await executionTrackingService.WriteExecutionAuditAsync(chatflow, execution, cancellationToken);
        return FlowiseMapper.MapExecution(execution);
    }

    public async Task<FlowiseExecutionDto> StartMcpAsync(
        FlowiseChatFlowEntity chatflow,
        string inputJson,
        string? question,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (chatflow.IsDeleted)
        {
            throw new ValidationException("MCP server not found", ErrorCodes.ParameterInvalid);
        }

        var normalizedInputJson = NormalizeJson(inputJson);
        var normalizedIdempotencyKey = NormalizeOptional(idempotencyKey);
        if (!string.IsNullOrWhiteSpace(normalizedIdempotencyKey))
        {
            var existing = await db.Queryable<FlowiseExecutionEntity>()
                .FirstAsync(item => !item.IsDeleted && item.ResourceId == chatflow.Id && item.IdempotencyKey == normalizedIdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return FlowiseMapper.MapExecution(existing);
            }
        }

        var execution = new FlowiseExecutionEntity
        {
            TenantId = chatflow.TenantId,
            AppCode = chatflow.AppCode,
            OwnerUserId = chatflow.OwnerUserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ResourceName = chatflow.Name,
            FlowType = ToFlowType(chatflow.Type),
            Status = "Running",
            InputJson = normalizedInputJson,
            OutputJson = "{}",
            TraceId = Guid.NewGuid().ToString("N"),
            StartedAt = DateTime.UtcNow,
            IdempotencyKey = normalizedIdempotencyKey
        };
        await db.Insertable(execution).ExecuteCommandAsync(cancellationToken);
        var scheduleLog = await executionTrackingService.CreateScheduleTriggerLogAsync(chatflow, execution, inputJson, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await ExecuteDefinitionAsync(chatflow, execution.TraceId, FlowiseExecutionContext.FromQuestion(question), cancellationToken);
            stopwatch.Stop();
            execution.Status = result.Status;
            execution.ActionJson = result.ActionJson;
            execution.OutputJson = JsonSerializer.Serialize(result);
            execution.SourceDocumentsJson = JsonSerializer.Serialize(result.SourceDocuments);
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
        }
        catch (ValidationException ex)
        {
            stopwatch.Stop();
            execution.Status = "Failed";
            execution.ErrorCode = ex.Code.ToString();
            execution.ErrorMessage = ex.Message;
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            execution.Status = "Failed";
            execution.ErrorCode = "FLOWISE_MCP_EXECUTION_FAILED";
            execution.ErrorMessage = ex.Message;
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
        }

        await db.Updateable(execution).ExecuteCommandAsync(cancellationToken);
        await executionTrackingService.CompleteScheduleTriggerLogAsync(scheduleLog, execution, cancellationToken);
        await executionTrackingService.WriteMcpExecutionAuditAsync(chatflow, execution, cancellationToken);
        return FlowiseMapper.MapExecution(execution);
    }

    public async Task<FlowiseExecutionDto> StreamAsync(
        FlowiseExecutionStartRequest request,
        Func<string, object?, CancellationToken, Task> emitAsync,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            throw new ValidationException("缺少 Flowise 资源 Id", ErrorCodes.ParameterInvalid);
        }

        var inputJson = NormalizeJson(request.InputJson);
        var idempotencyKey = NormalizeOptional(request.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await db.Queryable<FlowiseExecutionEntity>()
                .FirstAsync(item => !item.IsDeleted && item.ResourceId == request.ResourceId && item.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                await emitAsync("metadata", new { executionId = existing.Id, traceId = existing.TraceId, status = existing.Status }, cancellationToken);
                await emitAsync("end", FlowiseMapper.MapExecution(existing), cancellationToken);
                return FlowiseMapper.MapExecution(existing);
            }
        }

        var chatflow = await db.Queryable<FlowiseChatFlowEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == request.ResourceId.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise ChatFlow 不存在", ErrorCodes.ParameterInvalid);
        EnsureRun(chatflow.Type);

        var workspace = workspaceContext.Resolve();
        var execution = new FlowiseExecutionEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ResourceName = chatflow.Name,
            FlowType = ToFlowType(chatflow.Type),
            Status = "Running",
            InputJson = inputJson,
            OutputJson = "{}",
            TraceId = Guid.NewGuid().ToString("N"),
            StartedAt = DateTime.UtcNow,
            IdempotencyKey = idempotencyKey
        };
        await db.Insertable(execution).ExecuteCommandAsync(cancellationToken);
        var scheduleLog = await executionTrackingService.CreateScheduleTriggerLogAsync(chatflow, execution, inputJson, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await emitAsync("start", new { executionId = execution.Id, traceId = execution.TraceId }, cancellationToken);
            var context = FlowiseExecutionContext.FromRequest(request);
            var result = await ExecuteDefinitionStreamingAsync(chatflow, execution.TraceId, context, emitAsync, cancellationToken);
            stopwatch.Stop();
            execution.Status = result.Status;
            execution.ActionJson = result.ActionJson;
            execution.OutputJson = JsonSerializer.Serialize(result);
            execution.SourceDocumentsJson = JsonSerializer.Serialize(result.SourceDocuments);
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            execution.Status = "Cancelled";
            execution.ErrorCode = "FLOWISE_STREAM_ABORTED";
            execution.ErrorMessage = "Flowise stream aborted.";
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
            await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("TERMINATED", execution.TraceId, execution.ErrorMessage), CancellationToken.None);
            await emitAsync("abort", new { executionId = execution.Id, traceId = execution.TraceId, error = execution.ErrorMessage }, CancellationToken.None);
        }
        catch (ValidationException ex)
        {
            stopwatch.Stop();
            execution.Status = "Failed";
            execution.ErrorCode = ex.Code.ToString();
            execution.ErrorMessage = ex.Message;
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
            await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("ERROR", execution.TraceId, execution.ErrorMessage), cancellationToken);
            await emitAsync("error", new { errorCode = execution.ErrorCode, message = execution.ErrorMessage, traceId = execution.TraceId }, cancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            execution.Status = "Failed";
            execution.ErrorCode = "FLOWISE_STREAM_FAILED";
            execution.ErrorMessage = ex.Message;
            execution.DurationMs = (int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue);
            execution.CompletedAt = DateTime.UtcNow;
            execution.UpdatedTime = DateTime.UtcNow;
            await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("ERROR", execution.TraceId, execution.ErrorMessage), cancellationToken);
            await emitAsync("error", new { errorCode = execution.ErrorCode, message = execution.ErrorMessage, traceId = execution.TraceId }, cancellationToken);
        }

        await db.Updateable(execution).ExecuteCommandAsync(CancellationToken.None);
        await executionTrackingService.CompleteScheduleTriggerLogAsync(scheduleLog, execution, CancellationToken.None);
        await executionTrackingService.WriteExecutionAuditAsync(chatflow, execution, CancellationToken.None);
        var dto = FlowiseMapper.MapExecution(execution);
        if (!cancellationToken.IsCancellationRequested)
        {
            await emitAsync("metadata", new { executionId = dto.Id, status = dto.Status, traceId = dto.TraceId }, CancellationToken.None);
            await emitAsync("end", dto, CancellationToken.None);
        }

        return dto;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseExecutionsManage, PermissionCodes.FlowiseManage);
        var entity = await LoadExecutionAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    private async Task<FlowiseExecutionRuntimeResult> ExecuteDefinitionStreamingAsync(
        FlowiseChatFlowEntity chatflow,
        string traceId,
        FlowiseExecutionContext context,
        Func<string, object?, CancellationToken, Task> emitAsync,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteDefinitionAsync(chatflow, traceId, context, cancellationToken);
        await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("INPROGRESS", traceId), cancellationToken);
        var flowNodes = flowDataParser.Parse(chatflow.FlowData).Nodes;
        foreach (var node in result.AgentExecutedData)
        {
            var flowNode = flowNodes.FirstOrDefault(item =>
                string.Equals(item.Id, node.NodeId, StringComparison.OrdinalIgnoreCase));
            if (flowNode is not null)
            {
                await emitAsync("nextAgentFlow", agentFlowEventBuilder.BuildNextAgentFlowEvent(flowNode, "INPROGRESS"), cancellationToken);
            }

            await emitAsync("agentFlowExecutedData", new[] { node }, cancellationToken);
            if (flowNode is not null)
            {
                await emitAsync("nextAgentFlow", agentFlowEventBuilder.BuildNextAgentFlowEvent(flowNode, node.Status), cancellationToken);
            }
        }

        foreach (var token in ChunkAnswer(result.Answer))
        {
            await emitAsync("token", token, cancellationToken);
        }

        await emitAsync("sourceDocuments", result.SourceDocuments, cancellationToken);
        await emitAsync("usedTools", result.UsedTools, cancellationToken);
        await emitAsync("agentReasoning", result.AgentReasoning, cancellationToken);
        await emitAsync("agentFlowExecutedData", result.AgentExecutedData, cancellationToken);
        await emitAsync("artifacts", result.Artifacts, cancellationToken);
        await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent(
            string.Equals(result.Status, "Stopped", StringComparison.OrdinalIgnoreCase) ? "STOPPED" : "FINISHED",
            traceId), cancellationToken);
        return result;
    }

    private async Task<FlowiseExecutionRuntimeResult> ExecuteDefinitionStreamingLegacyAsync(
        FlowiseChatFlowEntity chatflow,
        string traceId,
        FlowiseExecutionContext context,
        Func<string, object?, CancellationToken, Task> emitAsync,
        CancellationToken cancellationToken)
    {
        var flowData = flowDataParser.Parse(chatflow.FlowData);
        if (flowData.Nodes.Count == 0)
        {
            throw new ValidationException("Flowise 画布没有节点，不能执行", ErrorCodes.ParameterInvalid);
        }

        var nodeKeys = flowData.Nodes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var danglingEdge = flowData.Edges.FirstOrDefault(edge => !nodeKeys.Contains(edge.Source) || !nodeKeys.Contains(edge.Target));
        if (danglingEdge is not null)
        {
            throw new ValidationException($"Flowise 画布边 {danglingEdge.Id} 引用了不存在的节点", ErrorCodes.ParameterInvalid);
        }
        EnsureAgentflowStartNode(chatflow, flowData);

        EnsureSupportedNodeTypes(flowData);
        var branchDecisions = conditionEvaluator.ResolveBranchDecisions(flowData, context);
        var executionOrder = ExpandLoopExecutionOrder(flowData, executionOrderPlanner.CreateCursor(flowData, branchDecisions).Nodes);
        var entryNodes = executionOrder.Where(node => IncomingNodeIds(flowData, node.Id).Count == 0).Select(node => node.Id).ToList();
        var resumeNode = ResolveResumeNode(executionOrder, context.HumanInput);
        var initialHumanInputNode = context.HumanInput is null ? executionOrder.FirstOrDefault(nodeClassifier.IsHumanInputNode) : null;
        var activeExecutionOrder = ResolveActiveExecutionOrder(executionOrder, resumeNode);
        var graphResults = initialHumanInputNode is null
            ? await ExecuteGraphNodesAsync(chatflow, flowData, activeExecutionOrder, context, cancellationToken)
            : new FlowiseGraphNodeResults();
        var runtimeModelResults = graphResults.RuntimeModel;
        var httpResults = graphResults.Http;
        var executeFlowResults = graphResults.ExecuteFlow;
        var customFunctionResults = graphResults.CustomFunction;
        var llmResults = graphResults.Llm;
        var agentResults = graphResults.Agent;
        var directReplyResults = initialHumanInputNode is null
            ? ResolveDirectReplyNodes(flowData, activeExecutionOrder, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults)
            : [];
        var executedData = executionSnapshotBuilder.BuildExecutedData(flowData, activeExecutionOrder, runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions: branchDecisions)
            .Concat(executionSnapshotBuilder.BuildSkippedNodeSnapshots(flowData, activeExecutionOrder, branchDecisions))
            .ToList();
        var usedTools = BuildUsedTools(flowData, runtimeModelResults);
        var sourceDocuments = flowData.Nodes
            .Where(node => node.NodeType.Contains("document", StringComparison.OrdinalIgnoreCase) || node.NodeType.Contains("retriever", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(node => new FlowiseSourceDocumentDto
            {
                Content = $"Flowise node source: {node.DisplayName}.",
                MetadataJson = JsonSerializer.Serialize(node.Data),
                Score = 1,
                SourceId = node.Id
            })
            .ToList();
        sourceDocuments.AddRange(executionResultBuilder.BuildRuntimeModelSourceDocuments(runtimeModelResults));
        var agentReasoning = BuildAgentReasoning(chatflow, flowData, sourceDocuments, usedTools);
        var prompt = ResolvePrompt(flowData, context.Question);
        var directReplyAnswer = BuildDirectReplyAnswer(directReplyResults);
        var executeFlowAnswer = string.IsNullOrWhiteSpace(directReplyAnswer) ? BuildExecuteFlowAnswer(executeFlowResults) : directReplyAnswer;
        var customFunctionAnswer = string.IsNullOrWhiteSpace(executeFlowAnswer) ? BuildCustomFunctionAnswer(customFunctionResults) : executeFlowAnswer;
        var llmAnswer = string.IsNullOrWhiteSpace(customFunctionAnswer) ? BuildLlmAnswer(llmResults) : customFunctionAnswer;
        var agentAnswer = string.IsNullOrWhiteSpace(llmAnswer) ? BuildAgentAnswer(agentResults) : llmAnswer;
        var runtimeModelAnswer = string.IsNullOrWhiteSpace(agentAnswer) ? executionResultBuilder.BuildRuntimeModelAnswer(runtimeModelResults) : agentAnswer;
        var answerParts = new List<string>();

        await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("INPROGRESS", traceId), cancellationToken);
        if (initialHumanInputNode is not null)
        {
            var executedBeforeStop = await EmitNodeFlowProgressUntilStopAsync(flowData, executionOrder, initialHumanInputNode, runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions, emitAsync, cancellationToken);
            var humanInputAction = agentFlowEventBuilder.BuildHumanInputAction(initialHumanInputNode, context.Question);
            await emitAsync("agentFlowExecutedData", executedBeforeStop, cancellationToken);
            await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("STOPPED", traceId), cancellationToken);
            await emitAsync("action", humanInputAction, cancellationToken);

            return CompleteRuntimeResult(new FlowiseExecutionRuntimeResult
            {
                Action = humanInputAction,
                ActionJson = JsonSerializer.Serialize(humanInputAction),
                Answer = agentFlowEventBuilder.ResolveHumanInputMessage(initialHumanInputNode, context.Question),
                AgentExecutedData = executedBeforeStop,
                AgentReasoning = agentReasoning,
                Artifacts = [],
                EdgeCount = flowData.Edges.Count,
                EntryNodes = entryNodes,
                ExecutedAt = DateTime.UtcNow,
                NodeCount = flowData.Nodes.Count,
                ResourceId = chatflow.Id,
                ResourceName = chatflow.Name,
                ResourceType = chatflow.Type,
                SourceDocuments = sourceDocuments,
                Status = "Stopped",
                TraceId = traceId,
                UsedTools = usedTools
            }, context);
        }

        var previousExecutedData = ParseAgentExecutedData(context.HumanInput?.PreviousExecutionDataJson)
            .Where(item => !string.Equals(item.Status, "STOPPED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (context.HumanInput is not null && IsHumanInputReject(context.HumanInput))
        {
            var rejectedSnapshot = resumeNode is null
                ? null
                : executionSnapshotBuilder.BuildNodeExecutionSnapshot(flowData, resumeNode, "REJECTED", runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions);
            var rejectedData = rejectedSnapshot is null ? previousExecutedData : previousExecutedData.Concat([rejectedSnapshot]).ToList();
            await emitAsync("agentFlowExecutedData", rejectedData, cancellationToken);
            await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("FINISHED", traceId), cancellationToken);

            return CompleteRuntimeResult(new FlowiseExecutionRuntimeResult
            {
                Answer = $"Human Input rejected: {context.HumanInput.Label}",
                AgentExecutedData = rejectedData,
                AgentReasoning = agentReasoning,
                Artifacts = [],
                EdgeCount = flowData.Edges.Count,
                EntryNodes = entryNodes,
                ExecutedAt = DateTime.UtcNow,
                NodeCount = flowData.Nodes.Count,
                ResourceId = chatflow.Id,
                ResourceName = chatflow.Name,
                ResourceType = chatflow.Type,
                SourceDocuments = sourceDocuments,
                Status = "Completed",
                TraceId = traceId,
                UsedTools = usedTools
            }, context);
        }

        await EmitNodeFlowProgressAsync(flowData, activeExecutionOrder, runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions, emitAsync, cancellationToken);
        if (previousExecutedData.Count > 0)
        {
            executedData = previousExecutedData.Concat(executedData).ToList();
        }

        if (!string.IsNullOrWhiteSpace(runtimeModelAnswer))
        {
            foreach (var token in ChunkAnswer(runtimeModelAnswer))
            {
                answerParts.Add(token);
                await emitAsync("token", token, cancellationToken);
            }

            await emitAsync("sourceDocuments", sourceDocuments, cancellationToken);
            await emitAsync("usedTools", usedTools, cancellationToken);
            await emitAsync("agentReasoning", agentReasoning, cancellationToken);
            await emitAsync("agentFlowExecutedData", executedData, cancellationToken);
            await emitAsync("artifacts", Array.Empty<object>(), cancellationToken);
            await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("FINISHED", traceId), cancellationToken);

            return CompleteRuntimeResult(new FlowiseExecutionRuntimeResult
            {
                Answer = string.Concat(answerParts),
                AgentExecutedData = executedData,
                AgentReasoning = agentReasoning,
                Artifacts = [],
                EdgeCount = flowData.Edges.Count,
                EntryNodes = entryNodes,
                ExecutedAt = DateTime.UtcNow,
                NodeCount = flowData.Nodes.Count,
                ResourceId = chatflow.Id,
                ResourceName = chatflow.Name,
                ResourceType = chatflow.Type,
                SourceDocuments = sourceDocuments,
                Status = "Completed",
                TraceId = traceId,
                UsedTools = usedTools
            }, context);
        }

        var modelConfigId = ResolveModelConfigId(flowData);
        var endpoint = await modelRouter.ResolveAsync(modelConfigId, cancellationToken);

        await foreach (var chunk in chatRuntime.StreamAsync(new AiKernelChatRequest
        {
            AgentName = chatflow.Name,
            Endpoint = endpoint,
            Messages = BuildChatMessages(chatflow, flowData, runtimeModelResults, context, prompt)
        }, cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk.ContentDelta))
            {
                continue;
            }

            answerParts.Add(chunk.ContentDelta);
            await emitAsync("token", chunk.ContentDelta, cancellationToken);
        }

        await emitAsync("sourceDocuments", sourceDocuments, cancellationToken);
        await emitAsync("usedTools", usedTools, cancellationToken);
        await emitAsync("agentReasoning", agentReasoning, cancellationToken);
        await emitAsync("agentFlowExecutedData", executedData, cancellationToken);
        await emitAsync("artifacts", Array.Empty<object>(), cancellationToken);
        await emitAsync("agentFlowEvent", agentFlowEventBuilder.BuildAgentFlowEvent("FINISHED", traceId), cancellationToken);

        return CompleteRuntimeResult(new FlowiseExecutionRuntimeResult
        {
            Answer = string.Concat(answerParts),
            AgentExecutedData = executedData,
            AgentReasoning = agentReasoning,
            Artifacts = [],
            EdgeCount = flowData.Edges.Count,
            EntryNodes = entryNodes,
            ExecutedAt = DateTime.UtcNow,
            NodeCount = flowData.Nodes.Count,
            ResourceId = chatflow.Id,
            ResourceName = chatflow.Name,
            ResourceType = chatflow.Type,
            SourceDocuments = sourceDocuments,
            Status = "Completed",
            TraceId = traceId,
            UsedTools = usedTools
        }, context);
    }

    private async Task<FlowiseExecutionRuntimeResult> ExecuteDefinitionAsync(FlowiseChatFlowEntity chatflow, string traceId, FlowiseExecutionContext context, CancellationToken cancellationToken)
    {
        var flowData = flowDataParser.Parse(chatflow.FlowData);
        if (flowData.Nodes.Count == 0)
        {
            throw new ValidationException("Flowise 画布没有节点，不能执行", ErrorCodes.ParameterInvalid);
        }

        var nodeKeys = flowData.Nodes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var danglingEdge = flowData.Edges.FirstOrDefault(edge => !nodeKeys.Contains(edge.Source) || !nodeKeys.Contains(edge.Target));
        if (danglingEdge is not null)
        {
            throw new ValidationException($"Flowise 画布边 {danglingEdge.Id} 引用了不存在的节点", ErrorCodes.ParameterInvalid);
        }
        EnsureAgentflowStartNode(chatflow, flowData);

        EnsureSupportedNodeTypes(flowData);
        var branchDecisions = conditionEvaluator.ResolveBranchDecisions(flowData, context);
        var executionOrder = ExpandLoopExecutionOrder(flowData, executionOrderPlanner.CreateCursor(flowData, branchDecisions).Nodes);
        var entryNodes = executionOrder.Where(node => IncomingNodeIds(flowData, node.Id).Count == 0).Select(node => node.Id).ToList();
        var resumeNode = ResolveResumeNode(executionOrder, context.HumanInput);
        var initialHumanInputNode = context.HumanInput is null ? executionOrder.FirstOrDefault(nodeClassifier.IsHumanInputNode) : null;
        var activeExecutionOrder = ResolveActiveExecutionOrder(executionOrder, resumeNode);
        var graphResults = initialHumanInputNode is null
            ? await ExecuteGraphNodesAsync(chatflow, flowData, activeExecutionOrder, context, cancellationToken)
            : new FlowiseGraphNodeResults();
        var runtimeModelResults = graphResults.RuntimeModel;
        var httpResults = graphResults.Http;
        var executeFlowResults = graphResults.ExecuteFlow;
        var customFunctionResults = graphResults.CustomFunction;
        var llmResults = graphResults.Llm;
        var agentResults = graphResults.Agent;
        var directReplyResults = initialHumanInputNode is null
            ? ResolveDirectReplyNodes(flowData, activeExecutionOrder, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults)
            : [];
        var executedData = executionSnapshotBuilder.BuildExecutedData(flowData, activeExecutionOrder, runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions: branchDecisions)
            .Concat(executionSnapshotBuilder.BuildSkippedNodeSnapshots(flowData, activeExecutionOrder, branchDecisions))
            .ToList();
        var usedTools = BuildUsedTools(flowData, runtimeModelResults);
        var sourceDocuments = flowData.Nodes
            .Where(node => node.NodeType.Contains("document", StringComparison.OrdinalIgnoreCase) || node.NodeType.Contains("retriever", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(node => new FlowiseSourceDocumentDto
            {
                Content = $"Flowise node source: {node.DisplayName}.",
                MetadataJson = JsonSerializer.Serialize(node.Data),
                Score = 1,
                SourceId = node.Id
            })
            .ToList();
        sourceDocuments.AddRange(executionResultBuilder.BuildRuntimeModelSourceDocuments(runtimeModelResults));
        var agentReasoning = BuildAgentReasoning(chatflow, flowData, sourceDocuments, usedTools);
        var prompt = ResolvePrompt(flowData, context.Question);
        if (initialHumanInputNode is not null)
        {
            var humanInputAction = agentFlowEventBuilder.BuildHumanInputAction(initialHumanInputNode, context.Question);
            return CompleteRuntimeResult(new FlowiseExecutionRuntimeResult
            {
                Action = humanInputAction,
                ActionJson = JsonSerializer.Serialize(humanInputAction),
                Answer = agentFlowEventBuilder.ResolveHumanInputMessage(initialHumanInputNode, context.Question),
                AgentExecutedData = executionSnapshotBuilder.BuildExecutedData(flowData, TakeUntilNodeInclusive(executionOrder, initialHumanInputNode), runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, initialHumanInputNode, branchDecisions),
                AgentReasoning = agentReasoning,
                Artifacts = [],
                EdgeCount = flowData.Edges.Count,
                EntryNodes = entryNodes,
                ExecutedAt = DateTime.UtcNow,
                NodeCount = flowData.Nodes.Count,
                ResourceId = chatflow.Id,
                ResourceName = chatflow.Name,
                ResourceType = chatflow.Type,
                SourceDocuments = sourceDocuments,
                Status = "Stopped",
                TraceId = traceId,
                UsedTools = usedTools
            }, context);
        }

        var previousExecutedData = ParseAgentExecutedData(context.HumanInput?.PreviousExecutionDataJson)
            .Where(item => !string.Equals(item.Status, "STOPPED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (context.HumanInput is not null && IsHumanInputReject(context.HumanInput))
        {
            var rejectedSnapshot = resumeNode is null
                ? null
                : executionSnapshotBuilder.BuildNodeExecutionSnapshot(flowData, resumeNode, "REJECTED", runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions);
            var rejectedData = rejectedSnapshot is null ? previousExecutedData : previousExecutedData.Concat([rejectedSnapshot]).ToList();
            return CompleteRuntimeResult(new FlowiseExecutionRuntimeResult
            {
                Answer = $"Human Input rejected: {context.HumanInput.Label}",
                AgentExecutedData = rejectedData,
                AgentReasoning = agentReasoning,
                Artifacts = [],
                EdgeCount = flowData.Edges.Count,
                EntryNodes = entryNodes,
                ExecutedAt = DateTime.UtcNow,
                NodeCount = flowData.Nodes.Count,
                ResourceId = chatflow.Id,
                ResourceName = chatflow.Name,
                ResourceType = chatflow.Type,
                SourceDocuments = sourceDocuments,
                Status = "Completed",
                TraceId = traceId,
                UsedTools = usedTools
            }, context);
        }

        if (previousExecutedData.Count > 0)
        {
            executedData = previousExecutedData.Concat(executedData).ToList();
        }

        var answer = BuildDirectReplyAnswer(directReplyResults);
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = BuildExecuteFlowAnswer(executeFlowResults);
        }
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = BuildCustomFunctionAnswer(customFunctionResults);
        }
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = BuildLlmAnswer(llmResults);
        }
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = BuildAgentAnswer(agentResults);
        }
        if (string.IsNullOrWhiteSpace(answer))
        {
            answer = executionResultBuilder.BuildRuntimeModelAnswer(runtimeModelResults);
        }
        if (string.IsNullOrWhiteSpace(answer))
        {
            var modelConfigId = ResolveModelConfigId(flowData);
            var endpoint = await modelRouter.ResolveAsync(modelConfigId, cancellationToken);
            answer = await chatRuntime.CompleteAsync(new AiKernelChatRequest
            {
                AgentName = chatflow.Name,
                Endpoint = endpoint,
                Messages = BuildChatMessages(chatflow, flowData, runtimeModelResults, context, prompt)
            }, cancellationToken);
        }

        return CompleteRuntimeResult(new FlowiseExecutionRuntimeResult
        {
            Answer = answer,
            AgentExecutedData = executedData,
            AgentReasoning = agentReasoning,
            Artifacts = [],
            EdgeCount = flowData.Edges.Count,
            EntryNodes = entryNodes,
            ExecutedAt = DateTime.UtcNow,
            NodeCount = flowData.Nodes.Count,
            ResourceId = chatflow.Id,
            ResourceName = chatflow.Name,
            ResourceType = chatflow.Type,
            SourceDocuments = sourceDocuments,
            Status = "Completed",
            TraceId = traceId,
            UsedTools = usedTools
        }, context);
    }

    private async Task<IReadOnlyList<RuntimeDataModelNodeResult>> ExecuteRuntimeDataModelNodesAsync(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        CancellationToken cancellationToken)
    {
        var persistedState = await LoadPersistedFlowStateAsync(flowData, context, cancellationToken);
        InitializeStartFlowState(flowData, context, persistedState);
        var results = new List<RuntimeDataModelNodeResult>();
        var executionIndex = 0;
        foreach (var node in executionOrder)
        {
            if (nodeClassifier.IsRuntimeDataModelNode(node))
            {
                var result = await ExecuteRuntimeDataModelNodeAsync(node, context, null, results, cancellationToken);
                result.ExecutionIndex = executionIndex++;
                results.Add(result);
                continue;
            }

            if (nodeClassifier.IsIterationNode(node))
            {
                results.AddRange(await ExecuteIterationRuntimeDataModelNodesAsync(flowData, node, context, results, cancellationToken));
                continue;
            }

            if (nodeClassifier.IsLoopNode(node))
            {
                context.RegisterLoopExecution(node.Id);
                ApplyLoopStateUpdate(node, context, results);
            }
        }

        return results;
    }

    private async Task<FlowiseGraphNodeResults> ExecuteGraphNodesAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        CancellationToken cancellationToken)
    {
        var results = new FlowiseGraphNodeResults();
        var persistedState = await LoadPersistedFlowStateAsync(flowData, context, cancellationToken);
        InitializeStartFlowState(flowData, context, persistedState);
        var dispatcher = new FlowiseGraphNodeDispatcher(nodeClassifier);
        var state = await dispatcher.DispatchAsync(executionOrder, async (node, kind, token) =>
        {
            switch (kind)
            {
                case FlowiseRuntimeNodeKind.Start:
                case FlowiseRuntimeNodeKind.Condition:
                case FlowiseRuntimeNodeKind.HumanInput:
                    return null;
                case FlowiseRuntimeNodeKind.RuntimeDataModel:
                {
                    var result = await ExecuteRuntimeDataModelNodeAsync(node, context, null, results.RuntimeModel, token);
                    result.ExecutionIndex = results.RuntimeModel.Count;
                    results.RuntimeModel.Add(result);
                    return result;
                }
                case FlowiseRuntimeNodeKind.Http:
                {
                    var result = await ExecuteHttpNodeAsync(node, context, results.RuntimeModel, results.Http, token);
                    result.ExecutionIndex = results.Http.Count;
                    results.Http.Add(result);
                    return result;
                }
                case FlowiseRuntimeNodeKind.ExecuteFlow:
                {
                    var result = await ExecuteFlowNodeAsync(chatflow, node, context, results.RuntimeModel, results.Http, results.ExecuteFlow, token);
                    result.ExecutionIndex = results.ExecuteFlow.Count;
                    results.ExecuteFlow.Add(result);
                    return result;
                }
                case FlowiseRuntimeNodeKind.CustomFunction:
                {
                    var result = ExecuteCustomFunctionNode(node, context, results.RuntimeModel, results.Http, results.ExecuteFlow, results.CustomFunction);
                    result.ExecutionIndex = results.CustomFunction.Count;
                    results.CustomFunction.Add(result);
                    return result;
                }
                case FlowiseRuntimeNodeKind.Llm:
                {
                    var result = (await ExecuteLlmNodesAsync(chatflow, flowData, [node], context, results.RuntimeModel, results.Http, results.ExecuteFlow, results.CustomFunction, token, results.Llm)).Last();
                    if (!ReferenceEquals(result, results.Llm.LastOrDefault())) results.Llm.Add(result);
                    return result;
                }
                case FlowiseRuntimeNodeKind.Agent:
                {
                    var result = (await ExecuteAgentNodesAsync(chatflow, flowData, [node], context, results.RuntimeModel, results.Http, results.ExecuteFlow, results.CustomFunction, results.Llm, token, results.Agent)).Last();
                    if (!ReferenceEquals(result, results.Agent.LastOrDefault())) results.Agent.Add(result);
                    return result;
                }
                case FlowiseRuntimeNodeKind.Loop:
                    context.RegisterLoopExecution(node.Id);
                    ApplyLoopStateUpdate(node, context, results.RuntimeModel);
                    return null;
                case FlowiseRuntimeNodeKind.Iteration:
                    results.RuntimeModel.AddRange(await ExecuteIterationRuntimeDataModelNodesAsync(flowData, node, context, results.RuntimeModel, token));
                    return null;
                case FlowiseRuntimeNodeKind.DirectReply:
                    return null;
                default:
                    throw new ValidationException($"UNSUPPORTED_NODE_TYPE: {node.NodeType}", ErrorCodes.ParameterInvalid);
            }
        }, node => nodeClassifier.IsConditionNode(node) && !executionOrder.Contains(node), cancellationToken);

        var failure = state.Executions.Values.FirstOrDefault(item => string.Equals(item.Status, "FAILED", StringComparison.OrdinalIgnoreCase));
        if (failure is not null)
        {
            throw new ValidationException($"{failure.ErrorCode}: {failure.ErrorMessage}", ErrorCodes.ParameterInvalid);
        }

        return results;
    }

    private async Task<IReadOnlyDictionary<string, object?>> LoadPersistedFlowStateAsync(
        FlowiseRuntimeFlowData flowData,
        FlowiseExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startNode = flowData.Nodes.FirstOrDefault(nodeClassifier.IsStartNode);
        if (startNode is null || !nodeDataReader.ReadNodeInputBool(startNode.Data, "startPersistState"))
        {
            return new Dictionary<string, object?>();
        }

        var chatId = context.NormalizedChatId;
        var sessionId = context.NormalizedSessionId;
        if (string.IsNullOrWhiteSpace(context.ResourceId) || (string.IsNullOrWhiteSpace(chatId) && string.IsNullOrWhiteSpace(sessionId)))
        {
            return new Dictionary<string, object?>();
        }

        var candidates = await db.Queryable<FlowiseExecutionEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == context.ResourceId && item.Status == "Completed")
            .WhereIF(!string.IsNullOrWhiteSpace(chatId), item => item.InputJson.Contains($"\"chatId\":\"{chatId}\"") || item.InputJson.Contains($"\"chatId\": \"{chatId}\""))
            .WhereIF(!string.IsNullOrWhiteSpace(sessionId), item => item.InputJson.Contains($"\"sessionId\":\"{sessionId}\"") || item.InputJson.Contains($"\"sessionId\": \"{sessionId}\"") || item.InputJson.Contains($"\"SessionId\":\"{sessionId}\"") || item.InputJson.Contains($"\"SessionId\": \"{sessionId}\""))
            .OrderByDescending(item => item.CreatedTime)
            .Select(item => item.OutputJson)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var outputJson in candidates)
        {
            var state = ReadFlowStateFromOutputJson(outputJson);
            if (state.Count > 0)
            {
                return state;
            }
        }

        return new Dictionary<string, object?>();
    }

    private void InitializeStartFlowState(
        FlowiseRuntimeFlowData flowData,
        FlowiseExecutionContext context,
        IReadOnlyDictionary<string, object?> persistedState)
    {
        var startNode = flowData.Nodes.FirstOrDefault(nodeClassifier.IsStartNode);
        if (startNode is null)
        {
            return;
        }

        foreach (var state in runtimeNodeInputResolver.ReadStartStateUpdates(startNode.Data))
        {
            context.SetFlowState(state.Key, state.Value);
        }

        if (!nodeDataReader.ReadNodeInputBool(startNode.Data, "startPersistState"))
        {
            return;
        }

        foreach (var state in persistedState)
        {
            context.SetFlowState(state.Key, state.Value);
        }
    }

    private static FlowiseExecutionRuntimeResult CompleteRuntimeResult(
        FlowiseExecutionRuntimeResult result,
        FlowiseExecutionContext context)
    {
        result.FlowState = context.SnapshotFlowState();
        return result;
    }

    private void EnsureSupportedNodeTypes(FlowiseRuntimeFlowData flowData)
    {
        var validation = new FlowiseGraphNodeDispatcher(nodeClassifier).Validate(flowData.Nodes);
        var unsupported = validation.Executions.Values.FirstOrDefault(item => string.Equals(item.ErrorCode, "UNSUPPORTED_NODE_TYPE", StringComparison.OrdinalIgnoreCase));
        if (unsupported is not null)
        {
            throw new ValidationException(
                $"UNSUPPORTED_NODE_TYPE: {unsupported.ErrorMessage}",
                ErrorCodes.ParameterInvalid);
        }
    }

    private async Task<IReadOnlyList<RuntimeDataModelNodeResult>> ExecuteIterationRuntimeDataModelNodesAsync(
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode iterationNode,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults,
        CancellationToken cancellationToken)
    {
        var childNodes = flowData.Nodes
            .Where(node => string.Equals(node.ParentId, iterationNode.Id, StringComparison.OrdinalIgnoreCase))
            .Select(node => node with { ParentId = null })
            .ToList();
        if (childNodes.Count == 0)
        {
            return [];
        }

        var childIds = childNodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var childFlowData = new FlowiseRuntimeFlowData
        {
            Nodes = childNodes,
            Edges = flowData.Edges
                .Where(edge => childIds.Contains(edge.Source) && childIds.Contains(edge.Target))
                .ToList()
        };
        var childExecutionOrder = executionOrderPlanner.Plan(childFlowData);
        var iterationItems = ReadIterationItems(iterationNode);
        var maxLoops = Math.Clamp(nodeDataReader.ReadNodeInputInt(iterationNode.Data, "maxLoops") ?? iterationItems.Count, 0, 100);
        var results = new List<RuntimeDataModelNodeResult>();
        for (var index = 0; index < Math.Min(iterationItems.Count, maxLoops); index++)
        {
            var iterationContext = new FlowiseIterationContext(index, iterationItems[index], index == 0, index == iterationItems.Count - 1, iterationNode.Id);
            foreach (var childNode in childExecutionOrder.Where(nodeClassifier.IsRuntimeDataModelNode))
            {
                var visibleResults = previousResults.Concat(results).ToList();
                var result = await ExecuteRuntimeDataModelNodeAsync(childNode, context, iterationContext, visibleResults, cancellationToken);
                result.ExecutionIndex = results.Count;
                results.Add(result);
            }
        }

        return results;
    }

    private async Task<RuntimeDataModelNodeResult> ExecuteRuntimeDataModelNodeAsync(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults,
        CancellationToken cancellationToken)
    {
        var modelCode = ResolveRuntimeNodeInputString(node.Data, "modelCode", context, iterationContext, previousResults);
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            throw new ValidationException($"Runtime Data Model 节点 {node.DisplayName} 缺少 modelCode", ErrorCodes.ParameterInvalid);
        }

        var delayMs = NormalizeRuntimeDelayMs(nodeDataReader.ReadNodeInputInt(node.Data, "delayMs") ?? 0);
        if (delayMs > 0)
        {
            await Task.Delay(delayMs, cancellationToken);
        }

        var request = new RuntimeQueryRequest(
            NormalizePageIndex(ResolveRuntimeNodeInputInt(node.Data, "pageIndex", context, iterationContext, previousResults) ?? 1),
            NormalizePageSize(ResolveRuntimeNodeInputInt(node.Data, "pageSize", context, iterationContext, previousResults) ?? DefaultPageSize),
            NormalizeOptional(ResolveRuntimeNodeInputString(node.Data, "keyword", context, iterationContext, previousResults)),
            runtimeNodeInputResolver.ReadRuntimeFilters(node.Data, "filters", context, iterationContext, previousResults),
            runtimeNodeInputResolver.ReadRuntimeSorts(node.Data, "sorts", context, iterationContext, previousResults),
            NormalizeOptional(ResolveRuntimeNodeInputString(node.Data, "pageCode", context, iterationContext, previousResults)));
        var response = await runtimeDataModelService.QueryAsync(modelCode.Trim(), request, cancellationToken);
        return new RuntimeDataModelNodeResult
        {
            NodeId = node.Id,
            NodeLabel = iterationContext is null ? node.DisplayName : $"{node.DisplayName} #{iterationContext.Index + 1}",
            ModelCode = modelCode.Trim(),
            Request = request,
            Response = response,
            Iteration = iterationContext
        };
    }

    private async Task<IReadOnlyList<HttpNodeResult>> ExecuteHttpNodesAsync(
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        CancellationToken cancellationToken)
    {
        var results = new List<HttpNodeResult>();
        var executionIndex = 0;
        foreach (var node in executionOrder.Where(nodeClassifier.IsHttpNode))
        {
            var result = await ExecuteHttpNodeAsync(node, context, runtimeModelResults, results, cancellationToken);
            result.ExecutionIndex = executionIndex++;
            results.Add(result);
        }

        return results;
    }

    private async Task<IReadOnlyList<ExecuteFlowNodeResult>> ExecuteFlowNodesAsync(
        FlowiseChatFlowEntity currentChatflow,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        CancellationToken cancellationToken)
    {
        var results = new List<ExecuteFlowNodeResult>();
        var executionIndex = 0;
        foreach (var node in executionOrder.Where(nodeClassifier.IsExecuteFlowNode))
        {
            var result = await ExecuteFlowNodeAsync(currentChatflow, node, context, runtimeModelResults, httpResults, results, cancellationToken);
            result.ExecutionIndex = executionIndex++;
            results.Add(result);
        }

        return results;
    }

    private async Task<ExecuteFlowNodeResult> ExecuteFlowNodeAsync(
        FlowiseChatFlowEntity currentChatflow,
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> previousExecuteFlowResults,
        CancellationToken cancellationToken)
    {
        var selectedFlowId = templateResolver.ResolveExecuteFlowInput(nodeDataReader.ReadNodeInputString(node.Data, "executeFlowSelectedFlow"), context, runtimeModelResults, httpResults, previousExecuteFlowResults);
        if (string.IsNullOrWhiteSpace(selectedFlowId))
        {
            throw new ValidationException($"Execute Flow 节点 {node.DisplayName} 缺少目标工作流", ErrorCodes.ParameterInvalid);
        }

        if (string.Equals(selectedFlowId.Trim(), currentChatflow.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Execute Flow 不能调用当前工作流自身", ErrorCodes.ParameterInvalid);
        }

        var childChatflow = await db.Queryable<FlowiseChatFlowEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == selectedFlowId.Trim(), cancellationToken)
            ?? throw new ValidationException("Execute Flow 目标工作流不存在", ErrorCodes.ParameterInvalid);
        var input = templateResolver.ResolveExecuteFlowInput(nodeDataReader.ReadNodeInputString(node.Data, "executeFlowInput"), context, runtimeModelResults, httpResults, previousExecuteFlowResults);
        if (string.IsNullOrWhiteSpace(input))
        {
            input = context.Question ?? string.Empty;
        }

        var childContext = new FlowiseExecutionContext(
            input,
            childChatflow.Id,
            context.ChatId,
            context.SessionId,
            context.ChatHistory,
            context.Form,
            context.Webhook);
        var childResult = await ExecuteDefinitionAsync(childChatflow, Guid.NewGuid().ToString("N"), childContext, cancellationToken);
        stateUpdateApplier.ApplyExecuteFlowStateUpdate(node, context, runtimeModelResults, httpResults, previousExecuteFlowResults, childResult.Answer);
        return new ExecuteFlowNodeResult
        {
            NodeId = node.Id,
            NodeLabel = node.DisplayName,
            SelectedFlowId = childChatflow.Id,
            SelectedFlowName = childChatflow.Name,
            Input = input,
            ReturnResponseAs = nodeDataReader.ReadNodeInputString(node.Data, "executeFlowReturnResponseAs") ?? "userMessage",
            Content = childResult.Answer,
            Status = childResult.Status,
            SourceDocuments = childResult.SourceDocuments,
            UsedTools = childResult.UsedTools,
            AgentExecutedData = childResult.AgentExecutedData
        };
    }

    private IReadOnlyList<CustomFunctionNodeResult> ExecuteCustomFunctionNodes(
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults)
    {
        var results = new List<CustomFunctionNodeResult>();
        var executionIndex = 0;
        foreach (var node in executionOrder.Where(nodeClassifier.IsCustomFunctionNode))
        {
            var result = ExecuteCustomFunctionNode(node, context, runtimeModelResults, httpResults, executeFlowResults, results);
            result.ExecutionIndex = executionIndex++;
            results.Add(result);
        }

        return results;
    }

    private CustomFunctionNodeResult ExecuteCustomFunctionNode(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> previousCustomFunctionResults)
    {
        var inputVariables = ReadCustomFunctionInputVariables(node.Data, context, runtimeModelResults, httpResults, executeFlowResults, previousCustomFunctionResults);
        var code = nodeDataReader.ReadNodeInputString(node.Data, "customFunctionJavascriptFunction") ?? string.Empty;
        var content = EvaluateCustomFunctionExpression(code, context, inputVariables, runtimeModelResults, httpResults, executeFlowResults, previousCustomFunctionResults);
        stateUpdateApplier.ApplyCustomFunctionStateUpdate(node, context, runtimeModelResults, httpResults, executeFlowResults, previousCustomFunctionResults, content);
        return new CustomFunctionNodeResult
        {
            NodeId = node.Id,
            NodeLabel = node.DisplayName,
            Code = code,
            InputVariables = inputVariables,
            Content = content
        };
    }

    private IReadOnlyDictionary<string, string> ReadCustomFunctionInputVariables(
        IReadOnlyDictionary<string, JsonElement> data,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, "customFunctionInputVariables", out var value))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var document = JsonDocument.Parse(value.GetString() ?? "[]");
                return ReadCustomFunctionInputVariableArray(document.RootElement, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults);
            }
            catch (JsonException)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        return ReadCustomFunctionInputVariableArray(value, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults);
    }

    private IReadOnlyDictionary<string, string> ReadCustomFunctionInputVariableArray(
        JsonElement value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = FlowiseJsonElementReader.ReadString(item, "variableName")
                ?? FlowiseJsonElementReader.ReadString(item, "key")
                ?? FlowiseJsonElementReader.ReadString(item, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var rawValue = FlowiseJsonElementReader.ReadJsonPropertyAsString(item, "variableValue");
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                rawValue = FlowiseJsonElementReader.ReadJsonPropertyAsString(item, "value");
            }

            result[name.Trim()] = templateResolver.ResolveCustomFunctionTemplate(rawValue, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults);
        }

        return result;
    }

    private async Task<IReadOnlyList<LlmNodeResult>> ExecuteLlmNodesAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        CancellationToken cancellationToken,
        IReadOnlyList<LlmNodeResult>? previousResults = null)
    {
        var results = previousResults?.ToList() ?? [];
        var executionIndex = 0;
        foreach (var node in executionOrder.Where(nodeClassifier.IsLlmAgentNode))
        {
            var priorResults = results.ToList();
            var messages = nodeMessageBuilder.BuildLlmMessages(chatflow, flowData, node, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, priorResults);
            var modelConfigId = nodeDataReader.ReadNodeInputString(node.Data, "llmModelConfigId") ??
                nodeDataReader.ReadNodeInputString(node.Data, "modelConfigId") ??
                nodeDataReader.ReadNodeInputString(node.Data, "llmModel");
            var endpoint = await modelRouter.ResolveAsync(modelConfigId, cancellationToken);
            var startedAt = DateTime.UtcNow;
            var content = await chatRuntime.CompleteAsync(new AiKernelChatRequest
            {
                AgentName = string.IsNullOrWhiteSpace(node.DisplayName) ? chatflow.Name : node.DisplayName,
                Endpoint = endpoint,
                Messages = messages
            }, cancellationToken);
            var completedAt = DateTime.UtcNow;
            var structuredOutput = structuredOutputBuilder.BuildLlmOutput(node, content);
            stateUpdateApplier.ApplyLlmStateUpdate(node, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, priorResults, content, structuredOutput);
            results.Add(new LlmNodeResult
            {
                Content = content,
                ExecutionIndex = executionIndex++ + (previousResults?.Count ?? 0),
                Messages = messages.Select(message => new LlmMessageDto(message.Role.Label, message.Content ?? string.Empty)).ToList(),
                NodeId = node.Id,
                NodeLabel = node.DisplayName,
                ReturnResponseAs = nodeDataReader.ReadNodeInputString(node.Data, "llmReturnResponseAs") ?? "userMessage",
                StartedAt = startedAt,
                CompletedAt = completedAt,
                StructuredOutput = structuredOutput
            });
        }

        return results;
    }

    private async Task<IReadOnlyList<AgentNodeResult>> ExecuteAgentNodesAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        CancellationToken cancellationToken,
        IReadOnlyList<AgentNodeResult>? previousResults = null)
    {
        var results = previousResults?.ToList() ?? [];
        var executionIndex = 0;
        foreach (var node in executionOrder.Where(nodeClassifier.IsAgentAgentNode))
        {
            var previousAgentResults = results.ToList();
            var knowledgeContext = await BuildAgentKnowledgeContextAsync(node, context, cancellationToken);
            var toolContext = await ExecuteAgentConfiguredToolsAsync(flowData, node, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, previousAgentResults, cancellationToken);
            var messages = nodeMessageBuilder.BuildAgentMessages(chatflow, flowData, node, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, previousAgentResults, knowledgeContext, toolContext);
            var modelConfigId = nodeDataReader.ReadNodeInputString(node.Data, "agentModelConfigId") ??
                nodeDataReader.ReadNodeInputString(node.Data, "modelConfigId") ??
                nodeDataReader.ReadNodeInputString(node.Data, "agentModel");
            var endpoint = await modelRouter.ResolveAsync(modelConfigId, cancellationToken);
            var startedAt = DateTime.UtcNow;
            var content = await chatRuntime.CompleteAsync(new AiKernelChatRequest
            {
                AgentName = string.IsNullOrWhiteSpace(node.DisplayName) ? chatflow.Name : node.DisplayName,
                Endpoint = endpoint,
                Messages = messages
            }, cancellationToken);
            var completedAt = DateTime.UtcNow;
            var structuredOutput = structuredOutputBuilder.BuildAgentOutput(node, content);
            stateUpdateApplier.ApplyAgentStateUpdate(node, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, previousAgentResults, content, structuredOutput);
            results.Add(new AgentNodeResult
            {
                Content = content,
                ExecutionIndex = executionIndex++ + (previousResults?.Count ?? 0),
                Messages = messages.Select(message => new AgentMessageDto(message.Role.Label, message.Content ?? string.Empty)).ToList(),
                NodeId = node.Id,
                NodeLabel = node.DisplayName,
                ReturnResponseAs = nodeDataReader.ReadNodeInputString(node.Data, "agentReturnResponseAs") ?? "userMessage",
                SourceDocuments = knowledgeContext.SourceDocuments,
                UsedTools = toolContext.UsedTools,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                StructuredOutput = structuredOutput,
                ToolsJson = NormalizeNodeJsonInput(node.Data, "agentTools"),
                KnowledgeDocumentStoresJson = NormalizeNodeJsonInput(node.Data, "agentKnowledgeDocumentStores"),
                KnowledgeVectorEmbeddingsJson = NormalizeNodeJsonInput(node.Data, "agentKnowledgeVSEmbeddings")
            });
        }

        return results;
    }

    private async Task<AgentToolContext> ExecuteAgentConfiguredToolsAsync(
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults,
        CancellationToken cancellationToken)
    {
        var toolCalls = agentToolCallParser.ReadToolCalls(node.Data, nodeDataReader, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
        var usedTools = BuildConnectedRuntimeDataModelTools(flowData, node, runtimeModelResults).ToList();
        if (toolCalls.Count == 0)
        {
            return usedTools.Count == 0 ? AgentToolContext.Empty : new AgentToolContext(usedTools);
        }

        foreach (var call in toolCalls)
        {
            var request = new AiToolInvokeRequest
            {
                ConversationId = context.ChatId,
                RunId = context.SessionId,
                WorkMode = "Agent",
                Arguments = call.Arguments,
                ArgumentsJson = JsonSerializer.Serialize(call.Arguments, CaseInsensitiveJsonOptions)
            };
            var response = await kernelFunctionService.InvokeAsync(call.ToolCode, request, cancellationToken);
            usedTools.Add(new FlowiseUsedToolDto
            {
                Tool = call.ToolCode,
                InputJson = request.ArgumentsJson ?? "{}",
                OutputJson = JsonSerializer.Serialize(new
                {
                    response.ResultSummary,
                    response.Content,
                    response.EvidenceJson,
                    response.OutputType,
                    invocation = response.Invocation
                }, CaseInsensitiveJsonOptions)
            });
        }

        return new AgentToolContext(usedTools);
    }

    private IReadOnlyList<FlowiseUsedToolDto> BuildConnectedRuntimeDataModelTools(
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode agentNode,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults)
    {
        if (runtimeModelResults.Count == 0)
        {
            return [];
        }

        var incomingNodeIds = IncomingNodeIds(flowData, agentNode.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (incomingNodeIds.Count == 0)
        {
            return [];
        }

        return runtimeModelResults
            .Where(result => incomingNodeIds.Contains(result.NodeId))
            .Select(result => new FlowiseUsedToolDto
            {
                Tool = result.NodeLabel,
                InputJson = JsonSerializer.Serialize(new { result.ModelCode, result.Request }, CaseInsensitiveJsonOptions),
                OutputJson = JsonSerializer.Serialize(new
                {
                    result.Response.Fields,
                    result.Response.Rows,
                    result.Response.Total,
                    result.Response.PageIndex,
                    result.Response.PageSize,
                    result.Response.CellSpans,
                    content = executionResultBuilder.BuildRuntimeModelSummary(result)
                }, CaseInsensitiveJsonOptions)
            })
            .ToList();
    }

    private async Task<AgentKnowledgeContext> BuildAgentKnowledgeContextAsync(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        CancellationToken cancellationToken)
    {
        var references = documentStoreReferenceParser.ReadReferences(node.Data, nodeDataReader);
        if (references.Count == 0)
        {
            return AgentKnowledgeContext.Empty;
        }

        var sourceDocuments = new List<FlowiseSourceDocumentDto>();
        var query = string.IsNullOrWhiteSpace(context.Question) ? " " : context.Question.Trim();
        foreach (var reference in references)
        {
            if (string.IsNullOrWhiteSpace(reference.StoreId))
            {
                continue;
            }

            var result = await documentStoreService.QueryAsync(new FlowiseDocumentStoreQueryRequest
            {
                StoreId = reference.StoreId,
                Query = query,
                Limit = reference.TopK
            }, cancellationToken);
            sourceDocuments.AddRange(result.Chunks.Select(chunk => new FlowiseSourceDocumentDto
            {
                Content = chunk.Content,
                MetadataJson = documentStoreReferenceParser.MergeMetadata(chunk.MetadataJson, reference.StoreId, reference.Name),
                SourceId = chunk.Id
            }));
        }

        return new AgentKnowledgeContext(sourceDocuments);
    }

    private string NormalizeNodeJsonInput(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return "[]";
        }

        return FlowiseJsonElementReader.NormalizeJsonElement(value);
    }

    private async Task<HttpNodeResult> ExecuteHttpNodeAsync(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> previousHttpResults,
        CancellationToken cancellationToken)
    {
        var method = FlowiseHttpNodeMessageFactory.NormalizeHttpMethod(nodeDataReader.ReadNodeInputString(node.Data, "method"));
        var url = templateResolver.ResolveHttpNodeInput(nodeDataReader.ReadNodeInputString(node.Data, "url"), context, runtimeModelResults, previousHttpResults);
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new ValidationException($"HTTP 节点 {node.DisplayName} 缺少有效 http/https URL", ErrorCodes.ParameterInvalid);
        }

        var queryParams = keyValueInputReader.Read(node.Data, "queryParams", context, runtimeModelResults, previousHttpResults);
        var finalUri = FlowiseHttpNodeMessageFactory.BuildHttpUri(uri, queryParams);
        using var request = new HttpRequestMessage(new HttpMethod(method), finalUri);
        foreach (var header in keyValueInputReader.Read(node.Data, "headers", context, runtimeModelResults, previousHttpResults))
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var bodyType = nodeDataReader.ReadNodeInputString(node.Data, "bodyType") ?? "json";
        var body = templateResolver.ResolveHttpNodeInput(nodeDataReader.ReadNodeInputString(node.Data, "body"), context, runtimeModelResults, previousHttpResults);
        if (method is not "GET" && !string.IsNullOrWhiteSpace(body))
        {
            request.Content = FlowiseHttpNodeMessageFactory.BuildHttpContent(bodyType, body);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(HttpNodeTimeoutSeconds));
        var client = httpClientFactory.CreateClient();
        using var response = await client.SendAsync(request, timeout.Token);
        var responseBytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
        var responseType = nodeDataReader.ReadNodeInputString(node.Data, "responseType") ?? "json";
        var responseData = FlowiseHttpNodeMessageFactory.BuildHttpResponseData(responseType, responseBytes);
        return new HttpNodeResult
        {
            NodeId = node.Id,
            NodeLabel = node.DisplayName,
            Method = method,
            Url = finalUri.ToString(),
            BodyType = bodyType,
            Body = body,
            ResponseType = responseType,
            Status = (int)response.StatusCode,
            StatusText = response.ReasonPhrase ?? string.Empty,
            Headers = response.Headers.Concat(response.Content.Headers)
                .ToDictionary(header => header.Key, header => string.Join(",", header.Value), StringComparer.OrdinalIgnoreCase),
            Data = responseData
        };
    }

    private string EvaluateCustomFunctionExpression(
        string code,
        FlowiseExecutionContext context,
        IReadOnlyDictionary<string, string> inputVariables,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults)
    {
        var expression = ExtractCustomFunctionReturnExpression(code);
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        if (TryEvaluateCustomFunctionObjectLiteral(expression, context, inputVariables, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, out var objectResult))
        {
            return objectResult;
        }

        var pieces = SplitCustomFunctionConcatExpression(expression);
        if (pieces.Count == 0)
        {
            return templateResolver.ResolveCustomFunctionTemplate(UnquoteCustomFunctionLiteral(expression), context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, inputVariables);
        }

        var builder = new StringBuilder();
        foreach (var piece in pieces)
        {
            builder.Append(ResolveCustomFunctionExpressionPiece(piece, context, inputVariables, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults));
        }

        return builder.ToString();
    }

    private static string ExtractCustomFunctionReturnExpression(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var match = Regex.Match(code, @"return\s+(?<expression>[\s\S]*?);?\s*(?:\}?\s*)$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["expression"].Value.Trim() : code.Trim();
    }

    private static IReadOnlyList<string> SplitCustomFunctionConcatExpression(string expression)
    {
        var pieces = new List<string>();
        var start = 0;
        var inString = false;
        var stringQuote = '\0';
        var escape = false;
        var depth = 0;
        for (var index = 0; index < expression.Length; index++)
        {
            var current = expression[index];
            if (escape)
            {
                escape = false;
                continue;
            }

            if (current == '\\' && inString)
            {
                escape = true;
                continue;
            }

            if (current is '\'' or '"' or '`')
            {
                if (!inString)
                {
                    inString = true;
                    stringQuote = current;
                    continue;
                }

                if (stringQuote == current)
                {
                    inString = false;
                }

                continue;
            }

            if (inString)
            {
                continue;
            }

            if (current is '{' or '[' or '(')
            {
                depth++;
                continue;
            }

            if (current is '}' or ']' or ')')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (current == '+' && depth == 0)
            {
                pieces.Add(expression[start..index].Trim());
                start = index + 1;
            }
        }

        if (pieces.Count == 0)
        {
            return [];
        }

        pieces.Add(expression[start..].Trim());
        return pieces;
    }

    private string ResolveCustomFunctionExpressionPiece(
        string piece,
        FlowiseExecutionContext context,
        IReadOnlyDictionary<string, string> inputVariables,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults)
    {
        var trimmed = piece.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        if (trimmed.StartsWith('$') && inputVariables.TryGetValue(trimmed.TrimStart('$'), out var inputValue))
        {
            return inputValue;
        }

        return templateResolver.ResolveCustomFunctionTemplate(UnquoteCustomFunctionLiteral(trimmed), context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, inputVariables);
    }

    private static string UnquoteCustomFunctionLiteral(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && ((trimmed[0] == '\'' && trimmed[^1] == '\'') || (trimmed[0] == '"' && trimmed[^1] == '"') || (trimmed[0] == '`' && trimmed[^1] == '`')))
        {
            return trimmed[1..^1]
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\r", StringComparison.Ordinal)
                .Replace("\\t", "\t", StringComparison.Ordinal)
                .Replace("\\\"", "\"", StringComparison.Ordinal)
                .Replace("\\'", "'", StringComparison.Ordinal)
                .Replace("\\`", "`", StringComparison.Ordinal);
        }

        return trimmed;
    }

    private bool TryEvaluateCustomFunctionObjectLiteral(
        string expression,
        FlowiseExecutionContext context,
        IReadOnlyDictionary<string, string> inputVariables,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        out string result)
    {
        result = string.Empty;
        var trimmed = expression.Trim();
        if (!trimmed.StartsWith('{') || !trimmed.EndsWith('}'))
        {
            return false;
        }

        var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(trimmed.Trim('{', '}'), @"(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?<value>[^,]+)"))
        {
            var key = match.Groups["key"].Value;
            var value = ResolveCustomFunctionExpressionPiece(match.Groups["value"].Value, context, inputVariables, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults);
            entries[key] = value;
        }

        result = JsonSerializer.Serialize(entries);
        return true;
    }

    private IReadOnlyList<DirectReplyNodeResult> ResolveDirectReplyNodes(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults)
    {
        var orderIndexByNodeId = executionOrder
            .Select((node, index) => new { node.Id, Index = index })
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Min(item => item.Index), StringComparer.OrdinalIgnoreCase);
        var results = new List<DirectReplyNodeResult>();
        foreach (var node in executionOrder.Where(nodeClassifier.IsDirectReplyNode))
        {
            var template = nodeDataReader.ReadNodeInputString(node.Data, "directReplyMessage") ??
                nodeDataReader.ReadNodeInputString(node.Data, "message") ??
                nodeDataReader.ReadNodeInputString(node.Data, "content") ??
                string.Empty;
            var currentIndex = orderIndexByNodeId.TryGetValue(node.Id, out var index) ? index : int.MaxValue;
            var previousRuntimeResults = runtimeModelResults
                .Where(result => !orderIndexByNodeId.TryGetValue(result.NodeId, out var resultIndex) || resultIndex <= currentIndex)
                .OrderBy(result => result.ExecutionIndex)
                .ToList();
            var previousHttpResults = httpResults
                .Where(result => !orderIndexByNodeId.TryGetValue(result.NodeId, out var resultIndex) || resultIndex <= currentIndex)
                .OrderBy(result => result.ExecutionIndex)
                .ToList();
            var previousExecuteFlowResults = executeFlowResults
                .Where(result => !orderIndexByNodeId.TryGetValue(result.NodeId, out var resultIndex) || resultIndex <= currentIndex)
                .OrderBy(result => result.ExecutionIndex)
                .ToList();
            var previousCustomFunctionResults = customFunctionResults
                .Where(result => !orderIndexByNodeId.TryGetValue(result.NodeId, out var resultIndex) || resultIndex <= currentIndex)
                .OrderBy(result => result.ExecutionIndex)
                .ToList();
            var previousLlmResults = llmResults
                .Where(result => !orderIndexByNodeId.TryGetValue(result.NodeId, out var resultIndex) || resultIndex <= currentIndex)
                .OrderBy(result => result.ExecutionIndex)
                .ToList();
            var previousAgentResults = agentResults
                .Where(result => !orderIndexByNodeId.TryGetValue(result.NodeId, out var resultIndex) || resultIndex <= currentIndex)
                .OrderBy(result => result.ExecutionIndex)
                .ToList();
            var content = string.IsNullOrWhiteSpace(template)
                ? string.Empty
                : outputReferenceResolver.ReplaceAgentOutputReferences(
                    outputReferenceResolver.ReplaceLlmOutputReferences(
                        outputReferenceResolver.ReplaceCustomFunctionOutputReferences(
                            outputReferenceResolver.ReplaceExecuteFlowOutputReferences(
                                outputReferenceResolver.ReplaceHttpOutputReferences(ReplaceRuntimeVariables(template, context, null, previousRuntimeResults), previousHttpResults),
                                previousExecuteFlowResults),
                            previousCustomFunctionResults),
                        previousLlmResults),
                    previousAgentResults);
            results.Add(new DirectReplyNodeResult(node.Id, node.DisplayName, template, content));
        }

        return results;
    }

    private static string BuildDirectReplyAnswer(IReadOnlyList<DirectReplyNodeResult> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, results
            .Select(result => result.Content)
            .Where(content => !string.IsNullOrWhiteSpace(content)));
    }

    private static string BuildExecuteFlowAnswer(IReadOnlyList<ExecuteFlowNodeResult> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, results
            .Select(result => result.Content)
            .Where(content => !string.IsNullOrWhiteSpace(content)));
    }

    private static string BuildCustomFunctionAnswer(IReadOnlyList<CustomFunctionNodeResult> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, results
            .Select(result => result.Content)
            .Where(content => !string.IsNullOrWhiteSpace(content)));
    }

    private static string BuildLlmAnswer(IReadOnlyList<LlmNodeResult> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, results
            .Where(result => result.ReturnResponseAs.Equals("userMessage", StringComparison.OrdinalIgnoreCase) || result.ReturnResponseAs.Equals("assistantMessage", StringComparison.OrdinalIgnoreCase))
            .Select(result => result.Content)
            .Where(content => !string.IsNullOrWhiteSpace(content)));
    }

    private static string BuildAgentAnswer(IReadOnlyList<AgentNodeResult> results)
    {
        if (results.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, results
            .Where(result => result.ReturnResponseAs.Equals("userMessage", StringComparison.OrdinalIgnoreCase) || result.ReturnResponseAs.Equals("assistantMessage", StringComparison.OrdinalIgnoreCase))
            .Select(result => result.Content)
            .Where(content => !string.IsNullOrWhiteSpace(content)));
    }

    private IReadOnlyList<string> ReadIterationItems(FlowiseRuntimeNode iterationNode)
    {
        if (!nodeDataReader.TryGetNodeInputValue(iterationNode.Data, "items", out var value) &&
            !nodeDataReader.TryGetNodeInputValue(iterationNode.Data, "iterationInput", out value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .ToList();
        }

        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<IReadOnlyList<JsonElement>>(trimmed, CaseInsensitiveJsonOptions)?
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!.Trim())
                    .ToList() ?? [];
            }
            catch (JsonException)
            {
                throw new ValidationException("Iteration items 必须是 JSON array 或逗号分隔文本", ErrorCodes.ParameterInvalid);
            }
        }

        return trimmed
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private string? ResolveRuntimeNodeInputString(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        var value = nodeDataReader.ReadNodeInputString(data, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return ReplaceRuntimeVariables(value, context, iterationContext, previousResults);
    }

    private int? ResolveRuntimeNodeInputInt(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        var resolvedValue = ResolveRuntimeNodeInputString(data, propertyName, context, iterationContext, previousResults);
        return int.TryParse(resolvedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private string ReplaceRuntimeVariables(
        string value,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults) =>
        variableResolver.ReplaceRuntimeVariables(value, context, iterationContext, previousResults);

    private void ApplyLoopStateUpdate(
        FlowiseRuntimeNode loopNode,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        var loopOutput = BuildLoopOutput(loopNode);
        stateUpdateApplier.ApplyLoopStateUpdate(loopNode, context, previousResults, loopOutput);
    }

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

    private static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IEnumerable<string> ChunkAnswer(string answer)
    {
        const int chunkSize = 80;
        for (var index = 0; index < answer.Length; index += chunkSize)
        {
            yield return answer.Substring(index, Math.Min(chunkSize, answer.Length - index));
        }
    }

    private static FlowiseRuntimeNode? ResolveResumeNode(
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseHumanInputResumeRequest? humanInput)
    {
        if (humanInput is null || string.IsNullOrWhiteSpace(humanInput.NodeId))
        {
            return null;
        }

        return executionOrder.FirstOrDefault(node => string.Equals(node.Id, humanInput.NodeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException("Human Input 恢复节点不存在，流程可能已被修改", ErrorCodes.ParameterInvalid);
    }

    private static IReadOnlyList<FlowiseRuntimeNode> ResolveActiveExecutionOrder(
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseRuntimeNode? resumeNode)
    {
        if (resumeNode is null)
        {
            return executionOrder;
        }

        var resumeIndex = executionOrder.ToList().FindIndex(node => string.Equals(node.Id, resumeNode.Id, StringComparison.OrdinalIgnoreCase));
        return resumeIndex < 0 ? executionOrder : executionOrder.Skip(resumeIndex).ToList();
    }

    private static IReadOnlyList<FlowiseRuntimeNode> TakeUntilNodeInclusive(
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseRuntimeNode stopNode)
    {
        var nodes = new List<FlowiseRuntimeNode>();
        foreach (var node in executionOrder)
        {
            nodes.Add(node);
            if (string.Equals(node.Id, stopNode.Id, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return nodes;
    }

    private static IReadOnlyList<FlowiseAgentExecutedNodeDto> ParseAgentExecutedData(string? executionDataJson)
    {
        if (string.IsNullOrWhiteSpace(executionDataJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<FlowiseAgentExecutedNodeDto>>(executionDataJson, CaseInsensitiveJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsHumanInputReject(FlowiseHumanInputResumeRequest humanInput) =>
        humanInput.Choice.Equals("reject", StringComparison.OrdinalIgnoreCase) ||
        humanInput.Label.Equals("Reject", StringComparison.OrdinalIgnoreCase);

    private async Task EmitNodeFlowProgressAsync(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        IReadOnlyList<RuntimeDataModelNodeResult>? runtimeModelResults,
        IReadOnlyList<DirectReplyNodeResult>? directReplyResults,
        IReadOnlyList<HttpNodeResult>? httpResults,
        IReadOnlyList<ExecuteFlowNodeResult>? executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult>? customFunctionResults,
        IReadOnlyList<LlmNodeResult>? llmResults,
        IReadOnlyList<AgentNodeResult>? agentResults,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions,
        Func<string, object?, CancellationToken, Task> emitAsync,
        CancellationToken cancellationToken)
    {
        foreach (var node in executionOrder)
        {
            await emitAsync("nextAgentFlow", agentFlowEventBuilder.BuildNextAgentFlowEvent(node, "INPROGRESS"), cancellationToken);
            await emitAsync("agentFlowExecutedData", executionSnapshotBuilder.BuildNodeExecutionSnapshot(flowData, node, "INPROGRESS", runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions), cancellationToken);
            await emitAsync("nextAgentFlow", agentFlowEventBuilder.BuildNextAgentFlowEvent(node, "FINISHED"), cancellationToken);
            await emitAsync("agentFlowExecutedData", executionSnapshotBuilder.BuildNodeExecutionSnapshot(flowData, node, "FINISHED", runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions), cancellationToken);
        }
    }

    private async Task<IReadOnlyList<FlowiseAgentExecutedNodeDto>> EmitNodeFlowProgressUntilStopAsync(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder,
        FlowiseRuntimeNode stopNode,
        IReadOnlyList<RuntimeDataModelNodeResult>? runtimeModelResults,
        IReadOnlyList<DirectReplyNodeResult>? directReplyResults,
        IReadOnlyList<HttpNodeResult>? httpResults,
        IReadOnlyList<ExecuteFlowNodeResult>? executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult>? customFunctionResults,
        IReadOnlyList<LlmNodeResult>? llmResults,
        IReadOnlyList<AgentNodeResult>? agentResults,
        IReadOnlyDictionary<string, BranchDecision>? branchDecisions,
        Func<string, object?, CancellationToken, Task> emitAsync,
        CancellationToken cancellationToken)
    {
        var executed = new List<FlowiseAgentExecutedNodeDto>();
        foreach (var node in executionOrder)
        {
            await emitAsync("nextAgentFlow", agentFlowEventBuilder.BuildNextAgentFlowEvent(node, "INPROGRESS"), cancellationToken);
            await emitAsync("agentFlowExecutedData", executionSnapshotBuilder.BuildNodeExecutionSnapshot(flowData, node, "INPROGRESS", runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions), cancellationToken);

            var status = string.Equals(node.Id, stopNode.Id, StringComparison.OrdinalIgnoreCase) ? "STOPPED" : "FINISHED";
            var snapshot = executionSnapshotBuilder.BuildNodeExecutionSnapshot(flowData, node, status, runtimeModelResults, directReplyResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults, branchDecisions);
            await emitAsync("nextAgentFlow", agentFlowEventBuilder.BuildNextAgentFlowEvent(node, status), cancellationToken);
            await emitAsync("agentFlowExecutedData", snapshot, cancellationToken);
            executed.Add(snapshot);

            if (status == "STOPPED")
            {
                break;
            }
        }

        return executed;
    }

    private IReadOnlyList<FlowiseRuntimeNode> ExpandLoopExecutionOrder(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseRuntimeNode> executionOrder)
    {
        if (!executionOrder.Any(nodeClassifier.IsLoopNode))
        {
            return executionOrder;
        }

        var firstIndexByNodeId = executionOrder
            .Select((node, index) => new { node.Id, Index = index })
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Index, StringComparer.OrdinalIgnoreCase);
        var loopCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var expanded = new List<FlowiseRuntimeNode>();
        var index = 0;
        var guard = Math.Max(executionOrder.Count * 100, 100);
        while (index >= 0 && index < executionOrder.Count && expanded.Count < guard)
        {
            var node = executionOrder[index];
            expanded.Add(node);
            if (!nodeClassifier.IsLoopNode(node))
            {
                index += 1;
                continue;
            }

            var loopOutput = BuildLoopOutput(node);
            var loopCount = (loopCounts.TryGetValue(node.Id, out var count) ? count : 0) + 1;
            if (loopCount < loopOutput.MaxLoopCount &&
                !string.IsNullOrWhiteSpace(loopOutput.NodeId) &&
                firstIndexByNodeId.TryGetValue(loopOutput.NodeId, out var targetIndex))
            {
                loopCounts[node.Id] = loopCount;
                index = targetIndex;
                continue;
            }

            index += 1;
        }

        return expanded;
    }

    private IReadOnlyList<FlowiseUsedToolDto> BuildUsedTools(
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults)
    {
        var tools = flowData.Nodes
            .Where(node => nodeClassifier.IsToolNode(node))
            .Select(node => new FlowiseUsedToolDto
            {
                InputJson = JsonSerializer.Serialize(node.Data),
                OutputJson = JsonSerializer.Serialize(new { status = "FINISHED", nodeId = node.Id }),
                Tool = node.DisplayName
            })
            .ToList();
        tools.AddRange(runtimeModelResults.Select(result => new FlowiseUsedToolDto
        {
            Tool = result.NodeLabel,
            InputJson = JsonSerializer.Serialize(new { result.ModelCode, result.Request }),
            OutputJson = JsonSerializer.Serialize(result.Response)
        }));
        return tools;
    }

    private static IReadOnlyList<FlowiseAgentReasoningDto> BuildAgentReasoning(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<FlowiseSourceDocumentDto> sourceDocuments,
        IReadOnlyList<FlowiseUsedToolDto> usedTools)
    {
        var agentNodes = flowData.Nodes
            .Where(node => node.NodeType.Contains("agent", StringComparison.OrdinalIgnoreCase) || node.DisplayName.Contains("agent", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (agentNodes.Count == 0 && !chatflow.Type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (agentNodes.Count == 0)
        {
            agentNodes.AddRange(flowData.Nodes.Take(1));
        }

        return agentNodes.Select(node => new FlowiseAgentReasoningDto
        {
            AgentName = string.IsNullOrWhiteSpace(node.DisplayName) ? chatflow.Name : node.DisplayName,
            ArtifactsJson = "[]",
            Instructions = ReadDataString(node.Data, "instructions") ?? BuildSystemInstructions(chatflow, flowData),
            Messages = [$"Executed {node.DisplayName}."],
            NodeName = node.NodeType,
            SourceDocuments = sourceDocuments,
            StateJson = JsonSerializer.Serialize(new { nodeId = node.Id, trace = "runtime" }),
            UsedTools = usedTools
        }).ToList();
    }

    private void EnsureAgentflowStartNode(FlowiseChatFlowEntity chatflow, FlowiseRuntimeFlowData flowData)
    {
        if (!chatflow.Type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var startNodes = flowData.Nodes.Where(nodeClassifier.IsStartNode).ToList();
        if (startNodes.Count == 0)
        {
            throw new ValidationException("工作流必须包含一个 Start 节点", ErrorCodes.ParameterInvalid);
        }

        if (startNodes.Count > 1)
        {
            throw new ValidationException("工作流只能包含一个 Start 节点", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task<FlowiseExecutionEntity> LoadExecutionAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ValidationException("缺少执行 Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseExecutionEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise 执行记录不存在", ErrorCodes.ParameterInvalid);
    }

    private static string ToFlowType(string resourceType) =>
        resourceType.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase) ? "Agentflow" : "Chatflow";

    private void EnsureRun(string type)
    {
        if (type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsRun, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsRun, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
    }

    private static string NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        try
        {
            using var _ = JsonDocument.Parse(value);
            return value.Trim();
        }
        catch (JsonException)
        {
            throw new ValidationException("JSON 格式不正确", ErrorCodes.ParameterInvalid);
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizePageIndex(int pageIndex) => Math.Max(pageIndex, 1);

    private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

    private static int NormalizeRuntimeDelayMs(int delayMs) => Math.Clamp(delayMs, 0, 30000);

    private static string ResolvePrompt(FlowiseRuntimeFlowData flowData, string? question)
    {
        if (!string.IsNullOrWhiteSpace(question))
        {
            return question.Trim();
        }

        var promptNode = flowData.Nodes.FirstOrDefault(node =>
            node.NodeType.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
            node.DisplayName.Contains("prompt", StringComparison.OrdinalIgnoreCase));
        var template = promptNode is null ? null : ReadDataString(promptNode.Data, "template");
        return string.IsNullOrWhiteSpace(template) ? "Run this Flowise workflow." : template;
    }

    private static string? ResolveModelConfigId(FlowiseRuntimeFlowData flowData)
    {
        var modelNode = flowData.Nodes.FirstOrDefault(node =>
            node.NodeType.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            node.DisplayName.Contains("model", StringComparison.OrdinalIgnoreCase) ||
            node.NodeType.Contains("llm", StringComparison.OrdinalIgnoreCase));
        return modelNode is null ? null : ReadDataString(modelNode.Data, "modelConfigId");
    }

    private static string BuildSystemInstructions(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<RuntimeDataModelNodeResult>? runtimeModelResults = null)
    {
        var nodeNames = string.Join(", ", flowData.Nodes.Select(item => item.DisplayName).Where(item => !string.IsNullOrWhiteSpace(item)).Take(20));
        var instructions = $"You are executing Flowise workflow \"{chatflow.Name}\" of type {chatflow.Type}. Follow the configured graph semantics. Nodes: {nodeNames}.";
        if (runtimeModelResults is null || runtimeModelResults.Count == 0)
        {
            return instructions;
        }

        return instructions + " Runtime model query results are authoritative business data; answer from them and do not invent missing rows.";
    }

    private static IReadOnlyList<ChatMessageContent> BuildChatMessages(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<RuntimeDataModelNodeResult>? runtimeModelResults,
        FlowiseExecutionContext context,
        string prompt)
    {
        var messages = new List<ChatMessageContent>
        {
            new(AuthorRole.System, BuildSystemInstructions(chatflow, flowData, runtimeModelResults))
        };
        foreach (var item in context.ChatHistory.TakeLast(20))
        {
            var role = item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? AuthorRole.Assistant
                : AuthorRole.User;
            if (!string.IsNullOrWhiteSpace(item.Content))
            {
                messages.Add(new ChatMessageContent(role, item.Content.Trim()));
            }
        }

        messages.Add(new ChatMessageContent(AuthorRole.User, prompt));
        return messages;
    }

    private static IReadOnlyList<string> IncomingNodeIds(FlowiseRuntimeFlowData flowData, string nodeId) =>
        flowData.Edges
            .Where(edge => string.Equals(edge.Target, nodeId, StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.Source)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? ReadDataString(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!data.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }

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

    private static int? ReadDataInt(IReadOnlyDictionary<string, JsonElement> data, string propertyName)
    {
        if (!data.TryGetValue(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
        {
            return number;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, object?> ReadFlowStateFromOutputJson(string? outputJson)
    {
        if (string.IsNullOrWhiteSpace(outputJson))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            using var document = JsonDocument.Parse(outputJson);
            if (!document.RootElement.TryGetProperty("FlowState", out var stateElement) ||
                stateElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object?>();
            }

            return JsonSerializer.Deserialize<Dictionary<string, object?>>(stateElement.GetRawText(), CaseInsensitiveJsonOptions)
                ?? new Dictionary<string, object?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>();
        }
    }

}

