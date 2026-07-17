using System.Diagnostics;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.SemanticKernel;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    AiKernelFunctionCatalog catalog,
    AiKernelFunctionArgumentNormalizer schemaValidator,
    AiKernelFunctionPermissionFilter permissionService,
    AiKernelFunctionArgumentRedactor argumentRedactor,
    ILoggerFactory loggerFactory,
    IEnumerable<IFunctionInvocationFilter> functionInvocationFilters,
    IEnumerable<IAutoFunctionInvocationFilter> autoFunctionInvocationFilters,
    IEnumerable<IPromptRenderFilter> promptRenderFilters)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<AiKernelFunctionDefinitionDto> ListDefinitions() =>
        catalog.ListDefinitions().Select(item => item.ToDto()).ToList();

    public AiKernelFunctionDefinitionDto GetDefinition(string toolCode) => catalog.Require(toolCode).Definition.ToDto();

    public async Task<AiToolDryRunResponse> DryRunAsync(
        string toolCode,
        AiToolInvokeRequest request,
        CancellationToken cancellationToken)
    {
        var tool = catalog.Require(toolCode);
        permissionService.EnsureAllowed(tool.Definition);
        EnsureHighRiskConfirmationAccepted(tool.Definition, request);
        var (context, issues) = BuildDirectContext(tool.Definition, request);
        var response = await tool.DryRunAsync(context, issues, cancellationToken);
        response.NormalizedArgumentsJson = argumentRedactor.RedactJson(tool.Definition, response.NormalizedArgumentsJson);
        return response;
    }

    public async Task<AiToolInvokeResponse> InvokeAsync(
        string toolCode,
        AiToolInvokeRequest request,
        CancellationToken cancellationToken)
    {
        var tool = catalog.Require(toolCode);
        permissionService.EnsureAllowed(tool.Definition);
        EnsureHighRiskConfirmationAccepted(tool.Definition, request);
        var (context, issues) = BuildDirectContext(tool.Definition, request);
        EnsureNoSchemaIssues(issues);
        var (result, log) = await ExecuteWithLogAsync(tool, context, cancellationToken);
        await PersistToolResultMessageAsync(context, result, cancellationToken);
        return new AiToolInvokeResponse
        {
            Invocation = AiKernelFunctionMapper.MapInvocation(log),
            ResultSummary = result.ResultSummary,
            Content = result.Content,
            EvidenceJson = result.EvidenceJson,
            OutputType = result.OutputType
        };
    }

    public async Task<AiTaskAgentRunOutput> ExecutePlanItemAsync(
        AiTaskPlanEntity plan,
        AiTaskPlanItemEntity item,
        string runId,
        string? modelConfigId,
        string? userInstruction,
        AiKernelFunctionSelection toolSelection,
        CancellationToken cancellationToken)
    {
        var toolCode = string.IsNullOrWhiteSpace(item.ToolCode)
            ? throw new ValidationException("任务缺少工具编码", ErrorCodes.AiTaskToolNotAllowed)
            : item.ToolCode.Trim();
        var tool = catalog.Require(toolCode, toolSelection);
        permissionService.EnsureAllowed(tool.Definition);
        var request = BuildPlanToolRequest(plan, item, runId, userInstruction);
        var (arguments, argumentsJson, issues) = schemaValidator.Normalize(tool.Definition, request);
        EnsureNoSchemaIssues(issues);
        var context = new AiKernelFunctionContext
        {
            TenantId = plan.TenantId,
            AppCode = plan.AppCode,
            OwnerUserId = plan.OwnerUserId,
            ConversationId = plan.ConversationId,
            RunId = runId,
            PlanId = plan.Id,
            PlanItemId = item.Id,
            WorkMode = "Agent",
            TraceId = Guid.NewGuid().ToString("N"),
            ModelConfigId = modelConfigId,
            UserInstruction = userInstruction,
            Arguments = arguments,
            ArgumentsJson = argumentsJson
        };
        var (result, _) = await ExecuteWithLogAsync(tool, context, cancellationToken);
        return new AiTaskAgentRunOutput
        {
            ResultSummary = result.ResultSummary,
            Content = result.Content,
            EvidenceJson = result.EvidenceJson,
            OutputType = result.OutputType,
            Events = result.Events
        };
    }

    public async Task<IReadOnlyList<AiToolInvocationDto>> GetRunInvocationsAsync(
        string runId,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var items = await db.Queryable<AiToolExecutionLogEntity>()
            .Where(item => !item.IsDeleted &&
                           item.TenantId == workspace.TenantId &&
                           item.AppCode == workspace.AppCode &&
                           item.OwnerUserId == workspace.UserId &&
                           item.RunId == runId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(100)
            .ToListAsync(cancellationToken);
        return items.Select(AiKernelFunctionMapper.MapInvocation).ToList();
    }

    private (AiKernelFunctionContext Context, IReadOnlyList<string> Issues) BuildDirectContext(
        AiKernelFunctionDefinition definition,
        AiToolInvokeRequest request)
    {
        var workspace = workspaceContext.Resolve();
        var (arguments, argumentsJson, issues) = schemaValidator.Normalize(definition, request);
        var context = new AiKernelFunctionContext
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ConversationId = NormalizeOptional(request.ConversationId),
            RunId = NormalizeOptional(request.RunId),
            PlanId = NormalizeOptional(request.PlanId),
            PlanItemId = NormalizeOptional(request.PlanItemId),
            WorkMode = string.IsNullOrWhiteSpace(request.WorkMode) ? "Ask" : request.WorkMode.Trim(),
            TraceId = Guid.NewGuid().ToString("N"),
            ModelConfigId = NormalizeOptional(request.ModelConfigId),
            Arguments = arguments,
            ArgumentsJson = argumentsJson
        };
        return (context, issues);
    }

    private async Task<(AiKernelFunctionResult Result, AiToolExecutionLogEntity Log)> ExecuteWithLogAsync(
        IAiKernelFunction functionHandler,
        AiKernelFunctionContext context,
        CancellationToken cancellationToken)
    {
        var definition = functionHandler.Definition;
        var stopwatch = Stopwatch.StartNew();
        var log = new AiToolExecutionLogEntity
        {
            TenantId = context.TenantId,
            AppCode = context.AppCode,
            OwnerUserId = context.OwnerUserId,
            ConversationId = context.ConversationId,
            RunId = context.RunId,
            ModelConfigId = context.ModelConfigId,
            PlanId = context.PlanId,
            ItemId = context.PlanItemId,
            ToolName = definition.ToolName,
            ToolCode = definition.ToolCode,
            TraceId = context.TraceId,
            ArgumentsJson = argumentRedactor.RedactJson(definition, context.ArgumentsJson),
            RequiresConfirmation = definition.RequiresConfirmation,
            Status = "Running"
        };
        await db.Insertable(log).ExecuteCommandAsync(cancellationToken);

        try
        {
            var result = await InvokeKernelFunctionAsync(functionHandler, context, cancellationToken);
            stopwatch.Stop();
            log.Status = "Succeeded";
            log.ResultSummary = Truncate(result.ResultSummary, 500);
            log.DurationMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
            log.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(log).ExecuteCommandAsync(cancellationToken);
            return (result, log);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            log.Status = "Failed";
            log.ErrorMessage = Truncate(ex.Message, 1000);
            log.DurationMs = (int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds);
            log.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(log).ExecuteCommandAsync(cancellationToken);
            throw;
        }
    }

    private async Task<AiKernelFunctionResult> InvokeKernelFunctionAsync(
        IAiKernelFunction functionHandler,
        AiKernelFunctionContext context,
        CancellationToken cancellationToken)
    {
        var definition = functionHandler.Definition;
        var kernelName = definition.KernelName;
        var kernel = Kernel.CreateBuilder().Build();
        RegisterFilters(kernel);
        var function = KernelFunctionFactory.CreateFromMethod(
            (Func<CancellationToken, Task<AiKernelFunctionResult>>)(token => functionHandler.ExecuteAsync(context, token)),
            kernelName.FunctionName,
            definition.Description,
            Array.Empty<KernelParameterMetadata>(),
            null,
            loggerFactory);
        kernel.Plugins.AddFromFunctions(kernelName.PluginName, definition.Description, [function]);
        var result = await kernel.InvokeAsync(kernelName.PluginName, kernelName.FunctionName, new KernelArguments(), cancellationToken);
        return result.GetValue<AiKernelFunctionResult>()
               ?? throw new BusinessException(ErrorCodes.AiTaskExecutionFailed, $"SK 函数 {kernelName.PluginName}/{kernelName.FunctionName} 未返回结果");
    }

    private async Task PersistToolResultMessageAsync(
        AiKernelFunctionContext context,
        AiKernelFunctionResult result,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.ConversationId))
        {
            return;
        }

        var nextSeq = await NextSeqAsync(context.ConversationId, cancellationToken);
        var metadata = new
        {
            type = "toolResult",
            context.TraceId,
            context.ModelConfigId,
            result.OutputType,
            result.EvidenceJson,
            content = result.Content
        };
        var message = new AiMessageEntity
        {
            TenantId = context.TenantId,
            AppCode = context.AppCode,
            OwnerUserId = context.OwnerUserId,
            ConversationId = context.ConversationId,
            RunId = context.RunId,
            Role = "assistant",
            Seq = nextSeq,
            Content = string.IsNullOrWhiteSpace(result.ResultSummary) ? "工具执行完成。" : result.ResultSummary,
            MetadataJson = JsonSerializer.Serialize(metadata, JsonOptions),
            TokenCount = EstimateTokens(result.ResultSummary),
            FinishReason = "tool_result",
            Status = "Completed"
        };
        await db.Insertable(message).ExecuteCommandAsync(cancellationToken);

        var conversation = await db.Queryable<AiConversationEntity>()
            .FirstAsync(item => item.Id == context.ConversationId && !item.IsDeleted, cancellationToken);
        if (conversation is not null)
        {
            conversation.LastMessageAt = DateTime.UtcNow;
            conversation.LastRunStatus = "ToolSucceeded";
            conversation.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task<int> NextSeqAsync(string conversationId, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversationId)
            .Select(item => item.Seq)
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? 1 : rows.Max() + 1;
    }

    private void RegisterFilters(Kernel kernel)
    {
        foreach (var filter in functionInvocationFilters)
        {
            kernel.FunctionInvocationFilters.Add(filter);
        }

        foreach (var filter in autoFunctionInvocationFilters)
        {
            kernel.AutoFunctionInvocationFilters.Add(filter);
        }

        foreach (var filter in promptRenderFilters)
        {
            kernel.PromptRenderFilters.Add(filter);
        }
    }

    private AiToolInvokeRequest BuildPlanToolRequest(
        AiTaskPlanEntity plan,
        AiTaskPlanItemEntity item,
        string runId,
        string? userInstruction)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["conversationId"] = plan.ConversationId,
            ["runId"] = runId,
            ["planId"] = plan.Id,
            ["planItemId"] = item.Id,
            ["requirementText"] = FirstNonEmpty(item.ExecutionHint, item.Description, plan.Goal, userInstruction),
            ["businessType"] = InferBusinessType(FirstNonEmpty(item.ExecutionHint, item.Description, plan.Goal, userInstruction))
        };

        if (!string.IsNullOrWhiteSpace(item.ExecutionHint) && LooksLikeJson(item.ExecutionHint))
        {
            foreach (var (key, value) in schemaValidator.DeserializeObject(item.ExecutionHint))
            {
                arguments[key] = value;
            }
        }

        return new AiToolInvokeRequest
        {
            ConversationId = plan.ConversationId,
            RunId = runId,
            PlanId = plan.Id,
            PlanItemId = item.Id,
            WorkMode = "Agent",
            Arguments = arguments,
            ArgumentsJson = JsonSerializer.Serialize(arguments, JsonOptions)
        };
    }

    private static void EnsureNoSchemaIssues(IReadOnlyList<string> issues)
    {
        if (issues.Count > 0)
        {
            throw new ValidationException(string.Join("; ", issues), ErrorCodes.ParameterInvalid);
        }
    }

    private void EnsureHighRiskConfirmationAccepted(AiKernelFunctionDefinition definition, AiToolInvokeRequest request)
    {
        if (!definition.RequiresConfirmation)
        {
            return;
        }

        if (!request.ConfirmedRiskAccepted)
        {
            throw new ValidationException("高风险工具需要人工确认后才能执行", ErrorCodes.AiWorkflowPermissionDenied);
        }

        permissionService.EnsureHighRiskConfirmationAllowed();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith('{') && value.TrimEnd().EndsWith('}');
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))?.Trim();

    private static int EstimateTokens(string? content) => string.IsNullOrEmpty(content) ? 0 : Math.Max(1, content.Length / 4);

    private static string InferBusinessType(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "workflow.ai";
        }

        return text.Contains("采购", StringComparison.OrdinalIgnoreCase)
            ? "purchase.order"
            : "workflow.ai";
    }

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= maxLength ? value : value[..maxLength];
}
