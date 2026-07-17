using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Common;

public static class GridSortApplier
{
    public static TQueryable Apply<TQueryable>(
        TQueryable query,
        IReadOnlyList<GridSort>? sorts,
        Func<TQueryable, string, OrderByType, TQueryable?> applySort,
        Func<TQueryable, TQueryable> applyDefaultSort)
    {
        return ApplyCore(
            query,
            sorts,
            (nextQuery, field, order) =>
                applySort(nextQuery, field, order) ?? throw new ValidationException($"排序字段不允许: {field}"),
            applyDefaultSort);
    }

    public static TQueryable Apply<TQueryable>(
        TQueryable query,
        IReadOnlyList<GridSort>? sorts,
        IReadOnlyDictionary<string, Func<TQueryable, OrderByType, TQueryable>> sorters,
        Func<TQueryable, TQueryable> applyDefaultSort)
    {
        return ApplyCore(
            query,
            sorts,
            (nextQuery, field, order) =>
            {
                if (!sorters.TryGetValue(field, out var applySort))
                {
                    throw new ValidationException($"排序字段不允许: {field}");
                }

                return applySort(nextQuery, order);
            },
            applyDefaultSort);
    }

    public static OrderByType NormalizeOrder(string? order)
    {
        if (string.IsNullOrWhiteSpace(order) || order.Equals("asc", StringComparison.OrdinalIgnoreCase))
        {
            return OrderByType.Asc;
        }

        if (order.Equals("desc", StringComparison.OrdinalIgnoreCase))
        {
            return OrderByType.Desc;
        }

        throw new ValidationException($"排序方向无效: {order}");
    }

    private static TQueryable ApplyCore<TQueryable>(
        TQueryable query,
        IReadOnlyList<GridSort>? sorts,
        Func<TQueryable, string, OrderByType, TQueryable> applySort,
        Func<TQueryable, TQueryable> applyDefaultSort)
    {
        if (sorts is null || sorts.Count == 0)
        {
            return applyDefaultSort(query);
        }

        var applied = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nextQuery = query;

        foreach (var sort in sorts)
        {
            var field = sort.Field?.Trim();
            if (string.IsNullOrWhiteSpace(field) || !seen.Add(field))
            {
                continue;
            }

            nextQuery = applySort(nextQuery, field, NormalizeOrder(sort.Order));
            applied = true;
        }

        return applied ? nextQuery : applyDefaultSort(query);
    }
}
