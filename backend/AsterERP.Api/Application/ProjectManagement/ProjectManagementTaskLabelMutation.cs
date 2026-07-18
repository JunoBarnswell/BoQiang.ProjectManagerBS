using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 在调用方事务内替换任务标签。
/// 任务版本和领域事件由调用方统一维护，避免单任务与批量命令出现不同语义。
/// </summary>
public sealed class ProjectManagementTaskLabelMutation
{
    public async Task ReplaceAsync(
        ISqlSugarClient db,
        ProjectManagementTaskEntity task,
        IReadOnlyCollection<string> requestedLabelIds,
        string tenantId,
        string appCode,
        string actorUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var labelIds = requestedLabelIds
            .Where(labelId => !string.IsNullOrWhiteSpace(labelId))
            .Select(labelId => labelId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (labelIds.Count != requestedLabelIds.Count)
            throw new ValidationException("标签不能为空且不能重复");

        var allowed = labelIds.Count == 0
            ? []
            : await db.Queryable<ProjectManagementLabelEntity>()
                .Where(label => labelIds.Contains(label.Id) && !label.IsDeleted &&
                    (label.ProjectId == null || label.ProjectId == task.ProjectId))
                .ToListAsync(cancellationToken);
        if (allowed.Count != labelIds.Count)
            throw new ValidationException("存在不属于当前项目的标签");

        await db.Updateable<ProjectManagementTaskLabelEntity>()
            .SetColumns(item => new ProjectManagementTaskLabelEntity
            {
                IsDeleted = true,
                DeletedBy = actorUserId,
                DeletedTime = now,
                UpdatedBy = actorUserId,
                UpdatedTime = now
            })
            .Where(item => item.TaskId == task.Id && !item.IsDeleted)
            .ExecuteCommandAsync(cancellationToken);

        if (labelIds.Count == 0) return;
        await db.Insertable(labelIds.Select(labelId => new ProjectManagementTaskLabelEntity
        {
            TenantId = tenantId,
            AppCode = appCode,
            ProjectId = task.ProjectId,
            TaskId = task.Id,
            LabelId = labelId,
            VersionNo = 1,
            CreatedBy = actorUserId,
            CreatedTime = now
        }).ToList()).ExecuteCommandAsync(cancellationToken);
    }
}
