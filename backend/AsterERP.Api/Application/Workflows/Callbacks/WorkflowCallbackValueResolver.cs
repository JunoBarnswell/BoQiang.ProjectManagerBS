using AsterERP.Contracts.Workflows;

namespace AsterERP.Api.Application.Workflows.Callbacks;

public sealed class WorkflowCallbackValueResolver
{
    public string ResolveTargetKey(WorkflowCallbackTargetDto? target, WorkflowCallbackContext context)
    {
        var source = FirstNonEmpty(target?.KeySource, WorkflowCallbackValueSources.BusinessKey)!;
        var value = ResolveSourceValue(source, target?.KeyName, null, context);
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("审批回调目标主键解析为空");
        }

        return text;
    }

    public IReadOnlyDictionary<string, object?> ResolveAssignments(
        WorkflowCallbackRuleDto rule,
        WorkflowCallbackContext context)
    {
        var updates = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in rule.Assignments ?? [])
        {
            var value = ResolveSourceValue(
                assignment.ValueSource,
                assignment.ValueName,
                assignment.Value,
                context);
            updates[assignment.FieldCode] = value;
        }

        return updates;
    }

    private static object? ResolveSourceValue(
        string source,
        string? name,
        object? constantValue,
        WorkflowCallbackContext context)
    {
        var normalizedSource = source.Trim();
        if (string.Equals(normalizedSource, WorkflowCallbackValueSources.BusinessKey, StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.BusinessKey;
        }

        if (string.Equals(normalizedSource, WorkflowCallbackValueSources.Constant, StringComparison.OrdinalIgnoreCase))
        {
            return constantValue;
        }

        if (string.Equals(normalizedSource, WorkflowCallbackValueSources.Context, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveContextValue(name, context);
        }

        if (string.Equals(normalizedSource, WorkflowCallbackValueSources.Variable, StringComparison.OrdinalIgnoreCase))
        {
            return ReadNamedValue(context.Variables, name);
        }

        if (string.Equals(normalizedSource, WorkflowCallbackValueSources.SubmittedField, StringComparison.OrdinalIgnoreCase))
        {
            return ReadNamedValue(WorkflowJson.DeserializeVariables(context.Instance.SubmittedFormJson), name);
        }

        throw new InvalidOperationException($"审批回调值来源不支持: {source}");
    }

    private static object? ResolveContextValue(string? name, WorkflowCallbackContext context)
    {
        var normalizedName = name?.Trim();
        if (string.Equals(normalizedName, "tenantId", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.TenantId;
        }

        if (string.Equals(normalizedName, "appCode", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.AppCode;
        }

        if (string.Equals(normalizedName, "menuCode", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.MenuCode;
        }

        if (string.Equals(normalizedName, "businessType", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.BusinessType;
        }

        if (string.Equals(normalizedName, "businessKey", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.BusinessKey;
        }

        if (string.Equals(normalizedName, "processInstanceId", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.ProcessInstanceId;
        }

        if (string.Equals(normalizedName, "processDefinitionKey", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.ProcessDefinitionKey;
        }

        if (string.Equals(normalizedName, "instanceStatus", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.Status;
        }

        if (string.Equals(normalizedName, "trigger", StringComparison.OrdinalIgnoreCase))
        {
            return context.Trigger;
        }

        if (string.Equals(normalizedName, "nodeId", StringComparison.OrdinalIgnoreCase))
        {
            return context.NodeId;
        }

        if (string.Equals(normalizedName, "workflowTaskId", StringComparison.OrdinalIgnoreCase))
        {
            return context.WorkflowTaskId;
        }

        if (string.Equals(normalizedName, "action", StringComparison.OrdinalIgnoreCase))
        {
            return context.Action;
        }

        if (string.Equals(normalizedName, "currentUserId", StringComparison.OrdinalIgnoreCase))
        {
            return context.CurrentUserId;
        }

        if (string.Equals(normalizedName, "startedBy", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.StartedBy;
        }

        if (string.Equals(normalizedName, "startedAt", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.StartedAt;
        }

        if (string.Equals(normalizedName, "finishedAt", StringComparison.OrdinalIgnoreCase))
        {
            return context.Instance.FinishedAt;
        }

        return null;
    }

    private static object? ReadNamedValue(IReadOnlyDictionary<string, object?> values, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return values.TryGetValue(name.Trim(), out var value) ? value : null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
