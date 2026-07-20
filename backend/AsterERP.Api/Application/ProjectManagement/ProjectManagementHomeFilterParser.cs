using System.Text.Json;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

internal static class ProjectManagementHomeFilterParser
{
    private static readonly HashSet<string> Fields = new(StringComparer.OrdinalIgnoreCase)
    {
        "health", "priority", "lead", "members", "status", "startDate", "targetDate", "issuesCount",
        "updated", "created", "projectKey", "workspace", "labels", "archived"
    };

    private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
    {
        "is", "isNot", "contains", "notContains", "in", "notIn", "before", "beforeOrOn", "after", "afterOrOn",
        "equals", "notEquals", "greaterThan", "greaterOrEqual", "lessThan", "lessOrEqual", "between", "today", "thisWeek", "overdue", "isEmpty", "isNotEmpty"
    };

    public static IReadOnlyList<ProjectManagementHomeFilterRule> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var rulesElement = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("rules", out var rules)
                ? rules
                : root;
            if (rulesElement.ValueKind != JsonValueKind.Array)
                throw new ValidationException("HOME 筛选器必须是包含 rules 的 JSON 对象");

            var result = new List<ProjectManagementHomeFilterRule>();
            foreach (var item in rulesElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    throw new ValidationException("HOME 筛选规则格式无效");
                var field = ReadString(item, "field");
                var op = ReadString(item, "operator");
                if (!Fields.Contains(field)) throw new ValidationException($"HOME 筛选字段不受支持：{field}");
                if (!Operators.Contains(op)) throw new ValidationException($"HOME 筛选运算符不受支持：{op}");
                if (!item.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
                    throw new ValidationException("HOME 筛选 values 必须是数组");
                var normalizedValues = values.EnumerateArray()
                    .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : throw new ValidationException("HOME 筛选值必须是字符串"))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (normalizedValues.Length == 0 && (op.Equals("isEmpty", StringComparison.OrdinalIgnoreCase) || op.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase) || op.Equals("today", StringComparison.OrdinalIgnoreCase) || op.Equals("thisWeek", StringComparison.OrdinalIgnoreCase) || op.Equals("overdue", StringComparison.OrdinalIgnoreCase)))
                    normalizedValues = ["__special__"];
                if (normalizedValues.Length == 0 || normalizedValues.Length > 50 || normalizedValues.Any(value => value.Length > 128))
                    throw new ValidationException("HOME 筛选值数量或长度超限");
                result.Add(new(field, op, normalizedValues));
            }
            if (result.Count > 40) throw new ValidationException("HOME 筛选规则数量超限");
            return result;
        }
        catch (JsonException)
        {
            throw new ValidationException("HOME 筛选器不是有效 JSON");
        }
    }

    public static string Serialize(IEnumerable<ProjectManagementHomeFilterRule> rules) =>
        JsonSerializer.Serialize(new ProjectManagementHomeFilterGroup("and", rules.ToArray()));

    private static string ReadString(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ValidationException($"HOME 筛选字段 {name} 无效");
        return value.GetString()!.Trim();
    }
}
