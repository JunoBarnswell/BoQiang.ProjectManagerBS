using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Workflows;

namespace AsterERP.Api.Application.Ai.Agent;

public sealed record AiWorkflowDraftAutoImportResult(
    string Status,
    string DraftArtifactId,
    string WorkflowKey,
    string WorkflowName,
    string? WorkflowModelId,
    bool IsUpdate,
    string Summary)
{
    public static AiWorkflowDraftAutoImportResult Imported(
        AiWorkflowDraftArtifactEntity draft,
        WorkflowModelDetailResponse detail,
        bool isUpdate) =>
        new(
            "Imported",
            draft.Id,
            draft.WorkflowKey,
            draft.WorkflowName,
            detail.ModelId,
            isUpdate,
            isUpdate
                ? $"已自动更新正式 Workflow 草稿：{detail.Name}"
                : $"已自动导入正式 Workflow 草稿：{detail.Name}");

    public static AiWorkflowDraftAutoImportResult Skipped(AiWorkflowDraftArtifactEntity draft, string reason) =>
        new("Skipped", draft.Id, draft.WorkflowKey, draft.WorkflowName, draft.ImportedWorkflowModelId, false, reason);

    public static AiWorkflowDraftAutoImportResult Failed(AiWorkflowDraftArtifactEntity draft, string reason) =>
        new("Failed", draft.Id, draft.WorkflowKey, draft.WorkflowName, draft.ImportedWorkflowModelId, false, reason);
}
