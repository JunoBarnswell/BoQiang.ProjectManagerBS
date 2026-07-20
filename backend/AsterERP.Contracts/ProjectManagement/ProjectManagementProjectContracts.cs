namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementProjectQuery(
    int PageIndex = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? Status = null,
    string? OwnerUserId = null);

public sealed record ProjectManagementProjectUpsertRequest(
    string ProjectCode,
    string ProjectName,
    string? Description = null,
    string Status = "Planning",
    string Priority = "Medium",
    string? OwnerUserId = null,
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    int? WipLimit = null,
    decimal ProgressPercent = 0,
    long VersionNo = 0,
    string? ClientMutationId = null,
    IReadOnlyList<ProjectManagementProjectInitialMemberUpsertRequest>? InitialMembers = null);

/// <summary>
/// Member configuration captured while a project is being created. The
/// project owner is created by the server and must not be included here.
/// </summary>
public sealed record ProjectManagementProjectInitialMemberUpsertRequest(
    string UserId,
    string? EmploymentId = null,
    string RoleCode = "Member",
    string? ScopeRootTaskId = null);

public sealed record ProjectManagementProjectArchiveRequest(long VersionNo, string? ClientMutationId = null);

/// <summary>
/// 发生乐观并发冲突时，原始客户端提交的项目值。
/// SubmittedFields 明确哪些字段属于本次操作，避免归档、恢复、删除等操作把未提交字段误判为冲突。
/// </summary>
public sealed record ProjectManagementProjectConflictLocalValues(
    string Operation,
    long VersionNo,
    IReadOnlyList<string> SubmittedFields,
    string? ProjectCode = null,
    string? ProjectName = null,
    string? Description = null,
    string? Status = null,
    string? Priority = null,
    string? OwnerUserId = null,
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    int? WipLimit = null,
    decimal? ProgressPercent = null,
    bool? IsDeleted = null)
{
    public static ProjectManagementProjectConflictLocalValues FromUpdate(ProjectManagementProjectUpsertRequest request) => new(
        "update", request.VersionNo,
        ["VersionNo", "ProjectCode", "ProjectName", "Description", "Status", "Priority", "OwnerUserId", "StartDate", "DueDate", "WipLimit", "ProgressPercent"],
        request.ProjectCode, request.ProjectName, request.Description, request.Status, request.Priority, request.OwnerUserId,
        request.StartDate, request.DueDate, request.WipLimit, request.ProgressPercent);

    public static ProjectManagementProjectConflictLocalValues ForArchive(long versionNo) => new(
        "archive", versionNo, ["VersionNo", "Status"], Status: "Archived");

    public static ProjectManagementProjectConflictLocalValues ForRestore(long versionNo) => new(
        "restore", versionNo, ["VersionNo", "IsDeleted"], IsDeleted: false);

    public static ProjectManagementProjectConflictLocalValues ForDelete(long versionNo) => new(
        "delete", versionNo, ["VersionNo", "IsDeleted"], IsDeleted: true);
}

public sealed record ProjectManagementProjectConflictField(
    string Field,
    string DisplayName,
    object? ServerValue,
    object? LocalValue);

/// <summary>
/// 409 项目并发响应。服务端当前值和客户端提交值均保留，客户端可只处理 FieldConflicts 中的字段。
/// </summary>
public sealed record ProjectManagementProjectVersionConflictResponse(
    ProjectManagementProjectResponse ServerValues,
    ProjectManagementProjectConflictLocalValues LocalValues,
    IReadOnlyList<ProjectManagementProjectConflictField> FieldConflicts);

public sealed record ProjectManagementProjectResponse(
    string Id,
    string TenantId,
    string AppCode,
    string ProjectCode,
    string ProjectName,
    string? Description,
    string Status,
    string Priority,
    string OwnerUserId,
    DateTime? StartDate,
    DateTime? DueDate,
    DateTime? CompletedAt,
    int? WipLimit,
    decimal ProgressPercent,
    long VersionNo,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    string? OwnerDisplayName = null);
