using System.Text.Json;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

public static class ProjectManagementSavedViewState
{
    private static readonly HashSet<string> Keys = ["version", "viewKey", "keyword", "status", "assigneeUserId", "milestoneId", "groupBy", "dueFrom", "dueTo", "includeCompleted", "sortBy", "sortDirection", "labelIds", "labelMatchMode", "visibleColumns", "ganttZoom"];
    private static readonly HashSet<string> ViewKeys = ["tree", "list", "card", "board", "gantt", "calendar"];
    private static readonly HashSet<string> Groups = ["status", "priority", "assignee", "milestone", "parent", "label"];
    private static readonly HashSet<string> Sorts = ["tree", "dueDate", "priority", "status", "updated"];
    private static readonly HashSet<string> VisibleColumns = ["taskCode", "title", "status", "priority", "progressPercent", "dueDate", "blockedByCount"];

    public static string Normalize(string queryJson, string requestViewKey, bool isShared)
    {
        try
        {
            using var document = JsonDocument.Parse(queryJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ValidationException("视图查询状态必须是 JSON 对象");
            var source = document.RootElement;
            foreach (var property in source.EnumerateObject()) if (!Keys.Contains(property.Name)) throw new ValidationException($"视图查询状态包含不支持的字段：{property.Name}");
            var version = source.TryGetProperty("version", out var versionElement) ? RequireInteger(versionElement, "version") : 1;
            if (version is < 1 or > 2) throw new ValidationException("视图查询状态版本不受支持");
            var viewKey = ReadString(source, "viewKey") ?? requestViewKey;
            if (!ViewKeys.Contains(viewKey) || !string.Equals(viewKey, requestViewKey, StringComparison.Ordinal)) throw new ValidationException("视图类型与查询状态不一致");
            var assignee = ReadString(source, "assigneeUserId");
            if (isShared && !string.IsNullOrWhiteSpace(assignee)) throw new ValidationException("共享视图不能保存负责人个人筛选");
            var groupBy = ReadString(source, "groupBy"); if (groupBy is not null && !Groups.Contains(groupBy)) throw new ValidationException("视图分组不受支持");
            var sortBy = ReadString(source, "sortBy") ?? "tree"; if (!Sorts.Contains(sortBy)) throw new ValidationException("视图排序不受支持");
            var sortDirection = ReadString(source, "sortDirection") ?? "asc"; if (sortDirection is not ("asc" or "desc")) throw new ValidationException("视图排序方向不受支持");
            var dueFrom = ReadDate(source, "dueFrom"); var dueTo = ReadDate(source, "dueTo"); if (dueFrom.HasValue && dueTo.HasValue && dueFrom > dueTo) throw new ValidationException("截止日期范围无效");
            var result = new SortedDictionary<string, object?> { ["version"] = 2, ["viewKey"] = requestViewKey, ["sortBy"] = sortBy, ["sortDirection"] = sortDirection };
            AddString(result, "keyword", ReadString(source, "keyword"), 200); AddString(result, "status", ReadString(source, "status"), 32); AddString(result, "milestoneId", ReadString(source, "milestoneId"), 64); AddString(result, "groupBy", groupBy, 32);
            if (!isShared) AddString(result, "assigneeUserId", assignee, 64);
            if (source.TryGetProperty("includeCompleted", out var includeCompleted)) { if (includeCompleted.ValueKind != JsonValueKind.True && includeCompleted.ValueKind != JsonValueKind.False) throw new ValidationException("includeCompleted 必须是布尔值"); result["includeCompleted"] = includeCompleted.GetBoolean(); }
            if (dueFrom.HasValue) result["dueFrom"] = dueFrom.Value.ToString("yyyy-MM-dd"); if (dueTo.HasValue) result["dueTo"] = dueTo.Value.ToString("yyyy-MM-dd");
            var labelIds = ReadStringArray(source, "labelIds", 64, 50);
            if (labelIds.Count > 0) result["labelIds"] = labelIds;
            var labelMatchMode = ReadString(source, "labelMatchMode");
            if (labelMatchMode is not null && labelMatchMode is not ("Any" or "All")) throw new ValidationException("标签筛选匹配方式不受支持");
            if (labelMatchMode is not null) result["labelMatchMode"] = labelMatchMode;
            var columns = ReadStringArray(source, "visibleColumns", 32, VisibleColumns.Count);
            if (columns.Count > 0)
            {
                if (columns.Any(column => !VisibleColumns.Contains(column))) throw new ValidationException("视图列配置包含不支持的字段");
                result["visibleColumns"] = columns;
            }
            if (source.TryGetProperty("ganttZoom", out var ganttZoom))
            {
                var zoom = RequireInteger(ganttZoom, "ganttZoom");
                if (zoom is not (28 or 56 or 84)) throw new ValidationException("甘特缩放级别不受支持");
                result["ganttZoom"] = zoom;
            }
            return JsonSerializer.Serialize(result);
        }
        catch (JsonException) { throw new ValidationException("视图查询状态不是有效 JSON"); }
    }

    private static string? ReadString(JsonElement source, string name) { if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null; if (value.ValueKind != JsonValueKind.String) throw new ValidationException($"{name} 必须是字符串"); return value.GetString()?.Trim(); }
    private static int RequireInteger(JsonElement value, string name) { if (!value.TryGetInt32(out var number)) throw new ValidationException($"{name} 必须是整数"); return number; }
    private static List<string> ReadStringArray(JsonElement source, string name, int maxLength, int maxCount)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return [];
        if (value.ValueKind != JsonValueKind.Array) throw new ValidationException($"{name} 必须是数组");
        var values = value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : throw new ValidationException($"{name} 必须包含字符串")).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct(StringComparer.Ordinal).ToList();
        if (values.Count > maxCount || values.Any(item => item.Length > maxLength)) throw new ValidationException($"{name} 超出限制");
        return values;
    }
    private static DateTime? ReadDate(JsonElement source, string name) { var value = ReadString(source, name); if (value is null) return null; if (!DateTime.TryParse(value, out var date)) throw new ValidationException($"{name} 必须是 ISO 日期"); return date.ToUniversalTime(); }
    private static void AddString(IDictionary<string, object?> target, string name, string? value, int maxLength) { if (string.IsNullOrWhiteSpace(value)) return; if (value.Length > maxLength) throw new ValidationException($"{name} 长度超限"); target[name] = value; }
}
