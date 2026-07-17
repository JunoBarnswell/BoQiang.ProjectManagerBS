namespace AsterERP.Shared;

public sealed class GridSort
{
    public string Field { get; set; } = string.Empty;

    public string Order { get; set; } = "asc";

    public bool Descending => string.Equals(Order, "desc", StringComparison.OrdinalIgnoreCase);
}
