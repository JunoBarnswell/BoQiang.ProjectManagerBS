namespace AsterERP.Shared;

public sealed class GridQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? Status { get; set; }

    public string? DeptId { get; set; }

    public string? PositionId { get; set; }

    public string? RoleId { get; set; }

    public string? TenantId { get; set; }

    public string? AppCode { get; set; }

    public string? UserId { get; set; }

    public string? ParentId { get; set; }

    public string? MenuType { get; set; }

    public bool IncludeDescendants { get; set; }

    public List<GridFilter> Filters { get; set; } = [];

    public List<GridSort> Sorts { get; set; } = [];

    public PageQuery ToPageQuery()
    {
        return new PageQuery(PageIndex, PageSize);
    }
}
