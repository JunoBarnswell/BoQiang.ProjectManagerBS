namespace AsterERP.Shared;

public sealed class GridPageResult<T>
{
    public long Total { get; set; }

    public List<T> Items { get; set; } = [];

    public Dictionary<string, object?>? Summary { get; set; }

    public static GridPageResult<T> FromPageResult(PageResult<T> pageResult)
    {
        return new GridPageResult<T>
        {
            Total = pageResult.TotalCount,
            Items = pageResult.Items.ToList()
        };
    }
}
