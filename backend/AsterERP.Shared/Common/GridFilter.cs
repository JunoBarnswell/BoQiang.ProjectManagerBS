namespace AsterERP.Shared;

public sealed class GridFilter
{
    public string Field { get; set; } = string.Empty;

    public string Operator { get; set; } = "equals";

    public object? Value { get; set; }

    public object? ValueTo { get; set; }
}
