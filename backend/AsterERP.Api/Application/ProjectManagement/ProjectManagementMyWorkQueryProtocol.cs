using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 我的工作沿用任务查询的分页、排序和输入归一化语义，但以用户工作分类替代项目视图。
/// </summary>
public static class ProjectManagementMyWorkQueryProtocol
{
    private static readonly string[] Categories = ["all", "assigned", "participating", "created", "mentioned", "today", "upcoming", "overdue", "blocked"];
    private static readonly string[] SortFields = ["dueDate", "updated", "created", "priority"];

    public static ProjectManagementMyWorkQuery Normalize(ProjectManagementMyWorkQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return query with
        {
            PageIndex = Math.Max(query.PageIndex, 1),
            PageSize = Math.Clamp(query.PageSize, 1, 200),
            ProjectId = NormalizeOptional(query.ProjectId),
            Category = NormalizeAllowed(query.Category, Categories, "我的工作分类", "all"),
            SortBy = NormalizeAllowed(query.SortBy, SortFields, "我的工作排序字段", "dueDate"),
            SortDirection = NormalizeAllowed(query.SortDirection, ["asc", "desc"], "我的工作排序方向", "asc")
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string NormalizeAllowed(string? value, IReadOnlyCollection<string> allowed, string fieldName, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var match = allowed.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new ValidationException($"{fieldName}不受支持");
    }
}
