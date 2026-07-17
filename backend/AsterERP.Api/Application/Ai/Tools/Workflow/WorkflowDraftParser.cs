using System.Text.RegularExpressions;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowDraftParser
{
    public AiWorkflowDraftDto Parse(string requirementText, string? businessType)
    {
        var normalizedText = string.IsNullOrWhiteSpace(requirementText)
            ? "AI 工作流审批"
            : requirementText.Trim();
        var isPurchase = normalizedText.Contains("采购", StringComparison.OrdinalIgnoreCase) ||
                         normalizedText.Contains("purchase", StringComparison.OrdinalIgnoreCase);
        var threshold = ExtractThreshold(normalizedText) ?? 10000m;
        var type = string.IsNullOrWhiteSpace(businessType)
            ? isPurchase ? "purchase.order" : "workflow.ai"
            : businessType.Trim();
        if (isPurchase)
        {
            return BuildPurchaseApprovalDraft(type, threshold, normalizedText);
        }

        return new AiWorkflowDraftDto
        {
            WorkflowKey = $"ai_{Slug(type)}_approval",
            WorkflowName = BuildWorkflowName(normalizedText),
            BusinessType = type,
            Nodes =
            [
                new() { Id = "start", Name = "提交申请", Type = "startEvent", PositionX = 80, PositionY = 160 },
                new() { Id = "dept_approve", Name = "部门负责人审批", Type = "userTask", CandidateRoles = ["dept.manager"], PositionX = 280, PositionY = 160 },
                new() { Id = "finance_approve", Name = "财务审批", Type = "userTask", CandidateRoles = ["finance.manager"], PositionX = 520, PositionY = 80 },
                new() { Id = "end", Name = "审批完成", Type = "endEvent", PositionX = 760, PositionY = 160 }
            ],
            Edges =
            [
                new() { Id = "flow_start_dept", SourceId = "start", TargetId = "dept_approve", Name = "提交" },
                new() { Id = "flow_dept_finance", SourceId = "dept_approve", TargetId = "finance_approve", Name = $"金额 >= {threshold:0}", Condition = $"amount >= {threshold:0}" },
                new() { Id = "flow_dept_end", SourceId = "dept_approve", TargetId = "end", Name = $"金额 < {threshold:0}", Condition = $"amount < {threshold:0}" },
                new() { Id = "flow_finance_end", SourceId = "finance_approve", TargetId = "end", Name = "通过" }
            ],
            Variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = threshold,
                ["businessType"] = type,
                ["starterUserId"] = "{{currentUserId}}"
            }
        };
    }

    private static AiWorkflowDraftDto BuildPurchaseApprovalDraft(string businessType, decimal threshold, string requirementText)
    {
        var highAmount = ExtractHighThreshold(requirementText, threshold) ?? Math.Max(threshold * 5, 50000m);
        var executiveAmount = ExtractExecutiveThreshold(requirementText, highAmount) ?? Math.Max(highAmount * 2, 100000m);
        return new AiWorkflowDraftDto
        {
            WorkflowKey = "ai_purchase_order_approval",
            WorkflowName = "复杂采购订单审批",
            BusinessType = businessType,
            Nodes =
            [
                new() { Id = "start", Name = "提交采购申请", Type = "startEvent", PositionX = 60, PositionY = 220 },
                new() { Id = "purchase_review", Name = "采购复核", Type = "userTask", CandidateRoles = ["purchase.specialist"], PositionX = 240, PositionY = 220 },
                new() { Id = "dept_approve", Name = "部门负责人审批", Type = "userTask", CandidateRoles = ["dept.manager"], PositionX = 440, PositionY = 220 },
                new() { Id = "budget_check", Name = "预算校验", Type = "userTask", CandidateRoles = ["budget.owner"], PositionX = 640, PositionY = 120 },
                new() { Id = "legal_review", Name = "法务会签", Type = "userTask", CandidateRoles = ["legal.counsel"], PositionX = 840, PositionY = 60 },
                new() { Id = "finance_approve", Name = "财务审批", Type = "userTask", CandidateRoles = ["finance.manager"], PositionX = 1040, PositionY = 160 },
                new() { Id = "finance_director", Name = "财务总监审批", Type = "userTask", CandidateRoles = ["finance.director"], PositionX = 1240, PositionY = 100 },
                new() { Id = "general_manager", Name = "总经理审批", Type = "userTask", CandidateRoles = ["general.manager"], PositionX = 1440, PositionY = 80 },
                new() { Id = "end", Name = "审批完成", Type = "endEvent", PositionX = 1640, PositionY = 220 }
            ],
            Edges =
            [
                new() { Id = "flow_start_purchase", SourceId = "start", TargetId = "purchase_review", Name = "提交" },
                new() { Id = "flow_purchase_dept", SourceId = "purchase_review", TargetId = "dept_approve", Name = "复核通过" },
                new() { Id = "flow_dept_direct_end", SourceId = "dept_approve", TargetId = "end", Name = $"金额 < {threshold:0}", Condition = $"amount < {threshold:0}" },
                new() { Id = "flow_dept_budget", SourceId = "dept_approve", TargetId = "budget_check", Name = $"金额 >= {threshold:0}", Condition = $"amount >= {threshold:0}" },
                new() { Id = "flow_budget_legal", SourceId = "budget_check", TargetId = "legal_review", Name = "合同需法务", Condition = "contractRequired == true" },
                new() { Id = "flow_budget_finance", SourceId = "budget_check", TargetId = "finance_approve", Name = "无需法务", Condition = "contractRequired != true" },
                new() { Id = "flow_legal_finance", SourceId = "legal_review", TargetId = "finance_approve", Name = "法务通过" },
                new() { Id = "flow_finance_end", SourceId = "finance_approve", TargetId = "end", Name = $"金额 < {highAmount:0}", Condition = $"amount < {highAmount:0}" },
                new() { Id = "flow_finance_director", SourceId = "finance_approve", TargetId = "finance_director", Name = $"金额 >= {highAmount:0}", Condition = $"amount >= {highAmount:0}" },
                new() { Id = "flow_director_end", SourceId = "finance_director", TargetId = "end", Name = $"金额 < {executiveAmount:0}", Condition = $"amount < {executiveAmount:0}" },
                new() { Id = "flow_director_gm", SourceId = "finance_director", TargetId = "general_manager", Name = $"金额 >= {executiveAmount:0}", Condition = $"amount >= {executiveAmount:0}" },
                new() { Id = "flow_gm_end", SourceId = "general_manager", TargetId = "end", Name = "总经理通过" }
            ],
            Variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = executiveAmount,
                ["businessType"] = businessType,
                ["contractRequired"] = true,
                ["starterUserId"] = "{{currentUserId}}"
            }
        };
    }

    private static decimal? ExtractThreshold(string text)
    {
        var match = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*(?:元|￥|RMB|CNY)?", RegexOptions.IgnoreCase);
        return match.Success && decimal.TryParse(match.Groups[1].Value, out var value) ? value : null;
    }

    private static decimal? ExtractHighThreshold(string text, decimal baseThreshold)
    {
        var values = ExtractNumbers(text).Where(item => item > baseThreshold).OrderBy(item => item).ToList();
        return values.Count == 0 ? null : values[0];
    }

    private static decimal? ExtractExecutiveThreshold(string text, decimal highThreshold)
    {
        var values = ExtractNumbers(text).Where(item => item > highThreshold).OrderBy(item => item).ToList();
        return values.Count == 0 ? null : values[0];
    }

    private static IEnumerable<decimal> ExtractNumbers(string text)
    {
        foreach (Match match in Regex.Matches(text, @"(\d+(?:\.\d+)?)\s*(?:元|￥|RMB|CNY)?", RegexOptions.IgnoreCase))
        {
            if (decimal.TryParse(match.Groups[1].Value, out var value))
            {
                yield return value;
            }
        }
    }

    private static string BuildWorkflowName(string text)
    {
        var title = text.Length <= 18 ? text : text[..18];
        return $"{title}审批";
    }

    private static string Slug(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
}
