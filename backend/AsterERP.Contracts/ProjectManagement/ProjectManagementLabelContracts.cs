namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementLabelResponse(string Id, string? ProjectId, string LabelName, string Color, long VersionNo);
public sealed record ProjectManagementLabelUpsertRequest(string LabelName, string Color = "#64748B", long VersionNo = 0);
public sealed record ProjectManagementTaskLabelSetRequest(IReadOnlyList<string> LabelIds, long VersionNo);
public sealed record ProjectManagementTaskLabelResponse(string Id, string TaskId, string LabelId, string LabelName, string Color);
