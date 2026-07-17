using System.Text;
using System.Text.Json;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai;

public sealed class AiPlanParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AiTaskPlanUpsertRequest Parse(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(ExtractJson(content));
            var root = document.RootElement;
            var items = new List<AiTaskPlanItemUpsertRequest>();
            FlattenItems(ReadArray(root, "items") ?? ReadArray(root, "tasks"), null, 0, 1, items);
            if (items.Count == 0)
            {
                throw new JsonException("items 不能为空");
            }

            var title = ReadString(root, "title") ?? "AI 任务计划";
            var goal = ReadString(root, "goal") ?? ReadString(root, "overview") ?? title;
            return new AiTaskPlanUpsertRequest
            {
                Title = title,
                Goal = goal,
                Status = AiTaskPlanConstants.PlanStatus.PlanReady,
                Mode = "Plan",
                ExecutionStrategy = ReadString(root, "executionStrategy") ?? "Serial",
                RisksJson = SerializeStringArray(ReadStringArray(root, "risks")),
                AssumptionsJson = SerializeStringArray(ReadStringArray(root, "assumptions")),
                MetadataJson = BuildMetadata(root, title, goal, items),
                Items = items
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ValidationException)
        {
            throw new ValidationException($"计划 JSON 解析失败：{ex.Message}", ErrorCodes.AiPlanParseFailed);
        }
    }

    public string BuildPlanPrompt() => """
你正在 AsterERP AI 工作台的 Plan 模式中工作。请把用户需求拆成可审阅、可批准、可执行、可恢复的复杂任务 todo 计划。
只输出一个 JSON 对象，不要输出 Markdown、代码围栏或额外解释。
JSON 结构必须为：
{
  "title": "计划标题",
  "goal": "最终目标",
  "overview": "一段实施方案概览，说明目标、边界和验收口径",
  "executionStrategy": "Serial|Parallel",
  "risks": ["风险1"],
  "assumptions": ["假设1"],
  "planMarkdown": "# 计划标题\n\n## 目标与边界\n...\n\n## 实施方案\n...\n\n## 验收与回归\n...",
  "items": [
    {
      "id": "稳定的本地任务 ID，例如 task-1",
      "title": "任务标题",
      "description": "可执行说明",
      "priority": "P0|P1|P2",
      "ownerType": "User|Agent|Tool",
      "taskType": "Design|Code|Test|Review|Tool|Manual",
      "dependsOn": ["task-1"],
      "acceptanceCriteria": ["可验证验收条件"],
      "toolCode": "仅 Tool 任务填写",
      "executionHint": "Agent/Tool 执行提示",
      "children": []
    }
  ]
}
要求：
1. items 必须从 planMarkdown 的实施步骤抽取，任务数量 3 到 20 个，最多 4 层。
2. 写操作、权限确认、外部密钥、生产发布、数据删除、导入导出、SQL、业务变更默认标记 ownerType=User；但系统管理工具域 system-admin 的新增、编辑、删除、启停、授权、发布/撤回、强退、任务触发可以生成 ownerType=Tool 的工具任务，前提是任务必须依赖用户已应用/批准的计划并进入 Agent 执行。
3. Agent/Tool 任务必须有 acceptanceCriteria；Tool 任务必须有 toolCode。
4. dependsOn 只能引用同一 JSON 中已声明的稳定本地任务 ID，不能形成环。
5. 当需求涉及 Workflow/审批流/流程设计/采购审批时，必须优先编排以下 Tool 任务链：
   workflow.model.search 或 workflow.binding.search 用于读取现状；
   workflow.model.createDraftFromText 用于根据需求生成 AI 草稿；
   workflow.bpmn.generateDraft 与 workflow.businessCanvas.generateDraft 用于生成 BPMN 和业务画布；
   workflow.binding.createDraft、workflow.formPermission.suggest、workflow.actionMap.suggest、workflow.notification.preview 用于生成绑定/表单权限/动作/通知建议；
   workflow.model.validateDraft 与 workflow.model.simulateDraft 用于校验和模拟；
   workflow.publish.precheck 只做发布前审查，不发布。
   禁止把 workflow.model.publish、workflow.model.activate、workflow.model.deactivate、workflow.binding.apply、workflow.task.approve、workflow.task.reject 放入可执行 Tool 任务；如用户要求这些动作，必须 ownerType=User。
6. 当需求涉及系统管理菜单（用户、部门、岗位、菜单、角色、字典、系统参数、通知公告、操作日志、登录日志、在线用户、任务调度）时，可以编排 system-admin 工具任务。优先使用以下工具码：
   用户：system.user.search、system.user.get、system.user.create、system.user.update、system.user.delete、system.user.batchStatus、system.user.grantRoles、system.user.resetPassword；
   角色：system.role.search、system.role.get、system.role.create、system.role.update、system.role.delete、system.role.batchStatus、system.role.grantMenus；
   菜单：system.menu.search、system.menu.tree、system.menu.get、system.menu.create、system.menu.update、system.menu.delete、system.menu.batchStatus；
   部门：system.department.search、system.department.tree、system.department.get、system.department.create、system.department.update、system.department.delete、system.department.batchStatus；
   岗位：system.position.search、system.position.get、system.position.create、system.position.update、system.position.delete、system.position.batchStatus；
   字典：system.dict.type.search、system.dict.type.get、system.dict.type.create、system.dict.type.update、system.dict.type.delete、system.dict.item.search、system.dict.item.get、system.dict.item.create、system.dict.item.update、system.dict.item.delete；
   参数：system.parameter.search、system.parameter.create、system.parameter.update、system.parameter.delete、system.parameter.batchStatus；
   公告：system.announcement.search、system.announcement.create、system.announcement.update、system.announcement.delete、system.announcement.publish、system.announcement.withdraw、system.announcement.top；
   日志/在线/调度：system.operationLog.search、system.operationLog.get、system.operationLog.recent、system.loginLog.search、system.onlineUser.search、system.onlineUser.forceLogout、system.scheduledJob.search、system.scheduledJob.get、system.scheduledJob.create、system.scheduledJob.update、system.scheduledJob.delete、system.scheduledJob.pause、system.scheduledJob.resume、system.scheduledJob.trigger、system.scheduledJob.logs、system.scheduledJob.summary、system.scheduledJob.types。
   Tool 任务的 executionHint 应尽量是 JSON，写入参数放入 request，查询必须设置 pageSize 且不超过 100；日志类不得生成新增、编辑、删除工具任务。
""";

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLine = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLine >= 0 && lastFence > firstLine)
            {
                trimmed = trimmed[(firstLine + 1)..lastFence].Trim();
            }
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new JsonException("未找到 JSON 对象");
        }

        return trimmed[start..(end + 1)];
    }

    private static void FlattenItems(JsonElement? source, string? parentId, int depth, int startOrder, List<AiTaskPlanItemUpsertRequest> result)
    {
        if (source is null || source.Value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var order = startOrder;
        foreach (var item in source.Value.EnumerateArray())
        {
            var localId = ReadString(item, "id") ?? $"task-{result.Count + 1}";
            var dependsOn = ReadStringArray(item, "dependsOn");
            var toolCode = ReadString(item, "toolCode") ?? ReadString(item, "tool");
            var ownerType = ReadString(item, "ownerType");
            var taskType = ReadString(item, "taskType");
            if (!string.IsNullOrWhiteSpace(toolCode))
            {
                ownerType = AiTaskPlanConstants.OwnerType.Tool;
                taskType = AiTaskPlanConstants.TaskType.Tool;
            }

            var title = ReadString(item, "title") ?? ReadString(item, "name") ?? "未命名任务";
            var description = ReadString(item, "description") ?? string.Empty;
            var acceptance = ReadStringArray(item, "acceptanceCriteria");
            if (acceptance.Count == 0 && !string.IsNullOrWhiteSpace(toolCode))
            {
                acceptance = [$"{toolCode} 调用成功并写入可恢复证据"];
            }

            result.Add(new AiTaskPlanItemUpsertRequest
            {
                Id = localId,
                ParentItemId = parentId,
                Title = title,
                Description = description,
                Status = AiTaskPlanConstants.ItemStatus.Pending,
                Priority = ReadString(item, "priority") ?? "P1",
                OwnerType = ownerType ?? AiTaskPlanConstants.OwnerType.Agent,
                TaskType = taskType ?? AiTaskPlanConstants.TaskType.Design,
                SortOrder = order,
                DependsOnJson = dependsOn.Count == 0 ? null : JsonSerializer.Serialize(dependsOn, JsonOptions),
                AcceptanceCriteriaJson = acceptance.Count == 0 ? null : JsonSerializer.Serialize(acceptance, JsonOptions),
                ToolCode = toolCode,
                ExecutionHint = BuildExecutionHint(item, description),
                MaxRetryCount = 3
            });

            FlattenItems(ReadArray(item, "children") ?? ReadArray(item, "items") ?? ReadArray(item, "tasks"), localId, depth + 1, order * 100, result);
            order++;
        }
    }

    private static string? BuildExecutionHint(JsonElement item, string description)
    {
        var executionHint = ReadString(item, "executionHint");
        var parameters = ReadObject(item, "parameters");
        if (parameters is null)
        {
            return executionHint;
        }

        if (!parameters.ContainsKey("requirementText") && !string.IsNullOrWhiteSpace(description))
        {
            parameters["requirementText"] = description;
        }

        if (!string.IsNullOrWhiteSpace(executionHint))
        {
            parameters["executionHint"] = executionHint;
        }

        return JsonSerializer.Serialize(parameters, JsonOptions);
    }

    private static string BuildMetadata(JsonElement root, string title, string goal, IReadOnlyList<AiTaskPlanItemUpsertRequest> items)
    {
        var overview = ReadString(root, "overview");
        var markdown = ReadString(root, "planMarkdown");
        return JsonSerializer.Serialize(new
        {
            overview,
            planMarkdown = string.IsNullOrWhiteSpace(markdown) ? BuildFallbackMarkdown(title, goal, overview, items) : markdown
        }, JsonOptions);
    }

    private static string BuildFallbackMarkdown(string title, string goal, string? overview, IReadOnlyList<AiTaskPlanItemUpsertRequest> items)
    {
        var builder = new StringBuilder();
        builder.Append("# ").AppendLine(title).AppendLine();
        builder.AppendLine("## 目标与边界");
        builder.AppendLine(goal);
        if (!string.IsNullOrWhiteSpace(overview))
        {
            builder.AppendLine().AppendLine(overview);
        }

        builder.AppendLine().AppendLine("## 实施步骤");
        foreach (var item in items.OrderBy(item => item.SortOrder))
        {
            builder.Append("- ").Append(item.Title).Append(" [").Append(item.OwnerType).Append(']').AppendLine();
        }

        builder.AppendLine().AppendLine("## 验收与回归");
        builder.AppendLine("按任务验收标准逐项确认状态、输出、事件和刷新恢复。");
        return builder.ToString().Trim();
    }

    private static JsonElement? ReadArray(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array ? value : null;

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? AiTaskPlanValueNormalizer.Optional(value.GetString()) : value.GetRawText();
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToList();
    }

    private static Dictionary<string, object?>? ReadObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(value.GetRawText(), JsonOptions)
               ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? SerializeStringArray(IReadOnlyList<string> values) =>
        values.Count == 0 ? null : JsonSerializer.Serialize(values, JsonOptions);
}
