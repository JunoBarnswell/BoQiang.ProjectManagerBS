using System.Text.Json;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class AiWorkflowArtifactService(ISqlSugarClient db, AiWorkspaceContext workspaceContext)
{
    public async Task<AiWorkflowDraftArtifactEntity> CreateDraftAsync(
        AiKernelFunctionContext context,
        AiWorkflowDraftDto draft,
        string? bpmnXml,
        string? businessCanvasJson,
        CancellationToken cancellationToken)
    {
        var entity = new AiWorkflowDraftArtifactEntity
        {
            TenantId = context.TenantId,
            AppCode = context.AppCode,
            OwnerUserId = context.OwnerUserId,
            ConversationId = context.ConversationId ?? string.Empty,
            RunId = context.RunId,
            PlanId = context.PlanId,
            PlanItemId = context.PlanItemId,
            TraceId = context.TraceId,
            WorkflowKey = draft.WorkflowKey,
            WorkflowName = draft.WorkflowName,
            BusinessType = draft.BusinessType,
            DraftDslJson = JsonSerializer.Serialize(draft, WorkflowJsonOptions.Options),
            BpmnXml = bpmnXml,
            BusinessCanvasJson = businessCanvasJson,
            CreatedBy = context.OwnerUserId
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    public async Task<AiWorkflowDraftArtifactEntity> UpdateDraftAsync(
        AiWorkflowDraftArtifactEntity entity,
        Action<AiWorkflowDraftArtifactEntity> apply,
        CancellationToken cancellationToken)
    {
        apply(entity);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    public async Task<AiWorkflowDraftArtifactEntity> RequireDraftAsync(
        string draftArtifactId,
        AiKernelFunctionContext context,
        CancellationToken cancellationToken)
    {
        var draft = await db.Queryable<AiWorkflowDraftArtifactEntity>()
            .FirstAsync(item => !item.IsDeleted &&
                                item.Id == draftArtifactId &&
                                item.TenantId == context.TenantId &&
                                item.AppCode == context.AppCode &&
                                item.OwnerUserId == context.OwnerUserId,
                cancellationToken);
        return draft ?? throw new NotFoundException("AI Workflow 草稿不存在", ErrorCodes.AiWorkflowModelNotFound);
    }

    public async Task<AiWorkflowDraftArtifactEntity> RequireDraftFromArgumentsAsync(
        AiKernelFunctionContext context,
        CancellationToken cancellationToken)
    {
        var draftArtifactId = AiWorkflowArgumentReader.ReadString(context.Arguments, "draftArtifactId");
        if (!string.IsNullOrWhiteSpace(draftArtifactId))
        {
            return await RequireDraftAsync(draftArtifactId, context, cancellationToken);
        }

        var query = db.Queryable<AiWorkflowDraftArtifactEntity>()
            .Where(item => !item.IsDeleted &&
                           item.TenantId == context.TenantId &&
                           item.AppCode == context.AppCode &&
                           item.OwnerUserId == context.OwnerUserId)
            .WhereIF(!string.IsNullOrWhiteSpace(context.PlanId), item => item.PlanId == context.PlanId)
            .WhereIF(string.IsNullOrWhiteSpace(context.PlanId) && !string.IsNullOrWhiteSpace(context.ConversationId), item => item.ConversationId == context.ConversationId)
            .OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc);
        var draft = await query.FirstAsync(cancellationToken);
        return draft ?? throw new NotFoundException("未找到可继续处理的 AI Workflow 草稿", ErrorCodes.AiWorkflowModelNotFound);
    }

    public AiWorkflowDraftDto ParseDraft(AiWorkflowDraftArtifactEntity entity)
    {
        try
        {
            return JsonSerializer.Deserialize<AiWorkflowDraftDto>(entity.DraftDslJson, WorkflowJsonOptions.Options)
                   ?? throw new JsonException("empty draft");
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"AI Workflow 草稿解析失败：{ex.Message}", ErrorCodes.AiWorkflowDraftParseFailed);
        }
    }

    public async Task<AiWorkflowValidationReportEntity> SaveValidationReportAsync(
        AiKernelFunctionContext context,
        AiWorkflowDraftArtifactEntity draft,
        IReadOnlyList<AiWorkflowValidationIssueDto> issues,
        CancellationToken cancellationToken)
    {
        var report = new AiWorkflowValidationReportEntity
        {
            TenantId = context.TenantId,
            AppCode = context.AppCode,
            OwnerUserId = context.OwnerUserId,
            ConversationId = draft.ConversationId,
            RunId = context.RunId,
            PlanId = context.PlanId,
            PlanItemId = context.PlanItemId,
            DraftArtifactId = draft.Id,
            TraceId = context.TraceId,
            IsValid = issues.All(item => !item.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)),
            ErrorCount = issues.Count(item => item.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase)),
            WarningCount = issues.Count(item => item.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase)),
            IssuesJson = JsonSerializer.Serialize(issues, WorkflowJsonOptions.Options),
            CreatedBy = context.OwnerUserId
        };
        await db.Insertable(report).ExecuteCommandAsync(cancellationToken);
        return report;
    }

    public async Task<AiWorkflowSimulationReportEntity> SaveSimulationReportAsync(
        AiKernelFunctionContext context,
        AiWorkflowDraftArtifactEntity draft,
        IReadOnlyDictionary<string, object?> variables,
        IReadOnlyList<AiWorkflowSimulationStepDto> steps,
        CancellationToken cancellationToken)
    {
        var report = new AiWorkflowSimulationReportEntity
        {
            TenantId = context.TenantId,
            AppCode = context.AppCode,
            OwnerUserId = context.OwnerUserId,
            ConversationId = draft.ConversationId,
            RunId = context.RunId,
            PlanId = context.PlanId,
            PlanItemId = context.PlanItemId,
            DraftArtifactId = draft.Id,
            TraceId = context.TraceId,
            Succeeded = steps.Count > 0 && steps[^1].Action == "complete",
            VariablesJson = JsonSerializer.Serialize(variables, WorkflowJsonOptions.Options),
            StepsJson = JsonSerializer.Serialize(steps, WorkflowJsonOptions.Options),
            CreatedBy = context.OwnerUserId
        };
        await db.Insertable(report).ExecuteCommandAsync(cancellationToken);
        return report;
    }

    public async Task<AiWorkflowDiagnosisReportEntity> SaveDiagnosisReportAsync(
        AiKernelFunctionContext context,
        string diagnosisType,
        string targetId,
        string summary,
        IReadOnlyList<string> evidence,
        IReadOnlyList<string> suggestions,
        CancellationToken cancellationToken)
    {
        var report = new AiWorkflowDiagnosisReportEntity
        {
            TenantId = context.TenantId,
            AppCode = context.AppCode,
            OwnerUserId = context.OwnerUserId,
            ConversationId = context.ConversationId ?? string.Empty,
            RunId = context.RunId,
            PlanId = context.PlanId,
            PlanItemId = context.PlanItemId,
            TraceId = context.TraceId,
            DiagnosisType = diagnosisType,
            TargetId = targetId,
            Summary = summary,
            EvidenceJson = JsonSerializer.Serialize(evidence, WorkflowJsonOptions.Options),
            SuggestionsJson = JsonSerializer.Serialize(suggestions, WorkflowJsonOptions.Options),
            CreatedBy = context.OwnerUserId
        };
        await db.Insertable(report).ExecuteCommandAsync(cancellationToken);
        return report;
    }

    public async Task<AiWorkflowOverviewDto> GetOverviewAsync(string conversationId, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var drafts = await db.Queryable<AiWorkflowDraftArtifactEntity>()
            .Where(item => !item.IsDeleted &&
                           item.TenantId == workspace.TenantId &&
                           item.AppCode == workspace.AppCode &&
                           item.OwnerUserId == workspace.UserId &&
                           item.ConversationId == conversationId)
            .OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .Take(20)
            .ToListAsync(cancellationToken);
        var draftIds = drafts.Select(item => item.Id).ToList();
        var validations = draftIds.Count == 0
            ? []
            : await db.Queryable<AiWorkflowValidationReportEntity>()
                .Where(item => !item.IsDeleted && draftIds.Contains(item.DraftArtifactId))
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .Take(50)
                .ToListAsync(cancellationToken);
        var simulations = draftIds.Count == 0
            ? []
            : await db.Queryable<AiWorkflowSimulationReportEntity>()
                .Where(item => !item.IsDeleted && draftIds.Contains(item.DraftArtifactId))
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .Take(50)
                .ToListAsync(cancellationToken);
        var diagnoses = await db.Queryable<AiWorkflowDiagnosisReportEntity>()
            .Where(item => !item.IsDeleted &&
                           item.TenantId == workspace.TenantId &&
                           item.AppCode == workspace.AppCode &&
                           item.OwnerUserId == workspace.UserId &&
                           item.ConversationId == conversationId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(50)
            .ToListAsync(cancellationToken);
        var logs = await db.Queryable<AiToolExecutionLogEntity>()
            .Where(item => !item.IsDeleted &&
                           item.TenantId == workspace.TenantId &&
                           item.AppCode == workspace.AppCode &&
                           item.OwnerUserId == workspace.UserId &&
                           item.ConversationId == conversationId &&
                           item.ToolCode != null &&
                           item.ToolCode.StartsWith("workflow."))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return new AiWorkflowOverviewDto
        {
            DraftArtifacts = drafts.Select(MapDraft).ToList(),
            ValidationReports = validations.Select(MapValidation).ToList(),
            SimulationReports = simulations.Select(MapSimulation).ToList(),
            DiagnosisReports = diagnoses.Select(MapDiagnosis).ToList(),
            ToolInvocations = logs.Select(AiKernelFunctionMapper.MapInvocation).ToList()
        };
    }

    public async Task<AiWorkflowDraftArtifactDto> GetDraftAsync(string id, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var entity = await db.Queryable<AiWorkflowDraftArtifactEntity>()
            .FirstAsync(item => !item.IsDeleted &&
                                item.Id == id &&
                                item.TenantId == workspace.TenantId &&
                                item.AppCode == workspace.AppCode &&
                                item.OwnerUserId == workspace.UserId,
                cancellationToken)
            ?? throw new NotFoundException("AI Workflow 草稿不存在", ErrorCodes.AiWorkflowModelNotFound);
        return MapDraft(entity);
    }

    public async Task<AiWorkflowValidationReportDto> GetValidationReportAsync(string id, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var entity = await db.Queryable<AiWorkflowValidationReportEntity>()
            .FirstAsync(item => !item.IsDeleted &&
                                item.Id == id &&
                                item.TenantId == workspace.TenantId &&
                                item.AppCode == workspace.AppCode &&
                                item.OwnerUserId == workspace.UserId,
                cancellationToken)
            ?? throw new NotFoundException("AI Workflow 校验报告不存在", ErrorCodes.AiWorkflowModelNotFound);
        return MapValidation(entity);
    }

    public async Task<AiWorkflowSimulationReportDto> GetSimulationReportAsync(string id, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var entity = await db.Queryable<AiWorkflowSimulationReportEntity>()
            .FirstAsync(item => !item.IsDeleted &&
                                item.Id == id &&
                                item.TenantId == workspace.TenantId &&
                                item.AppCode == workspace.AppCode &&
                                item.OwnerUserId == workspace.UserId,
                cancellationToken)
            ?? throw new NotFoundException("AI Workflow 模拟报告不存在", ErrorCodes.AiWorkflowModelNotFound);
        return MapSimulation(entity);
    }

    public async Task<AiWorkflowDiagnosisReportDto> GetDiagnosisReportAsync(string id, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        var entity = await db.Queryable<AiWorkflowDiagnosisReportEntity>()
            .FirstAsync(item => !item.IsDeleted &&
                                item.Id == id &&
                                item.TenantId == workspace.TenantId &&
                                item.AppCode == workspace.AppCode &&
                                item.OwnerUserId == workspace.UserId,
                cancellationToken)
            ?? throw new NotFoundException("AI Workflow 诊断报告不存在", ErrorCodes.AiWorkflowModelNotFound);
        return MapDiagnosis(entity);
    }

    public static AiWorkflowDraftArtifactDto MapDraft(AiWorkflowDraftArtifactEntity entity) => new()
    {
        Id = entity.Id,
        ConversationId = entity.ConversationId,
        RunId = entity.RunId,
        PlanId = entity.PlanId,
        PlanItemId = entity.PlanItemId,
        TraceId = entity.TraceId,
        WorkflowKey = entity.WorkflowKey,
        WorkflowName = entity.WorkflowName,
        BusinessType = entity.BusinessType,
        Status = entity.Status,
        DraftDslJson = entity.DraftDslJson,
        BpmnXml = entity.BpmnXml,
        BusinessCanvasJson = entity.BusinessCanvasJson,
        BindingProposalJson = entity.BindingProposalJson,
        FormPermissionProposalJson = entity.FormPermissionProposalJson,
        ActionMappingProposalJson = entity.ActionMappingProposalJson,
        NotificationPreviewJson = entity.NotificationPreviewJson,
        ImportedWorkflowModelId = entity.ImportedWorkflowModelId,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    public static AiWorkflowValidationReportDto MapValidation(AiWorkflowValidationReportEntity entity) => new()
    {
        Id = entity.Id,
        DraftArtifactId = entity.DraftArtifactId,
        IsValid = entity.IsValid,
        ErrorCount = entity.ErrorCount,
        WarningCount = entity.WarningCount,
        Issues = DeserializeList<AiWorkflowValidationIssueDto>(entity.IssuesJson),
        TraceId = entity.TraceId,
        CreatedTime = entity.CreatedTime
    };

    public static AiWorkflowSimulationReportDto MapSimulation(AiWorkflowSimulationReportEntity entity) => new()
    {
        Id = entity.Id,
        DraftArtifactId = entity.DraftArtifactId,
        Succeeded = entity.Succeeded,
        Variables = DeserializeDictionary(entity.VariablesJson),
        Steps = DeserializeList<AiWorkflowSimulationStepDto>(entity.StepsJson),
        TraceId = entity.TraceId,
        CreatedTime = entity.CreatedTime
    };

    public static AiWorkflowDiagnosisReportDto MapDiagnosis(AiWorkflowDiagnosisReportEntity entity) => new()
    {
        Id = entity.Id,
        DiagnosisType = entity.DiagnosisType,
        TargetId = entity.TargetId,
        Summary = entity.Summary,
        Evidence = DeserializeList<string>(entity.EvidenceJson),
        Suggestions = DeserializeList<string>(entity.SuggestionsJson),
        TraceId = entity.TraceId,
        CreatedTime = entity.CreatedTime
    };

    private static IReadOnlyList<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, WorkflowJsonOptions.Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Dictionary<string, object?> DeserializeDictionary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, WorkflowJsonOptions.Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
