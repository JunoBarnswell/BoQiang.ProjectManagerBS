using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public static class FlowiseMapper
{
    public static FlowiseWorkspaceDto MapWorkspace(FlowiseWorkspaceEntity entity) => new()
    {
        Id = entity.Id,
        WorkspaceKey = entity.WorkspaceKey,
        WorkspaceName = entity.WorkspaceName,
        Status = entity.Status,
        Description = entity.Description,
        CreatedTime = entity.CreatedTime
    };

    public static FlowiseExecutionDto MapExecution(FlowiseExecutionEntity entity) => new()
    {
        Id = entity.Id,
        ResourceId = entity.ResourceId,
        ResourceName = entity.ResourceName,
        FlowType = entity.FlowType,
        Status = entity.Status,
        InputJson = entity.InputJson,
        OutputJson = entity.OutputJson,
        SourceDocumentsJson = entity.SourceDocumentsJson,
        ActionJson = entity.ActionJson,
        ErrorCode = entity.ErrorCode,
        ErrorMessage = entity.ErrorMessage,
        TraceId = entity.TraceId,
        DurationMs = entity.DurationMs,
        StartedAt = entity.StartedAt,
        CompletedAt = entity.CompletedAt,
        CreatedTime = entity.CreatedTime
    };
}
