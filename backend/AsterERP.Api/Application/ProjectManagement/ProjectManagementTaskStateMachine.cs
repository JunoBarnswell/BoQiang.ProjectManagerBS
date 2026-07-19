using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>任务状态、进度和实际日期的唯一转换入口；批量命令也应复用此规则。</summary>
public sealed class ProjectManagementTaskStateMachine
{
    public ProjectManagementTaskStateTransition Resolve(string currentStatus, string requestedStatus, decimal progressPercent, DateTime? actualStartAt, DateTime? actualEndAt, DateTime now, bool isNew = false)
    {
        var requested = ProjectManagementDomainRules.RequireTaskStatus(requestedStatus);
        var progress = ProjectManagementDomainRules.RequireProgress(progressPercent, "任务");
        var next = progress == 100m ? ProjectManagementDomainRules.TaskDone :
            currentStatus == ProjectManagementDomainRules.TaskDone && requestedStatus == ProjectManagementDomainRules.TaskDone && progress < 100m
                ? ProjectManagementDomainRules.TaskInProgress
                : requested;
        if (!isNew) ProjectManagementDomainRules.EnsureTaskStatusTransition(currentStatus, next);
        var normalizedProgress = next == ProjectManagementDomainRules.TaskDone ? 100m : progress;
        return new ProjectManagementTaskStateTransition(next, normalizedProgress,
            next is ProjectManagementDomainRules.TaskInProgress or ProjectManagementDomainRules.TaskDone ? actualStartAt ?? now : actualStartAt,
            next == ProjectManagementDomainRules.TaskDone ? actualEndAt ?? now : actualEndAt);
    }
}

public sealed record ProjectManagementTaskStateTransition(string Status, decimal ProgressPercent, DateTime? ActualStartAt, DateTime? ActualEndAt);
