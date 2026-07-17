namespace AsterERP.Shared;

public sealed record PageQuery(int PageIndex = 1, int PageSize = 20)
{
    public int SkipCount => Math.Max(PageIndex - 1, 0) * Math.Max(PageSize, 1);
}
