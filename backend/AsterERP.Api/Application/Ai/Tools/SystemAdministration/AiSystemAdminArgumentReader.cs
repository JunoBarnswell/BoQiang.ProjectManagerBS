using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools.SystemAdministration;

public static class AiSystemAdminArgumentReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static GridQuery ReadGridQuery(AiKernelFunctionContext context)
    {
        var pageIndex = Math.Max(1, ReadInt(context, "pageIndex") ?? 1);
        var pageSize = Math.Clamp(ReadInt(context, "pageSize") ?? 20, 1, 100);
        return new GridQuery
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            Keyword = ReadOptionalString(context, "keyword"),
            Status = ReadOptionalString(context, "status"),
            DeptId = ReadOptionalString(context, "deptId"),
            PositionId = ReadOptionalString(context, "positionId"),
            RoleId = ReadOptionalString(context, "roleId"),
            TenantId = context.TenantId,
            AppCode = context.AppCode,
            UserId = ReadOptionalString(context, "userId"),
            ParentId = ReadOptionalString(context, "parentId"),
            MenuType = ReadOptionalString(context, "menuType"),
            IncludeDescendants = ReadBool(context, "includeDescendants") ?? false,
            Filters = ReadList<GridFilter>(context, "filters"),
            Sorts = ReadList<GridSort>(context, "sorts")
        };
    }

    public static T ReadDto<T>(AiKernelFunctionContext context, string propertyName = "request")
    {
        var source = TryReadRaw(context, propertyName, out var value)
            ? value
            : context.Arguments;

        try
        {
            var json = JsonSerializer.Serialize(source, JsonOptions);
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new ValidationException($"参数 {propertyName} 不能为空", ErrorCodes.ParameterInvalid);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"参数 {propertyName} 结构不正确：{ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    public static string ReadRequiredString(AiKernelFunctionContext context, string propertyName)
    {
        var value = ReadOptionalString(context, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"缺少必填参数：{propertyName}", ErrorCodes.ParameterInvalid);
        }

        return value;
    }

    public static string? ReadOptionalString(AiKernelFunctionContext context, string propertyName)
    {
        if (!TryReadRaw(context, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            string text => string.IsNullOrWhiteSpace(text) ? null : text.Trim(),
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString()?.Trim(),
            _ => Convert.ToString(value)?.Trim()
        };
    }

    public static IReadOnlyList<string> ReadIds(AiKernelFunctionContext context)
    {
        var ids = ReadStringList(context, "ids");
        if (ids.Count > 0)
        {
            return ids;
        }

        var id = ReadOptionalString(context, "id");
        return string.IsNullOrWhiteSpace(id) ? [] : [id];
    }

    public static IReadOnlyList<string> ReadStringList(AiKernelFunctionContext context, string propertyName)
    {
        if (!TryReadRaw(context, propertyName, out var value) || value is null)
        {
            return [];
        }

        if (value is string text)
        {
            return string.IsNullOrWhiteSpace(text) ? [] : [text.Trim()];
        }

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions)?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"参数 {propertyName} 必须是字符串数组：{ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    public static int? ReadInt(AiKernelFunctionContext context, string propertyName)
    {
        if (!TryReadRaw(context, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            int intValue => intValue,
            long longValue => (int)Math.Clamp(longValue, int.MinValue, int.MaxValue),
            decimal decimalValue => (int)decimalValue,
            double doubleValue => (int)doubleValue,
            JsonElement element when element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var jsonInt) => jsonInt,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    public static bool? ReadBool(AiKernelFunctionContext context, string propertyName)
    {
        if (!TryReadRaw(context, propertyName, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            bool boolValue => boolValue,
            JsonElement element when element.ValueKind == JsonValueKind.True => true,
            JsonElement element when element.ValueKind == JsonValueKind.False => false,
            string text when bool.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }

    public static void EnsureNonEmpty(IReadOnlyList<string> values, string propertyName)
    {
        if (values.Count == 0)
        {
            throw new ValidationException($"缺少必填参数：{propertyName}", ErrorCodes.ParameterInvalid);
        }
    }

    public static T ClampPagedQuery<T>(T query)
    {
        switch (query)
        {
            case AsterERP.Contracts.Logs.LoginLogQuery loginLogQuery:
                loginLogQuery.PageIndex = Math.Max(1, loginLogQuery.PageIndex);
                loginLogQuery.PageSize = Math.Clamp(loginLogQuery.PageSize, 1, 100);
                break;
            case AsterERP.Contracts.Logs.OperationLogQueryRequest operationLogQuery:
                operationLogQuery.PageIndex = Math.Max(1, operationLogQuery.PageIndex);
                operationLogQuery.PageSize = Math.Clamp(operationLogQuery.PageSize, 1, 100);
                break;
            case AsterERP.Contracts.System.OnlineUsers.OnlineUserQuery onlineUserQuery:
                onlineUserQuery.PageIndex = Math.Max(1, onlineUserQuery.PageIndex);
                onlineUserQuery.PageSize = Math.Clamp(onlineUserQuery.PageSize, 1, 100);
                break;
        }

        return query;
    }

    private static List<T> ReadList<T>(AiKernelFunctionContext context, string propertyName)
    {
        if (!TryReadRaw(context, propertyName, out var value) || value is null)
        {
            return [];
        }

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryReadRaw(AiKernelFunctionContext context, string propertyName, out object? value)
    {
        if (context.Arguments.TryGetValue(propertyName, out value))
        {
            return true;
        }

        foreach (var (key, candidate) in context.Arguments)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        value = null;
        return false;
    }
}
