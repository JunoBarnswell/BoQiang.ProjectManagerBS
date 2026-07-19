using AsterERP.Contracts.ProjectManagement;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// M2 的统一任务查询协议。
/// 所有任务投影（树、行、卡片、看板、甘特和日历）先经过这里归一化，
/// 再交给任务服务构造数据库查询，避免视图自行解释筛选、排序和分页语义。
/// </summary>
public static class ProjectManagementTaskQueryProtocol
{
    private static readonly string[] ViewKeys = ["tree", "list", "card", "board", "gantt", "calendar"];
    private static readonly string[] SortFields = ["tree", "dueDate", "priority", "status", "updated"];
    private static readonly string[] GroupFields = ["status", "priority", "assignee", "milestone", "parent", "label"];

    public static ProjectManagementTaskQuery Normalize(ProjectManagementTaskQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var projectId = NormalizeRequired(query.ProjectId, "项目不能为空");
        var viewKey = NormalizeAllowed(query.ViewKey, ViewKeys, "任务视图类型");
        var sortBy = NormalizeAllowed(query.SortBy, SortFields, "任务排序字段");
        var sortDirection = NormalizeAllowed(query.SortDirection, ["asc", "desc"], "任务排序方向");
        var groupBy = NormalizeOptionalAllowed(query.GroupBy, GroupFields, "任务分组字段");
        var status = NormalizeOptional(query.Status);
        var assigneeUserId = NormalizeOptional(query.AssigneeUserId);
        var milestoneId = NormalizeOptional(query.MilestoneId);
        var parentTaskId = NormalizeOptional(query.ParentTaskId);
        var keyword = NormalizeOptional(query.Keyword);

        if (status is not null)
            ProjectManagementDomainRules.RequireTaskStatus(status);
        if (query.DueFrom.HasValue && query.DueTo.HasValue && query.DueFrom > query.DueTo)
            throw new ValidationException("任务截止日期筛选区间无效");

        return query with
        {
            ProjectId = projectId,
            ViewKey = viewKey,
            SortBy = sortBy,
            SortDirection = sortDirection,
            GroupBy = groupBy,
            Keyword = keyword,
            Status = status,
            AssigneeUserId = assigneeUserId,
            MilestoneId = milestoneId,
            ParentTaskId = parentTaskId,
            PageIndex = Math.Max(query.PageIndex, 1),
            PageSize = Math.Clamp(query.PageSize, 1, 200)
        };
    }

    private static string NormalizeRequired(string? value, string message) =>
        NormalizeOptional(value) ?? throw new ValidationException(message);

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeAllowed(string? value, IReadOnlyCollection<string> allowed, string fieldName)
    {
        var normalized = NormalizeRequired(value, $"{fieldName}不能为空");
        var match = allowed.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ValidationException($"{fieldName}不受支持");
    }

    private static string? NormalizeOptionalAllowed(string? value, IReadOnlyCollection<string> allowed, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null) return null;
        var match = allowed.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ValidationException($"{fieldName}不受支持");
    }
}
