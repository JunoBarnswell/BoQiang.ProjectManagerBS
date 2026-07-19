using AsterERP.Contracts.Workflows;

namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementApprovalStateResponse(
    string EntityType,
    string EntityId,
    string BusinessKey,
    string ProcessInstanceId,
    string WorkflowStatus,
    string ApprovalStatus,
    long CurrentVersionNo,
    long StartedVersionNo,
    bool CanWithdraw,
    string? DetailRoute,
    IReadOnlyList<WorkflowTimelineItemResponse> History);
