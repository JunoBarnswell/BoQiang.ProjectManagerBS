namespace AsterERP.Contracts.ProjectManagement;

public static class ProjectManagementLabelScopes
{
    public const string Public = "Public";
    public const string Project = "Project";
}

public static class ProjectManagementTaskLabelMatchModes
{
    public const string Any = "Any";
    public const string All = "All";
}

public sealed record ProjectManagementLabelResponse(string Id, string? ProjectId, string Scope, string LabelName, string Color, long VersionNo);
public sealed record ProjectManagementLabelUpsertRequest(string LabelName, string Color = "#64748B", long VersionNo = 0);
public sealed record ProjectManagementTaskLabelSetRequest(IReadOnlyList<string> LabelIds, long VersionNo);
public sealed record ProjectManagementTaskLabelResponse(string Id, string TaskId, string LabelId, string LabelName, string Color);

/// <summary>
/// 跨列表、看板、甘特和导出复用的标签筛选协议；任务查询实现由调用方在其查询边界中消费。
/// </summary>
public sealed record ProjectManagementTaskLabelFilter(IReadOnlyList<string> LabelIds, string MatchMode = ProjectManagementTaskLabelMatchModes.Any);
