using System.Text.Json;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai;

public sealed class AiTaskPlanValidator
{
    private const int MaxItems = 100;
    private const int MaxDepth = 4;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void ValidateUpsert(AiTaskPlanUpsertRequest request, bool requireItems)
    {
        _ = AiTaskPlanValueNormalizer.Required(request.Title, "计划标题不能为空");
        _ = AiTaskPlanValueNormalizer.Required(request.Goal, "计划目标不能为空");
        _ = AiTaskPlanValueNormalizer.PlanStatus(request.Status);
        _ = AiTaskPlanValueNormalizer.Mode(request.Mode);
        _ = AiTaskPlanValueNormalizer.ExecutionStrategy(request.ExecutionStrategy);

        if (requireItems && request.Items.Count == 0)
        {
            throw new ValidationException("任务计划至少需要一个任务", ErrorCodes.AiPlanValidationFailed);
        }

        if (request.Items.Count > MaxItems)
        {
            throw new ValidationException("任务计划最多允许 100 个任务", ErrorCodes.AiPlanValidationFailed);
        }

        ValidateItems(request.Items);
    }

    public void ValidateItems(IReadOnlyList<AiTaskPlanItemUpsertRequest> items)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            _ = AiTaskPlanValueNormalizer.Required(item.Title, "任务标题不能为空");
            _ = AiTaskPlanValueNormalizer.ItemStatus(item.Status);
            _ = AiTaskPlanValueNormalizer.Priority(item.Priority);
            var ownerType = AiTaskPlanValueNormalizer.OwnerType(item.OwnerType);
            var taskType = AiTaskPlanValueNormalizer.TaskType(item.TaskType);
            ValidateJsonArray(item.DependsOnJson, "任务依赖必须是 JSON 字符串数组");
            ValidateJsonArray(item.AcceptanceCriteriaJson, "验收标准必须是 JSON 字符串数组");

            if (!string.IsNullOrWhiteSpace(item.Id) && !ids.Add(item.Id.Trim()))
            {
                throw new ValidationException("任务临时 ID 不能重复", ErrorCodes.AiPlanValidationFailed);
            }

            if (ownerType == AiTaskPlanConstants.OwnerType.Tool && string.IsNullOrWhiteSpace(item.ToolCode))
            {
                throw new ValidationException("Tool 任务必须填写 toolCode", ErrorCodes.AiPlanValidationFailed);
            }

            if (taskType == AiTaskPlanConstants.TaskType.Tool && ownerType != AiTaskPlanConstants.OwnerType.Tool)
            {
                throw new ValidationException("Tool 类型任务负责人必须为 Tool", ErrorCodes.AiPlanValidationFailed);
            }

            if (item.MaxRetryCount is < 0 or > 10)
            {
                throw new ValidationException("最大重试次数必须在 0 到 10 之间", ErrorCodes.AiPlanValidationFailed);
            }
        }

        ValidateParentDepth(items);
        ValidateDependencyGraph(items);
    }

    public void EnsureRevision(int? expectedRevision, int currentRevision)
    {
        if (expectedRevision.HasValue && expectedRevision.Value != currentRevision)
        {
            throw new ValidationException("计划已被其他人修改，请刷新后重试", ErrorCodes.AiPlanRevisionConflict);
        }
    }

    public void EnsureUpdatedTime(DateTime? expectedUpdatedTime, DateTime? currentUpdatedTime)
    {
        if (!expectedUpdatedTime.HasValue)
        {
            return;
        }

        var current = currentUpdatedTime ?? DateTime.MinValue;
        if (Math.Abs((current - expectedUpdatedTime.Value).TotalMilliseconds) > 1000)
        {
            throw new ValidationException("任务已被其他人修改，请刷新后重试", ErrorCodes.AiPlanRevisionConflict);
        }
    }

    public static IReadOnlyList<string> ReadStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonOptions)?
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .ToList() ?? [];
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"JSON 字符串数组格式无效：{ex.Message}", ErrorCodes.AiPlanValidationFailed);
        }
    }

    private static void ValidateJsonArray(string? json, string message)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var value = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            if (value is null)
            {
                throw new JsonException("null array");
            }
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"{message}: {ex.Message}", ErrorCodes.AiPlanValidationFailed);
        }
    }

    private static void ValidateParentDepth(IReadOnlyList<AiTaskPlanItemUpsertRequest> items)
    {
        var byId = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .ToDictionary(item => item.Id!.Trim(), item => item, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var depth = 0;
            var cursor = item.ParentItemId;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (!string.IsNullOrWhiteSpace(cursor))
            {
                if (!seen.Add(cursor))
                {
                    throw new ValidationException("任务父子关系不能形成环", ErrorCodes.AiPlanValidationFailed);
                }

                if (!byId.TryGetValue(cursor, out var parent))
                {
                    throw new ValidationException("任务父级不存在", ErrorCodes.AiPlanValidationFailed);
                }

                depth++;
                if (depth > MaxDepth)
                {
                    throw new ValidationException("任务嵌套不能超过 4 层", ErrorCodes.AiPlanValidationFailed);
                }

                cursor = parent.ParentItemId;
            }
        }
    }

    private static void ValidateDependencyGraph(IReadOnlyList<AiTaskPlanItemUpsertRequest> items)
    {
        var ids = items
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .Select(item => item.Id!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items.Where(item => !string.IsNullOrWhiteSpace(item.Id)))
        {
            var itemId = item.Id!.Trim();
            var dependencies = ReadStringArray(item.DependsOnJson);
            foreach (var dependency in dependencies)
            {
                if (!ids.Contains(dependency))
                {
                    throw new ValidationException("任务依赖不存在", ErrorCodes.AiPlanValidationFailed);
                }
            }

            graph[itemId] = dependencies.ToList();
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in graph.Keys)
        {
            Visit(id, graph, visiting, visited);
        }
    }

    private static void Visit(
        string id,
        IReadOnlyDictionary<string, List<string>> graph,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(id))
        {
            return;
        }

        if (!visiting.Add(id))
        {
            throw new ValidationException("任务依赖不能形成环", ErrorCodes.AiPlanValidationFailed);
        }

        foreach (var dependency in graph.GetValueOrDefault(id) ?? [])
        {
            Visit(dependency, graph, visiting, visited);
        }

        visiting.Remove(id);
        visited.Add(id);
    }
}
