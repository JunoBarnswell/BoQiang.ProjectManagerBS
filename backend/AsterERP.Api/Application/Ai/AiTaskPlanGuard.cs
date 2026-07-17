using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai;

public sealed class AiTaskPlanGuard
{
    private static readonly HashSet<string> StructureReadonlyPlanStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        AiTaskPlanConstants.PlanStatus.Approved,
        AiTaskPlanConstants.PlanStatus.Running,
        AiTaskPlanConstants.PlanStatus.Paused,
        AiTaskPlanConstants.PlanStatus.Completed,
        AiTaskPlanConstants.PlanStatus.PartialCompleted,
        AiTaskPlanConstants.PlanStatus.Blocked,
        AiTaskPlanConstants.PlanStatus.Cancelled,
        AiTaskPlanConstants.PlanStatus.Archived
    };

    public void EnsureStructureEditable(string status)
    {
        if (StructureReadonlyPlanStatuses.Contains(status))
        {
            throw new ValidationException("当前计划状态禁止结构编辑", ErrorCodes.AiPlanRunningReadonly);
        }
    }

    public void EnsureCanApprove(string status)
    {
        if (status is not (AiTaskPlanConstants.PlanStatus.Draft or AiTaskPlanConstants.PlanStatus.PlanReady))
        {
            throw new ValidationException("只有草稿或待批准计划可以批准", ErrorCodes.AiTaskInvalidStatusTransition);
        }
    }

    public void EnsureCanUnapprove(string status)
    {
        if (status != AiTaskPlanConstants.PlanStatus.Approved)
        {
            throw new ValidationException("只有已批准计划可以撤回批准", ErrorCodes.AiTaskInvalidStatusTransition);
        }
    }

    public void EnsureCanExecute(string status)
    {
        if (status is not (AiTaskPlanConstants.PlanStatus.Approved or AiTaskPlanConstants.PlanStatus.PartialCompleted))
        {
            throw new ValidationException("计划必须批准后才能执行", ErrorCodes.AiPlanNotApproved);
        }
    }

    public void EnsureCanPause(string status)
    {
        if (status != AiTaskPlanConstants.PlanStatus.Running)
        {
            throw new ValidationException("只有运行中的计划可以暂停", ErrorCodes.AiTaskInvalidStatusTransition);
        }
    }

    public void EnsureCanResume(string status)
    {
        if (status is not (AiTaskPlanConstants.PlanStatus.Paused or AiTaskPlanConstants.PlanStatus.Blocked))
        {
            throw new ValidationException("只有暂停或阻断中的计划可以恢复", ErrorCodes.AiTaskInvalidStatusTransition);
        }
    }

    public void EnsureCanCancel(string status)
    {
        if (status is AiTaskPlanConstants.PlanStatus.Completed or AiTaskPlanConstants.PlanStatus.Cancelled or AiTaskPlanConstants.PlanStatus.Archived)
        {
            throw new ValidationException("当前计划不能取消", ErrorCodes.AiTaskInvalidStatusTransition);
        }
    }

    public void EnsureCanRetry(string status, int retryCount, int maxRetryCount)
    {
        if (status is not (AiTaskPlanConstants.ItemStatus.Failed or AiTaskPlanConstants.ItemStatus.Blocked))
        {
            throw new ValidationException("只有失败或阻塞任务可以重试", ErrorCodes.AiTaskInvalidStatusTransition);
        }

        if (retryCount >= maxRetryCount)
        {
            throw new ValidationException("任务已达到最大重试次数", ErrorCodes.AiTaskInvalidStatusTransition);
        }
    }
}
