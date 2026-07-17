using System.Linq.Expressions;
using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Common;

public static class GridFilterApplier
{
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "contains",
        "equals",
        "eq",
        "notEquals",
        "ne",
        "startsWith",
        "endsWith",
        "gt",
        "gte",
        "lt",
        "lte",
        "between"
    };

    public static TQueryable Apply<TQueryable>(
        TQueryable query,
        IReadOnlyList<GridFilter>? filters,
        IReadOnlyDictionary<string, Func<TQueryable, GridFilter, TQueryable>> filterers)
    {
        if (filters is null || filters.Count == 0)
        {
            return query;
        }

        var nextQuery = query;

        foreach (var filter in filters)
        {
            var field = filter.Field?.Trim();
            if (string.IsNullOrWhiteSpace(field) || !HasUsableValue(filter))
            {
                continue;
            }

            if (!filterers.TryGetValue(field, out var applyFilter))
            {
                throw new ValidationException($"筛选字段不允许: {field}");
            }

            nextQuery = applyFilter(nextQuery, NormalizeFilter(filter, field));
        }

        return nextQuery;
    }

    public static string NormalizeOperator(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "equals" : value.Trim();
        normalized = normalized switch
        {
            "eq" => "equals",
            "ne" => "notEquals",
            _ => normalized
        };

        if (!SupportedOperators.Contains(normalized))
        {
            throw new ValidationException($"筛选操作符无效: {value}");
        }

        return normalized;
    }

    public static void EnsureOperator(GridFilter filter, params string[] allowedOperators)
    {
        var normalized = NormalizeOperator(filter.Operator);
        if (allowedOperators.Any(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new ValidationException($"筛选操作符不支持: {filter.Field}.{normalized}");
    }

    public static ISugarQueryable<TEntity> ApplyString<TEntity>(
        ISugarQueryable<TEntity> query,
        GridFilter filter,
        Expression<Func<TEntity, string?>> selector)
    {
        var value = GetStringValue(filter) ?? string.Empty;
        var operatorName = NormalizeOperator(filter.Operator);
        var parameter = selector.Parameters[0];
        var member = selector.Body;
        var valueExpression = Expression.Constant(value, typeof(string));

        Expression body = operatorName switch
        {
            "contains" => BuildStringCall(member, valueExpression, nameof(string.Contains)),
            "equals" => Expression.Equal(member, valueExpression),
            "notEquals" => Expression.NotEqual(member, valueExpression),
            "startsWith" => BuildStringCall(member, valueExpression, nameof(string.StartsWith)),
            "endsWith" => BuildStringCall(member, valueExpression, nameof(string.EndsWith)),
            _ => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    public static ISugarQueryable<TEntity, TEntity2, TEntity3> ApplyString<TEntity, TEntity2, TEntity3>(
        ISugarQueryable<TEntity, TEntity2, TEntity3> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, TEntity3, string?>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, TEntity3, bool>>(
            BuildStringPredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity, TEntity2> ApplyString<TEntity, TEntity2>(
        ISugarQueryable<TEntity, TEntity2> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, string?>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, bool>>(
            BuildStringPredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> ApplyString<TEntity, TEntity2, TEntity3, TEntity4, TEntity5>(
        ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, string?>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, bool>>(
            BuildStringPredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity> ApplyBoolean<TEntity>(
        ISugarQueryable<TEntity> query,
        GridFilter filter,
        Expression<Func<TEntity, bool>> selector)
    {
        var value = GetBooleanValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var operatorName = NormalizeOperator(filter.Operator);
        var parameter = selector.Parameters[0];
        var valueExpression = Expression.Constant(value, typeof(bool));

        var body = operatorName switch
        {
            "equals" => Expression.Equal(selector.Body, valueExpression),
            "notEquals" => Expression.NotEqual(selector.Body, valueExpression),
            _ => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    public static ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> ApplyBoolean<TEntity, TEntity2, TEntity3, TEntity4, TEntity5>(
        ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, bool>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, bool>>(
            BuildBooleanPredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity> ApplyInt32<TEntity>(
        ISugarQueryable<TEntity> query,
        GridFilter filter,
        Expression<Func<TEntity, int>> selector)
    {
        var value = GetInt32Value(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var operatorName = NormalizeOperator(filter.Operator);
        var parameter = selector.Parameters[0];
        var valueExpression = Expression.Constant(value, typeof(int));

        Expression body = operatorName switch
        {
            "equals" => Expression.Equal(selector.Body, valueExpression),
            "notEquals" => Expression.NotEqual(selector.Body, valueExpression),
            "gt" => Expression.GreaterThan(selector.Body, valueExpression),
            "gte" => Expression.GreaterThanOrEqual(selector.Body, valueExpression),
            "lt" => Expression.LessThan(selector.Body, valueExpression),
            "lte" => Expression.LessThanOrEqual(selector.Body, valueExpression),
            "between" => BuildBetween(selector.Body, value, GetInt32ValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            _ => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    public static ISugarQueryable<TEntity> ApplyInt64<TEntity>(
        ISugarQueryable<TEntity> query,
        GridFilter filter,
        Expression<Func<TEntity, long>> selector)
    {
        var value = GetInt64Value(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var operatorName = NormalizeOperator(filter.Operator);
        var parameter = selector.Parameters[0];
        var valueExpression = Expression.Constant(value, typeof(long));

        Expression body = operatorName switch
        {
            "equals" => Expression.Equal(selector.Body, valueExpression),
            "notEquals" => Expression.NotEqual(selector.Body, valueExpression),
            "gt" => Expression.GreaterThan(selector.Body, valueExpression),
            "gte" => Expression.GreaterThanOrEqual(selector.Body, valueExpression),
            "lt" => Expression.LessThan(selector.Body, valueExpression),
            "lte" => Expression.LessThanOrEqual(selector.Body, valueExpression),
            "between" => BuildBetween(selector.Body, value, GetInt64ValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            _ => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    public static ISugarQueryable<TEntity> ApplyDateTime<TEntity>(
        ISugarQueryable<TEntity> query,
        GridFilter filter,
        Expression<Func<TEntity, DateTime>> selector)
    {
        var value = GetDateTimeValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var operatorName = NormalizeOperator(filter.Operator);
        var parameter = selector.Parameters[0];
        var valueExpression = Expression.Constant(value, typeof(DateTime));

        Expression body = operatorName switch
        {
            "equals" => Expression.Equal(selector.Body, valueExpression),
            "notEquals" => Expression.NotEqual(selector.Body, valueExpression),
            "gt" => Expression.GreaterThan(selector.Body, valueExpression),
            "gte" => Expression.GreaterThanOrEqual(selector.Body, valueExpression),
            "lt" => Expression.LessThan(selector.Body, valueExpression),
            "lte" => Expression.LessThanOrEqual(selector.Body, valueExpression),
            "between" => BuildBetween(selector.Body, value, GetDateTimeValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            _ => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    public static ISugarQueryable<TEntity, TEntity2, TEntity3> ApplyDateTime<TEntity, TEntity2, TEntity3>(
        ISugarQueryable<TEntity, TEntity2, TEntity3> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, TEntity3, DateTime>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, TEntity3, bool>>(
            BuildDateTimePredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity, TEntity2> ApplyDateTime<TEntity, TEntity2>(
        ISugarQueryable<TEntity, TEntity2> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, DateTime>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, bool>>(
            BuildDateTimePredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> ApplyDateTime<TEntity, TEntity2, TEntity3, TEntity4, TEntity5>(
        ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, DateTime>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, bool>>(
            BuildDateTimePredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity> ApplyNullableDateTime<TEntity>(
        ISugarQueryable<TEntity> query,
        GridFilter filter,
        Expression<Func<TEntity, DateTime?>> selector)
    {
        var value = GetDateTimeValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var operatorName = NormalizeOperator(filter.Operator);
        var parameter = selector.Parameters[0];
        var hasValue = Expression.Property(selector.Body, nameof(Nullable<DateTime>.HasValue));
        var memberValue = Expression.Property(selector.Body, nameof(Nullable<DateTime>.Value));
        var valueExpression = Expression.Constant(value, typeof(DateTime));

        Expression comparison = operatorName switch
        {
            "equals" => Expression.Equal(memberValue, valueExpression),
            "notEquals" => Expression.NotEqual(memberValue, valueExpression),
            "gt" => Expression.GreaterThan(memberValue, valueExpression),
            "gte" => Expression.GreaterThanOrEqual(memberValue, valueExpression),
            "lt" => Expression.LessThan(memberValue, valueExpression),
            "lte" => Expression.LessThanOrEqual(memberValue, valueExpression),
            "between" => BuildBetween(memberValue, value, GetDateTimeValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            _ => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };

        var body = operatorName == "notEquals"
            ? Expression.OrElse(Expression.Not(hasValue), comparison)
            : Expression.AndAlso(hasValue, comparison);

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    public static ISugarQueryable<TEntity, TEntity2, TEntity3> ApplyNullableDateTime<TEntity, TEntity2, TEntity3>(
        ISugarQueryable<TEntity, TEntity2, TEntity3> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, TEntity3, DateTime?>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, TEntity3, bool>>(
            BuildNullableDateTimePredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity, TEntity2> ApplyNullableDateTime<TEntity, TEntity2>(
        ISugarQueryable<TEntity, TEntity2> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, DateTime?>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, bool>>(
            BuildNullableDateTimePredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> ApplyNullableDateTime<TEntity, TEntity2, TEntity3, TEntity4, TEntity5>(
        ISugarQueryable<TEntity, TEntity2, TEntity3, TEntity4, TEntity5> query,
        GridFilter filter,
        Expression<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, DateTime?>> selector)
    {
        return query.Where(Expression.Lambda<Func<TEntity, TEntity2, TEntity3, TEntity4, TEntity5, bool>>(
            BuildNullableDateTimePredicate(selector.Body, filter),
            selector.Parameters));
    }

    public static TQueryable ApplyText<TQueryable>(
        TQueryable query,
        GridFilter filter,
        Func<TQueryable, string, TQueryable> contains,
        Func<TQueryable, string, TQueryable> equals,
        Func<TQueryable, string, TQueryable> notEquals,
        Func<TQueryable, string, TQueryable> startsWith,
        Func<TQueryable, string, TQueryable> endsWith)
    {
        var value = GetStringValue(filter) ?? string.Empty;
        return NormalizeOperator(filter.Operator) switch
        {
            "contains" => contains(query, value),
            "equals" => equals(query, value),
            "notEquals" => notEquals(query, value),
            "startsWith" => startsWith(query, value),
            "endsWith" => endsWith(query, value),
            var operatorName => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };
    }

    public static TQueryable ApplyBoolean<TQueryable>(
        TQueryable query,
        GridFilter filter,
        Func<TQueryable, bool, TQueryable> equals,
        Func<TQueryable, bool, TQueryable> notEquals)
    {
        var value = GetBooleanValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        return NormalizeOperator(filter.Operator) switch
        {
            "equals" => equals(query, value),
            "notEquals" => notEquals(query, value),
            var operatorName => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };
    }

    public static TQueryable ApplyInt32<TQueryable>(
        TQueryable query,
        GridFilter filter,
        Func<TQueryable, int, TQueryable> equals,
        Func<TQueryable, int, TQueryable> notEquals,
        Func<TQueryable, int, TQueryable> gt,
        Func<TQueryable, int, TQueryable> gte,
        Func<TQueryable, int, TQueryable> lt,
        Func<TQueryable, int, TQueryable> lte,
        Func<TQueryable, int, int, TQueryable> between)
    {
        var value = GetInt32Value(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        return NormalizeOperator(filter.Operator) switch
        {
            "equals" => equals(query, value),
            "notEquals" => notEquals(query, value),
            "gt" => gt(query, value),
            "gte" => gte(query, value),
            "lt" => lt(query, value),
            "lte" => lte(query, value),
            "between" => between(query, value, GetInt32ValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            var operatorName => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };
    }

    public static TQueryable ApplyDateTime<TQueryable>(
        TQueryable query,
        GridFilter filter,
        Func<TQueryable, DateTime, TQueryable> equals,
        Func<TQueryable, DateTime, TQueryable> notEquals,
        Func<TQueryable, DateTime, TQueryable> gt,
        Func<TQueryable, DateTime, TQueryable> gte,
        Func<TQueryable, DateTime, TQueryable> lt,
        Func<TQueryable, DateTime, TQueryable> lte,
        Func<TQueryable, DateTime, DateTime, TQueryable> between)
    {
        var value = GetDateTimeValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        return NormalizeOperator(filter.Operator) switch
        {
            "equals" => equals(query, value),
            "notEquals" => notEquals(query, value),
            "gt" => gt(query, value),
            "gte" => gte(query, value),
            "lt" => lt(query, value),
            "lte" => lte(query, value),
            "between" => between(query, value, GetDateTimeValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            var operatorName => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };
    }

    public static bool HasUsableValue(GridFilter filter)
    {
        var normalizedOperator = NormalizeOperator(filter.Operator);
        return normalizedOperator == "between"
            ? HasSingleValue(filter.Value) && HasSingleValue(filter.ValueTo)
            : HasSingleValue(filter.Value);
    }

    public static string? GetStringValue(GridFilter filter)
    {
        var value = NormalizeScalar(filter.Value)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static string? GetStringValueTo(GridFilter filter)
    {
        var value = NormalizeScalar(filter.ValueTo)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static bool? GetBooleanValue(GridFilter filter)
    {
        var value = GetStringValue(filter);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "enabled", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new ValidationException($"筛选值不是有效布尔值: {filter.Field}");
    }

    public static int? GetInt32Value(GridFilter filter)
    {
        var value = GetStringValue(filter);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out var number))
        {
            return number;
        }

        throw new ValidationException($"筛选值不是有效整数: {filter.Field}");
    }

    public static int? GetInt32ValueTo(GridFilter filter)
    {
        var value = GetStringValueTo(filter);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, out var number))
        {
            return number;
        }

        throw new ValidationException($"筛选值不是有效整数: {filter.Field}");
    }

    public static long? GetInt64Value(GridFilter filter)
    {
        var value = GetStringValue(filter);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, out var number))
        {
            return number;
        }

        throw new ValidationException($"筛选值不是有效长整数: {filter.Field}");
    }

    public static long? GetInt64ValueTo(GridFilter filter)
    {
        var value = GetStringValueTo(filter);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, out var number))
        {
            return number;
        }

        throw new ValidationException($"筛选值不是有效长整数: {filter.Field}");
    }

    public static DateTime? GetDateTimeValue(GridFilter filter)
    {
        var value = GetStringValue(filter);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return dateTime;
        }

        throw new ValidationException($"筛选值不是有效日期: {filter.Field}");
    }

    public static DateTime? GetDateTimeValueTo(GridFilter filter)
    {
        var value = GetStringValueTo(filter);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return dateTime;
        }

        throw new ValidationException($"筛选值不是有效日期: {filter.Field}");
    }

    private static Expression BuildStringCall(Expression member, Expression valueExpression, string methodName)
    {
        var method = typeof(string).GetMethod(methodName, [typeof(string)])
            ?? throw new InvalidOperationException($"String method not found: {methodName}");
        return Expression.AndAlso(
            Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
            Expression.Call(member, method, valueExpression));
    }

    private static Expression BuildStringPredicate(Expression member, GridFilter filter)
    {
        var value = GetStringValue(filter) ?? string.Empty;
        var valueExpression = Expression.Constant(value, typeof(string));
        return NormalizeOperator(filter.Operator) switch
        {
            "contains" => BuildStringCall(member, valueExpression, nameof(string.Contains)),
            "equals" => Expression.Equal(member, valueExpression),
            "notEquals" => Expression.NotEqual(member, valueExpression),
            "startsWith" => BuildStringCall(member, valueExpression, nameof(string.StartsWith)),
            "endsWith" => BuildStringCall(member, valueExpression, nameof(string.EndsWith)),
            var operatorName => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };
    }

    private static Expression BuildBooleanPredicate(Expression member, GridFilter filter)
    {
        var value = GetBooleanValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var valueExpression = Expression.Constant(value, typeof(bool));
        return NormalizeOperator(filter.Operator) switch
        {
            "equals" => Expression.Equal(member, valueExpression),
            "notEquals" => Expression.NotEqual(member, valueExpression),
            var operatorName => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };
    }

    private static Expression BuildDateTimePredicate(Expression member, GridFilter filter)
    {
        var value = GetDateTimeValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var valueExpression = Expression.Constant(value, typeof(DateTime));
        return NormalizeOperator(filter.Operator) switch
        {
            "equals" => Expression.Equal(member, valueExpression),
            "notEquals" => Expression.NotEqual(member, valueExpression),
            "gt" => Expression.GreaterThan(member, valueExpression),
            "gte" => Expression.GreaterThanOrEqual(member, valueExpression),
            "lt" => Expression.LessThan(member, valueExpression),
            "lte" => Expression.LessThanOrEqual(member, valueExpression),
            "between" => BuildBetween(member, value, GetDateTimeValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            var operatorName => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };
    }

    private static Expression BuildNullableDateTimePredicate(Expression member, GridFilter filter)
    {
        var value = GetDateTimeValue(filter) ?? throw new ValidationException($"筛选值不能为空: {filter.Field}");
        var operatorName = NormalizeOperator(filter.Operator);
        var hasValue = Expression.Property(member, nameof(Nullable<DateTime>.HasValue));
        var memberValue = Expression.Property(member, nameof(Nullable<DateTime>.Value));
        var valueExpression = Expression.Constant(value, typeof(DateTime));

        Expression comparison = operatorName switch
        {
            "equals" => Expression.Equal(memberValue, valueExpression),
            "notEquals" => Expression.NotEqual(memberValue, valueExpression),
            "gt" => Expression.GreaterThan(memberValue, valueExpression),
            "gte" => Expression.GreaterThanOrEqual(memberValue, valueExpression),
            "lt" => Expression.LessThan(memberValue, valueExpression),
            "lte" => Expression.LessThanOrEqual(memberValue, valueExpression),
            "between" => BuildBetween(memberValue, value, GetDateTimeValueTo(filter) ?? throw new ValidationException($"筛选区间值不能为空: {filter.Field}")),
            _ => throw new ValidationException($"筛选操作符不支持: {filter.Field}.{operatorName}")
        };

        return operatorName == "notEquals"
            ? Expression.OrElse(Expression.Not(hasValue), comparison)
            : Expression.AndAlso(hasValue, comparison);
    }

    private static Expression BuildBetween<TValue>(Expression member, TValue value, TValue valueTo)
    {
        var from = Comparer<TValue>.Default.Compare(value, valueTo) <= 0 ? value : valueTo;
        var to = Comparer<TValue>.Default.Compare(value, valueTo) <= 0 ? valueTo : value;
        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(member, Expression.Constant(from, typeof(TValue))),
            Expression.LessThanOrEqual(member, Expression.Constant(to, typeof(TValue))));
    }

    private static GridFilter NormalizeFilter(GridFilter filter, string field)
    {
        return new GridFilter
        {
            Field = field,
            Operator = NormalizeOperator(filter.Operator),
            Value = filter.Value,
            ValueTo = filter.ValueTo
        };
    }

    private static bool HasSingleValue(object? value)
    {
        return value switch
        {
            null => false,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => false,
            JsonElement element => !string.IsNullOrWhiteSpace(NormalizeJsonElement(element)),
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true
        };
    }

    private static string? NormalizeScalar(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => NormalizeJsonElement(element),
            _ => value.ToString()
        };
    }

    private static string? NormalizeJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue.ToString(),
            JsonValueKind.Number when element.TryGetDouble(out var doubleValue) => doubleValue.ToString("G"),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }
}
