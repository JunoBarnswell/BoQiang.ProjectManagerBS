using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Modules.Ai;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Agent;

public sealed class AiWorkflowDraftAutoImportService(
    ISqlSugarClient db,
    IWorkflowModelAppService workflowModelService)
{
    public async Task<AiWorkflowDraftAutoImportResult?> TryImportLatestAsync(
        AiTaskPlanEntity plan,
        CancellationToken cancellationToken)
    {
        var draft = await db.Queryable<AiWorkflowDraftArtifactEntity>()
            .Where(item => !item.IsDeleted &&
                           item.TenantId == plan.TenantId &&
                           item.AppCode == plan.AppCode &&
                           item.OwnerUserId == plan.OwnerUserId &&
                           item.ConversationId == plan.ConversationId &&
                           item.PlanId == plan.Id)
            .OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        if (draft is null)
        {
            return null;
        }

        var validation = await db.Queryable<AiWorkflowValidationReportEntity>()
            .Where(item => !item.IsDeleted && item.DraftArtifactId == draft.Id)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        var items = await db.Queryable<AiTaskPlanItemEntity>()
            .Where(item => !item.IsDeleted && item.PlanId == plan.Id)
            .ToListAsync(cancellationToken);
        var blockedReason = GetBlockedReason(draft, validation, items);
        if (!string.IsNullOrWhiteSpace(blockedReason))
        {
            return AiWorkflowDraftAutoImportResult.Skipped(draft, blockedReason);
        }

        var isUpdate = !string.IsNullOrWhiteSpace(draft.ImportedWorkflowModelId) ||
                       await HasExistingWorkflowModelAsync(draft, cancellationToken);
        try
        {
            var detail = await workflowModelService.ImportAiDraftAsync(draft.Id, cancellationToken);
            return AiWorkflowDraftAutoImportResult.Imported(draft, detail, isUpdate);
        }
        catch (Exception ex)
        {
            return AiWorkflowDraftAutoImportResult.Failed(draft, ex.Message);
        }
    }

    private async Task<bool> HasExistingWorkflowModelAsync(
        AiWorkflowDraftArtifactEntity draft,
        CancellationToken cancellationToken)
    {
        return await db.Queryable<ModelInfo>()
            .AnyAsync(item => item.DelFlag == 1 &&
                              item.AppSn == draft.AppCode &&
                              item.ModelKey == draft.WorkflowKey,
                cancellationToken);
    }

    public static string? GetBlockedReason(
        AiWorkflowDraftArtifactEntity draft,
        AiWorkflowValidationReportEntity? validation,
        IReadOnlyCollection<AiTaskPlanItemEntity> items)
    {
        if (string.IsNullOrWhiteSpace(draft.BpmnXml))
        {
            return "AI Workflow 草稿缺少 BPMN XML，未自动导入正式草稿";
        }

        if (string.IsNullOrWhiteSpace(draft.BusinessCanvasJson))
        {
            return "AI Workflow 草稿缺少业务画布 JSON，未自动导入正式草稿";
        }

        if (validation is null)
        {
            return "AI Workflow 草稿缺少校验报告，未自动导入正式草稿";
        }

        if (validation.ErrorCount > 0 || !validation.IsValid)
        {
            return $"AI Workflow 草稿校验存在 {validation.ErrorCount} 个错误，未自动导入正式草稿";
        }

        var failedWorkflowTool = items.FirstOrDefault(item =>
            item.OwnerType == AiTaskPlanConstants.OwnerType.Tool &&
            item.ToolCode?.StartsWith("workflow.", StringComparison.OrdinalIgnoreCase) == true &&
            item.Status == AiTaskPlanConstants.ItemStatus.Failed);
        return failedWorkflowTool is null
            ? null
            : $"Workflow 工具任务失败：{failedWorkflowTool.Title}";
    }
}
