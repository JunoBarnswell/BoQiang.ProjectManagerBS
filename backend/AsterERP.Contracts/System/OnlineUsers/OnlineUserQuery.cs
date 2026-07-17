using AsterERP.Shared;

namespace AsterERP.Contracts.System.OnlineUsers;

public sealed class OnlineUserQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public List<GridFilter> Filters { get; set; } = [];

    public List<GridSort> Sorts { get; set; } = [];
}
