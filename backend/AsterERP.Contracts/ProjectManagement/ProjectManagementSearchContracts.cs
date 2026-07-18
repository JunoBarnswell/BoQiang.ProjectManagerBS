namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementSearchQuery(string Keyword, string Scope = "all", int Limit = 20);

public sealed record ProjectManagementSearchItem(string ResultType, string Id, string ProjectId, string Title, string? Summary, string TargetRoute, DateTime? UpdatedTime);

public sealed record ProjectManagementSearchResponse(IReadOnlyList<ProjectManagementSearchItem> Projects, IReadOnlyList<ProjectManagementSearchItem> Tasks, IReadOnlyList<ProjectManagementSearchItem> Comments);
