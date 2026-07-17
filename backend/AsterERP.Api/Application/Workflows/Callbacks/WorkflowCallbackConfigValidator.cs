using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Workflows.Callbacks;

public sealed class WorkflowCallbackConfigValidator(IRuntimeDataModelService runtimeDataModelService)
{
    private const int MaxRules = 20;
    private const int MaxAssignmentsPerRule = 10;

    private static readonly HashSet<string> AllowedTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        WorkflowCallbackTriggers.ProcessStart,
        WorkflowCallbackTriggers.NodeEnter,
        WorkflowCallbackTriggers.TaskComplete,
        WorkflowCallbackTriggers.TaskReject,
        WorkflowCallbackTriggers.TaskReturn,
        WorkflowCallbackTriggers.ProcessCompleted,
        WorkflowCallbackTriggers.ProcessWithdrawn,
        WorkflowCallbackTriggers.ProcessTerminated
    };

    private static readonly HashSet<string> AllowedTargetSources = new(StringComparer.OrdinalIgnoreCase)
    {
        WorkflowCallbackValueSources.BusinessKey,
        WorkflowCallbackValueSources.Context,
        WorkflowCallbackValueSources.Variable,
        WorkflowCallbackValueSources.SubmittedField
    };

    private static readonly HashSet<string> AllowedValueSources = new(StringComparer.OrdinalIgnoreCase)
    {
        WorkflowCallbackValueSources.Constant,
        WorkflowCallbackValueSources.Context,
        WorkflowCallbackValueSources.Variable,
        WorkflowCallbackValueSources.SubmittedField
    };

    private static readonly HashSet<string> AllowedContextKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenantId",
        "appCode",
        "menuCode",
        "businessType",
        "businessKey",
        "processInstanceId",
        "processDefinitionKey",
        "instanceStatus",
        "trigger",
        "nodeId",
        "workflowTaskId",
        "action",
        "currentUserId",
        "startedBy",
        "startedAt",
        "finishedAt"
    };

    public async Task ValidateAsync(
        WorkflowCallbackConfigDto? config,
        string? defaultModelCode,
        CancellationToken cancellationToken = default)
    {
        if (config is not null && !string.Equals(config.Version, "latest", StringComparison.Ordinal))
        {
            throw new ValidationException("Workflow callback configuration version must be latest.", ErrorCodes.ParameterInvalid);
        }

        var rules = config?.Rules ?? [];
        if (rules.Count > MaxRules)
        {
            throw new ValidationException($"审批回调规则最多允许 {MaxRules} 条", ErrorCodes.ParameterInvalid);
        }

        var duplicateAssignments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            ValidateRuleHeader(rule);
            var modelCode = ResolveTargetModelCode(rule, defaultModelCode);
            var definition = await runtimeDataModelService.GetPublishedDefinitionAsync(modelCode, cancellationToken);
            ValidateTarget(rule.Target);
            ValidateAssignments(rule, definition, duplicateAssignments);
        }
    }

    private static void ValidateRuleHeader(WorkflowCallbackRuleDto rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Trigger) || !AllowedTriggers.Contains(rule.Trigger.Trim()))
        {
            throw new ValidationException($"审批回调触发器不支持: {rule.Trigger}", ErrorCodes.ParameterInvalid);
        }

        var assignments = rule.Assignments ?? [];
        if (assignments.Count == 0)
        {
            throw new ValidationException("审批回调规则至少需要一个字段赋值", ErrorCodes.ParameterInvalid);
        }

        if (assignments.Count > MaxAssignmentsPerRule)
        {
            throw new ValidationException($"单条审批回调规则最多允许 {MaxAssignmentsPerRule} 个字段赋值", ErrorCodes.ParameterInvalid);
        }
    }

    private static string ResolveTargetModelCode(WorkflowCallbackRuleDto rule, string? defaultModelCode)
    {
        var modelCode = FirstNonEmpty(rule.Target?.ModelCode, defaultModelCode);
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            throw new ValidationException("审批回调目标 DataModel 不能为空", ErrorCodes.ParameterInvalid);
        }

        return modelCode;
    }

    private static void ValidateTarget(WorkflowCallbackTargetDto? target)
    {
        var source = FirstNonEmpty(target?.KeySource, WorkflowCallbackValueSources.BusinessKey)!;
        if (!AllowedTargetSources.Contains(source))
        {
            throw new ValidationException($"审批回调目标主键来源不支持: {source}", ErrorCodes.ParameterInvalid);
        }

        if (string.Equals(source, WorkflowCallbackValueSources.Context, StringComparison.OrdinalIgnoreCase))
        {
            ValidateContextKey(target?.KeyName, "审批回调目标主键");
        }
        else if (!string.Equals(source, WorkflowCallbackValueSources.BusinessKey, StringComparison.OrdinalIgnoreCase) &&
                 string.IsNullOrWhiteSpace(target?.KeyName))
        {
            throw new ValidationException("审批回调目标主键来源字段不能为空", ErrorCodes.ParameterInvalid);
        }
    }

    private static void ValidateAssignments(
        WorkflowCallbackRuleDto rule,
        RuntimeDataModelDefinition definition,
        HashSet<string> duplicateAssignments)
    {
        foreach (var assignment in rule.Assignments ?? [])
        {
            var field = ResolveWritableField(definition, assignment.FieldCode);
            var source = assignment.ValueSource.Trim();
            if (!AllowedValueSources.Contains(source))
            {
                throw new ValidationException($"审批回调赋值来源不支持: {source}", ErrorCodes.ParameterInvalid);
            }

            if (string.Equals(source, WorkflowCallbackValueSources.Constant, StringComparison.OrdinalIgnoreCase))
            {
                var coerced = RuntimeDataProviderSupport.CoerceValue(assignment.Value, field.DataType);
                if (assignment.Value is not null && coerced is null)
                {
                    throw new ValidationException($"字段 {field.FieldCode} 的常量值类型无效", ErrorCodes.RuntimeFieldNotAllowed);
                }
            }
            else if (string.Equals(source, WorkflowCallbackValueSources.Context, StringComparison.OrdinalIgnoreCase))
            {
                ValidateContextKey(assignment.ValueName, $"字段 {field.FieldCode}");
            }
            else if (string.IsNullOrWhiteSpace(assignment.ValueName))
            {
                throw new ValidationException($"字段 {field.FieldCode} 的来源字段不能为空", ErrorCodes.ParameterInvalid);
            }

            var duplicateKey = $"{rule.Trigger}|{rule.NodeId}|{definition.ModelCode}|{field.FieldCode}";
            if (!duplicateAssignments.Add(duplicateKey))
            {
                throw new ValidationException($"同一触发器下字段重复赋值: {field.FieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
            }
        }
    }

    private static RuntimeDataFieldDefinition ResolveWritableField(
        RuntimeDataModelDefinition definition,
        string fieldCode)
    {
        if (string.IsNullOrWhiteSpace(fieldCode))
        {
            throw new ValidationException("审批回调目标字段不能为空", ErrorCodes.ParameterInvalid);
        }

        var normalized = fieldCode.Trim();
        var field = definition.Fields.FirstOrDefault(item =>
            string.Equals(item.FieldCode, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Binding, normalized, StringComparison.OrdinalIgnoreCase));
        if (field is null || !field.Writable || string.IsNullOrWhiteSpace(field.Binding))
        {
            throw new ValidationException($"字段不允许更新: {normalized}", ErrorCodes.RuntimeFieldNotAllowed);
        }

        if (string.Equals(field.FieldCode, definition.KeyField, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Binding, definition.KeyField, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("主键字段不允许更新", ErrorCodes.RuntimeFieldNotAllowed);
        }

        return field;
    }

    private static void ValidateContextKey(string? key, string displayName)
    {
        if (string.IsNullOrWhiteSpace(key) || !AllowedContextKeys.Contains(key.Trim()))
        {
            throw new ValidationException($"{displayName} 的上下文字段不支持: {key}", ErrorCodes.ParameterInvalid);
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}
