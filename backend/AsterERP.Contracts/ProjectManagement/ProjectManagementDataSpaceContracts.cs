namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementDataSpaceSummaryResponse(
    string TenantId,
    string AppCode,
    string DatabaseStatus,
    int ProjectCount,
    int TaskCount,
    int MemberCount,
    int MilestoneCount,
    int AttachmentCount,
    DateTime? LastActivityTime);
