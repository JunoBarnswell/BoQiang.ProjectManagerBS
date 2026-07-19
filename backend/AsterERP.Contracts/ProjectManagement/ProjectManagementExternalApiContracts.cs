namespace AsterERP.Contracts.ProjectManagement;

/// <summary>项目管理对外 HTTP API 的稳定版本与请求头契约。</summary>
public static class ProjectManagementExternalApiContract
{
    public const string ApiVersion = "v1";
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string VersionHeader = "If-Match";
    public const string SourceHeader = "X-Integration-Source";
}

/// <summary>对外任务分页查询。项目范围由 URL 路径提供，不接受第二个 ProjectId。</summary>
public sealed record ProjectManagementExternalTaskQuery(
    int PageIndex = 1,
    int PageSize = 50,
    string? Keyword = null,
    string? Status = null,
    string? AssigneeUserId = null,
    string ViewKey = "tree",
    string? GroupBy = null,
    string SortBy = "tree",
    string SortDirection = "asc",
    string? MilestoneId = null,
    string? ParentTaskId = null,
    DateTime? DueFrom = null,
    DateTime? DueTo = null,
    bool IncludeCompleted = true,
    ProjectManagementTaskLabelFilter? LabelFilter = null);

/// <summary>对外任务写入的请求载荷。更新时 Task.VersionNo 必须与 If-Match 一致。</summary>
public sealed record ProjectManagementExternalTaskWriteRequest(ProjectManagementTaskUpsertRequest Task);

/// <summary>对外任务评论写入的请求载荷。更新时 Comment.VersionNo 必须与 If-Match 一致。</summary>
public sealed record ProjectManagementExternalTaskCommentWriteRequest(ProjectManagementTaskCommentUpsertRequest Comment);

/// <summary>对外写入的统一响应，保留 API 版本、幂等回放标记和可追踪标识。</summary>
public sealed record ProjectManagementExternalApiWriteResponse<T>(
    string ApiVersion,
    string IdempotencyKey,
    bool Replayed,
    string TraceId,
    T Result);
