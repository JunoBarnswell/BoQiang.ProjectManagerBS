using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Workflows;
using Microsoft.Extensions.Logging;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Api.Application.Workflows.Callbacks;

public sealed class WorkflowCallbackExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IRuntimeDataModelService runtimeDataModelService,
    WorkflowCallbackConfigParser configParser,
    WorkflowCallbackValueResolver valueResolver,
    ILogger<WorkflowCallbackExecutor> logger,
    IGuidGenerator guidGenerator,
    IClock clock)
{
    private const string SuccessStatus = "Success";
    private const string FailedStatus = "Failed";

    public async Task ExecuteAsync(
        WorkflowCallbackContext context,
        CancellationToken cancellationToken = default)
    {
        var binding = await LoadBindingAsync(context.Instance, cancellationToken);
        if (binding is null)
        {
            return;
        }

        var config = configParser.ResolveEffectiveConfig(binding);
        var rules = config?.Rules?
            .Where(rule => rule.Enabled && MatchesTrigger(rule, context))
            .OrderBy(rule => rule.SortOrder)
            .ToList() ?? [];
        foreach (var rule in rules)
        {
            await ExecuteRuleAsync(binding, rule, context, cancellationToken);
        }
    }

    public async Task LogFailureAsync(
        WorkflowCallbackExecutionException exception,
        CancellationToken cancellationToken = default)
    {
        try
        {
            exception.FailureLog.Id = guidGenerator.Create().ToString("N");
            exception.FailureLog.CreatedTime = clock.Now;
            await databaseAccessor.GetCurrentDb().Insertable(exception.FailureLog).ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception logException)
        {
            logger.LogError(
                logException,
                "Failed to persist workflow callback failure log. ProcessInstanceId={ProcessInstanceId}, RuleId={RuleId}",
                exception.FailureLog.ProcessInstanceId,
                exception.FailureLog.RuleId);
        }
    }

    private async Task ExecuteRuleAsync(
        WorkflowBindingEntity binding,
        WorkflowCallbackRuleDto rule,
        WorkflowCallbackContext context,
        CancellationToken cancellationToken)
    {
        var targetModelCode = FirstNonEmpty(rule.Target?.ModelCode, binding.ModelCode)
            ?? throw new InvalidOperationException("审批回调目标 DataModel 为空");
        var targetKey = valueResolver.ResolveTargetKey(rule.Target, context);
        var updates = valueResolver.ResolveAssignments(rule, context);
        var ruleId = ResolveRuleId(rule);

        try
        {
            var now = clock.Now;
            await runtimeDataModelService.UpdateFieldsAsync(
                targetModelCode,
                targetKey,
                updates,
                cancellationToken);
            await InsertLogAsync(
                BuildLog(context, ruleId, targetModelCode, targetKey, now, now, SuccessStatus, null),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not WorkflowCallbackExecutionException)
        {
            var now = clock.Now;
            var log = BuildLog(context, ruleId, targetModelCode, targetKey, now, now, FailedStatus, ex.Message);
            throw new WorkflowCallbackExecutionException(log, ex);
        }
    }

    private async Task<WorkflowBindingEntity?> LoadBindingAsync(
        WorkflowBusinessInstanceEntity instance,
        CancellationToken cancellationToken)
    {
        return await databaseAccessor.GetCurrentDb().Queryable<WorkflowBindingEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.IsEnabled &&
                item.TenantId == instance.TenantId &&
                item.AppCode == instance.AppCode &&
                item.MenuCode == instance.MenuCode &&
                item.BusinessType == instance.BusinessType)
            .OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    private static bool MatchesTrigger(
        WorkflowCallbackRuleDto rule,
        WorkflowCallbackContext context)
    {
        if (!string.Equals(rule.Trigger, context.Trigger, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(rule.NodeId) ||
               string.Equals(rule.NodeId.Trim(), context.NodeId, StringComparison.OrdinalIgnoreCase);
    }

    private Task InsertLogAsync(
        WorkflowCallbackLogEntity log,
        CancellationToken cancellationToken)
    {
        return databaseAccessor.GetCurrentDb().Insertable(log).ExecuteCommandAsync(cancellationToken);
    }

    private WorkflowCallbackLogEntity BuildLog(
        WorkflowCallbackContext context,
        string ruleId,
        string targetModelCode,
        string targetKey,
        DateTime executedAt,
        DateTime createdTime,
        string status,
        string? errorMessage)
    {
        return new WorkflowCallbackLogEntity
        {
            TenantId = context.Instance.TenantId,
            AppCode = context.Instance.AppCode,
            ProcessInstanceId = context.Instance.ProcessInstanceId,
            WorkflowTaskId = context.WorkflowTaskId,
            ProcessDefinitionKey = context.Instance.ProcessDefinitionKey,
            Trigger = context.Trigger,
            NodeId = context.NodeId,
            RuleId = ruleId,
            TargetModelCode = targetModelCode,
            TargetKey = targetKey,
            Status = status,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage,
            ExecutedAt = executedAt,
            CreatedTime = createdTime
        };
    }

    private static string ResolveRuleId(WorkflowCallbackRuleDto rule)
    {
        return string.IsNullOrWhiteSpace(rule.RuleId)
            ? $"{rule.Trigger}:{rule.SortOrder}"
            : rule.RuleId.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
}

