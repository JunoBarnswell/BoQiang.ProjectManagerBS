using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai;

public static class AiTaskPlanValueNormalizer
{
    public static string Required(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message, ErrorCodes.ParameterInvalid);
        }

        return value.Trim();
    }

    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string PlanStatus(string? value) => OneOf(
        value,
        AiTaskPlanConstants.PlanStatuses,
        "计划状态无效",
        AiTaskPlanConstants.PlanStatus.Draft);

    public static string ItemStatus(string? value) => OneOf(
        value,
        AiTaskPlanConstants.ItemStatuses,
        "任务状态无效",
        AiTaskPlanConstants.ItemStatus.Pending);

    public static string Priority(string? value) => OneOf(value, AiTaskPlanConstants.Priorities, "任务优先级无效", "P1");

    public static string OwnerType(string? value) => OneOf(
        value,
        AiTaskPlanConstants.OwnerTypes,
        "任务负责人类型无效",
        AiTaskPlanConstants.OwnerType.Agent);

    public static string TaskType(string? value) => OneOf(
        value,
        AiTaskPlanConstants.TaskTypes,
        "任务类型无效",
        AiTaskPlanConstants.TaskType.Design);

    public static string Mode(string? value)
    {
        if (string.Equals(value, "Agent", StringComparison.OrdinalIgnoreCase))
        {
            return "Agent";
        }

        return string.Equals(value, "Ask", StringComparison.OrdinalIgnoreCase) ? "Ask" : "Plan";
    }

    public static string ExecutionStrategy(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Serial" : value.Trim();
        return normalized.Equals("Parallel", StringComparison.OrdinalIgnoreCase) ? "Parallel" : "Serial";
    }

    private static string OneOf(string? value, IReadOnlyCollection<string> allowed, string message, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return allowed.FirstOrDefault(item => string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new ValidationException(message, ErrorCodes.ParameterInvalid);
    }
}
