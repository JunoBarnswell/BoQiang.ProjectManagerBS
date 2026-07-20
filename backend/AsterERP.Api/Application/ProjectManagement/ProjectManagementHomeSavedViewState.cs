using System.Text.Json;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

internal static class ProjectManagementHomeSavedViewState
{
    private static readonly HashSet<string> Keys = ["version", "viewKey", "collection", "view", "filter", "sortBy", "sortDirection", "columns", "density", "insights", "insightsTab"];
    private static readonly HashSet<string> Columns = ["name", "health", "priority", "lead", "targetDate", "issues", "status"];
    private static readonly HashSet<string> Sorts = ["updated", "name", "health", "targetDate", "priority", "lead", "issues", "status"];

    public static string Normalize(string queryJson, string requestViewKey, bool isShared)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(queryJson) ? "{}" : queryJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ValidationException("HOME 视图查询状态必须是 JSON 对象");
            var source = document.RootElement;
            foreach (var property in source.EnumerateObject()) if (!Keys.Contains(property.Name)) throw new ValidationException($"HOME 视图查询状态包含不支持的字段：{property.Name}");

            var collection = ReadString(source, "collection") ?? "all";
            if (collection is not ("all" or "favorites" or "recent")) throw new ValidationException("HOME 视图集合不受支持");
            var view = ReadString(source, "view") ?? "all";
            if (view.Length > 64) throw new ValidationException("HOME 视图标识超出限制");
            var sortBy = ReadString(source, "sortBy") ?? "updated";
            if (!Sorts.Contains(sortBy)) throw new ValidationException("HOME 视图排序字段不受支持");
            var sortDirection = ReadString(source, "sortDirection") ?? "desc";
            if (sortDirection is not ("asc" or "desc")) throw new ValidationException("HOME 视图排序方向不受支持");
            var density = ReadString(source, "density") ?? "default";
            if (density is not ("compact" or "default" or "comfortable")) throw new ValidationException("HOME 视图密度不受支持");
            var insightsTab = ReadString(source, "insightsTab") ?? "health";
            if (insightsTab is not ("health" or "leads")) throw new ValidationException("HOME 视图洞察页签不受支持");
            var insights = source.TryGetProperty("insights", out var insightElement) ? RequireBoolean(insightElement, "insights") : false;
            var columns = ReadStringArray(source, "columns", 32, Columns.Count);
            if (columns.Count > 0 && columns.Any(column => !Columns.Contains(column))) throw new ValidationException("HOME 视图列配置包含不支持的字段");

            var filter = new List<ProjectManagementHomeFilterRule>();
            if (source.TryGetProperty("filter", out var filterElement) && filterElement.ValueKind != JsonValueKind.Null)
            {
                filter = ProjectManagementHomeFilterParser.Parse(filterElement.GetRawText()).ToList();
                if (isShared && filter.Any(rule => rule.Field.Equals("lead", StringComparison.OrdinalIgnoreCase) && rule.Values.Any(value => value.Length > 0)))
                    throw new ValidationException("共享 HOME 视图不能保存负责人个人筛选");
            }

            var result = new SortedDictionary<string, object?>
            {
                ["version"] = 1,
                ["viewKey"] = requestViewKey,
                ["collection"] = collection,
                ["view"] = view,
                ["sortBy"] = sortBy,
                ["sortDirection"] = sortDirection,
                ["density"] = density,
                ["insights"] = insights,
                ["insightsTab"] = insightsTab,
                ["filter"] = new ProjectManagementHomeFilterGroup("and", filter),
            };
            if (columns.Count > 0) result["columns"] = columns;
            return JsonSerializer.Serialize(result);
        }
        catch (JsonException)
        {
            throw new ValidationException("HOME 视图查询状态不是有效 JSON");
        }
    }

    private static string? ReadString(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String) throw new ValidationException($"{name} 必须是字符串");
        return value.GetString()?.Trim();
    }

    private static bool RequireBoolean(JsonElement value, string name)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new ValidationException($"{name} 必须是布尔值");
        return value.GetBoolean();
    }

    private static List<string> ReadStringArray(JsonElement source, string name, int maxLength, int maxCount)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array) throw new ValidationException($"{name} 必须是数组");
        var values = value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : throw new ValidationException($"{name} 必须包含字符串"))
            .Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.Ordinal).ToList();
        if (values.Count > maxCount || values.Any(item => item.Length > maxLength)) throw new ValidationException($"{name} 超出限制");
        return values;
    }
}
