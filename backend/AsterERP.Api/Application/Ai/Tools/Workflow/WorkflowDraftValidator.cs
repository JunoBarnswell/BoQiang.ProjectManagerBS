using AsterERP.Api.Modules.System.Roles;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowDraftValidator(ISqlSugarClient db, WorkflowConditionEvaluator conditionEvaluator)
{
    public async Task<IReadOnlyList<AiWorkflowValidationIssueDto>> ValidateAsync(
        AiWorkflowDraftDto draft,
        AiKernelFunctionContext context,
        CancellationToken cancellationToken)
    {
        var issues = new List<AiWorkflowValidationIssueDto>();
        ValidateStructure(draft, issues);
        ValidateConditions(draft, issues);
        await ValidateRolesAsync(draft, context, issues, cancellationToken);
        return issues;
    }

    private static void ValidateStructure(AiWorkflowDraftDto draft, ICollection<AiWorkflowValidationIssueDto> issues)
    {
        if (draft.Nodes.Count(item => item.Type.Equals("startEvent", StringComparison.OrdinalIgnoreCase)) != 1)
        {
            issues.Add(Error("start", "流程必须且只能有一个开始节点", ErrorCodes.AiWorkflowNodeInvalid));
        }

        if (!draft.Nodes.Any(item => item.Type.Equals("endEvent", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Error("end", "流程至少需要一个结束节点", ErrorCodes.AiWorkflowNodeInvalid));
        }

        var duplicateNode = draft.Nodes.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicateNode is not null)
        {
            issues.Add(Error(duplicateNode.Key, "节点 ID 重复", ErrorCodes.AiWorkflowNodeInvalid));
        }

        var nodeIds = draft.Nodes.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in draft.Edges)
        {
            if (!nodeIds.Contains(edge.SourceId) || !nodeIds.Contains(edge.TargetId))
            {
                issues.Add(new AiWorkflowValidationIssueDto
                {
                    Severity = "Error",
                    ErrorCode = ErrorCodes.AiWorkflowNodeInvalid.ToString(),
                    Message = "连线引用了不存在的节点",
                    EdgeId = edge.Id,
                    Suggestion = "请修正 sourceId/targetId"
                });
            }
        }
    }

    private void ValidateConditions(AiWorkflowDraftDto draft, ICollection<AiWorkflowValidationIssueDto> issues)
    {
        foreach (var edge in draft.Edges.Where(item => !string.IsNullOrWhiteSpace(item.Condition)))
        {
            var validationMessage = conditionEvaluator.Validate(edge.Condition);
            if (validationMessage is not null)
            {
                issues.Add(new AiWorkflowValidationIssueDto
                {
                    Severity = "Error",
                    ErrorCode = ErrorCodes.AiWorkflowConditionInvalid.ToString(),
                    Message = validationMessage,
                    EdgeId = edge.Id,
                    Field = "condition",
                    Suggestion = "仅使用 amount >= 10000、status == 'Approved' 这类安全比较"
                });
            }
        }
    }

    private async Task ValidateRolesAsync(
        AiWorkflowDraftDto draft,
        AiKernelFunctionContext context,
        ICollection<AiWorkflowValidationIssueDto> issues,
        CancellationToken cancellationToken)
    {
        var roleCodes = draft.Nodes.SelectMany(item => item.CandidateRoles)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (roleCodes.Count == 0)
        {
            return;
        }

        var existing = await db.Queryable<SystemRoleEntity>()
            .Where(item => !item.IsDeleted &&
                           item.IsEnabled &&
                           roleCodes.Contains(item.RoleCode) &&
                           (item.TenantId == null || item.TenantId == context.TenantId) &&
                           (item.AppCode == null || item.AppCode == context.AppCode))
            .Select(item => item.RoleCode)
            .ToListAsync(cancellationToken);
        foreach (var roleCode in roleCodes.Except(existing, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new AiWorkflowValidationIssueDto
            {
                Severity = "Warning",
                ErrorCode = ErrorCodes.AiWorkflowPermissionDenied.ToString(),
                Message = $"候选角色 {roleCode} 当前未在系统角色中匹配到",
                Field = "candidateRoles",
                Suggestion = "导入前请确认角色编码或在系统角色中补齐"
            });
        }
    }

    private static AiWorkflowValidationIssueDto Error(string nodeId, string message, int errorCode) => new()
    {
        Severity = "Error",
        ErrorCode = errorCode.ToString(),
        Message = message,
        NodeId = nodeId,
        Suggestion = "请调整流程草稿结构后重新校验"
    };
}
