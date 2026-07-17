namespace AsterERP.Shared;

public sealed record PageResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageIndex,
    int PageSize);
