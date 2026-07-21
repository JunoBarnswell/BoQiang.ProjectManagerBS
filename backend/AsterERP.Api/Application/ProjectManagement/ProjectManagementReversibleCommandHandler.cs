using System.Text.Json;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 仅将账本载荷重新送回既有业务服务；权限、状态机、成员约束、数据过滤和版本检查均由原服务执行。
/// </summary>
public sealed class ProjectManagementReversibleCommandHandler(
    IProjectManagementTaskService taskService,
    IProjectManagementProjectService projectService,
    IProjectManagementTaskParticipantService participantService,
    IProjectManagementLabelService labelService,
    IProjectManagementRecycleService recycleService) : IProjectManagementReversibleCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool CanHandle(string commandType) => ProjectManagementReversibleCommandTypes.Supported.Contains(commandType);

    public Task<ProjectManagementReversibleCommandReplayResult> ReplayAsync(ProjectManagementReversibleCommandReplayRequest request, CancellationToken cancellationToken = default) => request.CommandType switch
    {
        ProjectManagementReversibleCommandTypes.TaskUpdated or
        ProjectManagementReversibleCommandTypes.TaskStatusProgressChanged or
        ProjectManagementReversibleCommandTypes.TaskAssigneeChanged => ReplayTaskUpdateAsync(request, cancellationToken),
        ProjectManagementReversibleCommandTypes.TaskParticipantChanged => ReplayParticipantAsync(request, cancellationToken),
        ProjectManagementReversibleCommandTypes.TaskLabelsChanged => ReplayLabelsAsync(request, cancellationToken),
        ProjectManagementReversibleCommandTypes.TaskSoftDeleted => request.Direction == ProjectManagementReversibleCommandDirections.Undo
            ? ReplayTaskRestoreForDeleteAsync(request, cancellationToken) : ReplayTaskDeleteForDeleteAsync(request, cancellationToken),
        ProjectManagementReversibleCommandTypes.TaskRestored => request.Direction == ProjectManagementReversibleCommandDirections.Undo
            ? ReplayTaskDeleteForRestoreAsync(request, cancellationToken) : ReplayTaskRestoreForRestoreAsync(request, cancellationToken),
        ProjectManagementReversibleCommandTypes.ProjectUpdated => ReplayProjectUpdateAsync(request, cancellationToken),
        ProjectManagementReversibleCommandTypes.ProjectSoftDeleted => request.Direction == ProjectManagementReversibleCommandDirections.Undo
            ? ReplayProjectRestoreForDeleteAsync(request, cancellationToken) : ReplayProjectDeleteForDeleteAsync(request, cancellationToken),
        ProjectManagementReversibleCommandTypes.ProjectRestored => request.Direction == ProjectManagementReversibleCommandDirections.Undo
            ? ReplayProjectDeleteForRestoreAsync(request, cancellationToken) : ReplayProjectRestoreForRestoreAsync(request, cancellationToken),
        _ => throw new ValidationException("该业务操作不可撤销")
    };

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayTaskUpdateAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementTaskUpdateCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementTaskUpdateCommand>(replay.ForwardCommandJson);
        var before = await taskService.GetAsync(selected.TaskId, cancellationToken);
        var after = await taskService.UpdateAsync(selected.TaskId, selected.Request, cancellationToken);
        return Result(replay, after.VersionNo,
            Serialize(forward with { Request = forward.Request with { VersionNo = after.VersionNo } }),
            Serialize(new ProjectManagementTaskUpdateCommand(selected.TaskId, ToUpsert(before) with { VersionNo = after.VersionNo })));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayParticipantAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementTaskParticipantCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementTaskParticipantCommand>(replay.ForwardCommandJson);
        var beforeTask = await taskService.GetAsync(selected.TaskId, cancellationToken);
        ProjectManagementTaskParticipantCommand inverse;
        if (selected.Operation == ProjectManagementTaskParticipantOperations.Add)
        {
            var added = await participantService.AddAsync(selected.TaskId, selected.AddRequest ?? throw new ValidationException("参与人新增命令缺少请求"), cancellationToken);
            inverse = new ProjectManagementTaskParticipantCommand(selected.TaskId, ProjectManagementTaskParticipantOperations.Remove, null, added.Id, 0);
        }
        else
        {
            var existing = (await participantService.QueryAsync(selected.TaskId, cancellationToken))
                .FirstOrDefault(item => item.Id == selected.ParticipantId)
                ?? throw new ValidationException("参与人已被其他用户修改，请刷新后重试");
            await participantService.RemoveAsync(selected.TaskId, existing.Id, selected.VersionNo, cancellationToken);
            inverse = new ProjectManagementTaskParticipantCommand(selected.TaskId, ProjectManagementTaskParticipantOperations.Add,
                new ProjectManagementTaskParticipantUpsertRequest(existing.UserId, existing.EmploymentId, existing.RoleCode), null, 0);
        }
        var afterTask = await taskService.GetAsync(selected.TaskId, cancellationToken);
        var nextForward = forward with { VersionNo = afterTask.VersionNo, AddRequest = forward.AddRequest is null ? null : forward.AddRequest with { VersionNo = afterTask.VersionNo } };
        inverse = inverse with { VersionNo = afterTask.VersionNo, AddRequest = inverse.AddRequest is null ? null : inverse.AddRequest with { VersionNo = afterTask.VersionNo } };
        return Result(replay, afterTask.VersionNo, Serialize(nextForward), Serialize(inverse));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayLabelsAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementTaskLabelsCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementTaskLabelsCommand>(replay.ForwardCommandJson);
        var before = await labelService.QueryTaskLabelsAsync(selected.TaskId, cancellationToken);
        await labelService.SetTaskLabelsAsync(selected.TaskId, new ProjectManagementTaskLabelSetRequest(selected.LabelIds, selected.VersionNo), cancellationToken);
        var after = await taskService.GetAsync(selected.TaskId, cancellationToken);
        return Result(replay, after.VersionNo,
            Serialize(forward with { VersionNo = after.VersionNo }),
            Serialize(new ProjectManagementTaskLabelsCommand(selected.TaskId, before.Select(item => item.LabelId).ToList(), after.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayTaskDeleteForDeleteAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementTaskDeleteCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementTaskDeleteCommand>(replay.ForwardCommandJson);
        await taskService.DeleteAsync(selected.TaskId, new ProjectManagementTaskDeleteRequest(selected.VersionNo, selected.Mode), cancellationToken);
        var deleted = (await recycleService.QueryAsync(new ProjectManagementRecycleQuery(PageSize: 200, ProjectId: replay.ProjectId), cancellationToken)).Tasks.Items
            .FirstOrDefault(item => item.Id == selected.TaskId) ?? throw new ValidationException("删除任务后无法读取回收站版本");
        return Result(replay, deleted.VersionNo,
            Serialize(forward with { VersionNo = deleted.VersionNo }),
            Serialize(new ProjectManagementTaskRestoreCommand(selected.TaskId, selected.Mode == ProjectManagementTaskDeleteModes.Cascade, deleted.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayTaskRestoreForDeleteAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementTaskRestoreCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementTaskDeleteCommand>(replay.ForwardCommandJson);
        await recycleService.RestoreTaskAsync(selected.TaskId, new ProjectManagementRecycleRestoreRequest(selected.VersionNo, selected.RestoreDescendants), cancellationToken);
        var restored = await taskService.GetAsync(selected.TaskId, cancellationToken);
        return Result(replay, restored.VersionNo,
            Serialize(forward with { VersionNo = restored.VersionNo }),
            Serialize(new ProjectManagementTaskRestoreCommand(selected.TaskId, selected.RestoreDescendants, restored.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayTaskDeleteForRestoreAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementTaskDeleteCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementTaskRestoreCommand>(replay.ForwardCommandJson);
        await taskService.DeleteAsync(selected.TaskId, new ProjectManagementTaskDeleteRequest(selected.VersionNo, selected.Mode), cancellationToken);
        var deleted = (await recycleService.QueryAsync(new ProjectManagementRecycleQuery(PageSize: 200, ProjectId: replay.ProjectId), cancellationToken)).Tasks.Items
            .FirstOrDefault(item => item.Id == selected.TaskId) ?? throw new ValidationException("删除任务后无法读取回收站版本");
        return Result(replay, deleted.VersionNo,
            Serialize(forward with { VersionNo = deleted.VersionNo }),
            Serialize(new ProjectManagementTaskDeleteCommand(selected.TaskId, selected.Mode, deleted.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayTaskRestoreForRestoreAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementTaskRestoreCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementTaskRestoreCommand>(replay.ForwardCommandJson);
        await recycleService.RestoreTaskAsync(selected.TaskId, new ProjectManagementRecycleRestoreRequest(selected.VersionNo, selected.RestoreDescendants), cancellationToken);
        var restored = await taskService.GetAsync(selected.TaskId, cancellationToken);
        return Result(replay, restored.VersionNo,
            Serialize(forward with { VersionNo = restored.VersionNo }),
            Serialize(new ProjectManagementTaskDeleteCommand(selected.TaskId, ProjectManagementTaskDeleteModes.Cascade, restored.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayProjectUpdateAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementProjectUpdateCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementProjectUpdateCommand>(replay.ForwardCommandJson);
        var before = await projectService.QueryAsync(new ProjectManagementProjectQuery(PageSize: 100, Keyword: null), cancellationToken);
        var previous = before.Items.FirstOrDefault(item => item.Id == selected.ProjectId) ?? throw new ValidationException("项目已被其他用户修改，请刷新后重试");
        var after = await projectService.UpdateAsync(selected.ProjectId, selected.Request, cancellationToken);
        return Result(replay, after.VersionNo,
            Serialize(forward with { Request = forward.Request with { VersionNo = after.VersionNo } }),
            Serialize(new ProjectManagementProjectUpdateCommand(selected.ProjectId, ToUpsert(previous) with { VersionNo = after.VersionNo })));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayProjectDeleteForDeleteAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementProjectDeleteCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementProjectDeleteCommand>(replay.ForwardCommandJson);
        await projectService.DeleteAsync(selected.ProjectId, selected.VersionNo, cancellationToken: cancellationToken);
        var deleted = (await recycleService.QueryAsync(new ProjectManagementRecycleQuery(PageSize: 200), cancellationToken)).Projects.Items
            .FirstOrDefault(item => item.Id == selected.ProjectId) ?? throw new ValidationException("删除项目后无法读取回收站版本");
        return Result(replay, deleted.VersionNo,
            Serialize(forward with { VersionNo = deleted.VersionNo }),
            Serialize(new ProjectManagementProjectRestoreCommand(selected.ProjectId, deleted.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayProjectRestoreForDeleteAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementProjectRestoreCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementProjectDeleteCommand>(replay.ForwardCommandJson);
        await recycleService.RestoreProjectAsync(selected.ProjectId, new ProjectManagementRecycleRestoreRequest(selected.VersionNo), cancellationToken);
        var restored = await projectService.QueryAsync(new ProjectManagementProjectQuery(PageSize: 100), cancellationToken);
        var project = restored.Items.FirstOrDefault(item => item.Id == selected.ProjectId) ?? throw new ValidationException("项目恢复后无法读取版本");
        return Result(replay, project.VersionNo,
            Serialize(forward with { VersionNo = project.VersionNo }),
            Serialize(new ProjectManagementProjectRestoreCommand(selected.ProjectId, project.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayProjectDeleteForRestoreAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementProjectDeleteCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementProjectRestoreCommand>(replay.ForwardCommandJson);
        await projectService.DeleteAsync(selected.ProjectId, selected.VersionNo, cancellationToken: cancellationToken);
        var deleted = (await recycleService.QueryAsync(new ProjectManagementRecycleQuery(PageSize: 200), cancellationToken)).Projects.Items
            .FirstOrDefault(item => item.Id == selected.ProjectId) ?? throw new ValidationException("删除项目后无法读取回收站版本");
        return Result(replay, deleted.VersionNo,
            Serialize(forward with { VersionNo = deleted.VersionNo }),
            Serialize(new ProjectManagementProjectDeleteCommand(selected.ProjectId, deleted.VersionNo)));
    }

    private async Task<ProjectManagementReversibleCommandReplayResult> ReplayProjectRestoreForRestoreAsync(ProjectManagementReversibleCommandReplayRequest replay, CancellationToken cancellationToken)
    {
        var selected = Deserialize<ProjectManagementProjectRestoreCommand>(replay.CommandJson);
        var forward = Deserialize<ProjectManagementProjectRestoreCommand>(replay.ForwardCommandJson);
        await recycleService.RestoreProjectAsync(selected.ProjectId, new ProjectManagementRecycleRestoreRequest(selected.VersionNo), cancellationToken);
        var restored = await projectService.QueryAsync(new ProjectManagementProjectQuery(PageSize: 100), cancellationToken);
        var project = restored.Items.FirstOrDefault(item => item.Id == selected.ProjectId) ?? throw new ValidationException("项目恢复后无法读取版本");
        return Result(replay, project.VersionNo,
            Serialize(forward with { VersionNo = project.VersionNo }),
            Serialize(new ProjectManagementProjectDeleteCommand(selected.ProjectId, project.VersionNo)));
    }

    private static ProjectManagementReversibleCommandReplayResult Result(ProjectManagementReversibleCommandReplayRequest replay, long versionNo, string forward, string inverse) =>
        new(replay.ProjectId, replay.AggregateType, replay.AggregateId, versionNo, NextForwardCommandJson: forward, NextInverseCommandJson: inverse);

    internal static ProjectManagementTaskUpsertRequest ToUpsert(ProjectManagementTaskDetailResponse task) => new(
        task.TaskCode,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.MilestoneId,
        task.ParentTaskId,
        task.AssigneeUserId,
        task.AssigneeEmploymentId,
        task.StartDate,
        task.DueDate,
        task.ProgressPercent,
        task.Weight,
        task.VersionNo,
        Markdown: task.Markdown,
        Summary: task.Summary,
        WorkItemType: task.WorkItemType,
        ContentJson: task.ContentJson,
        ContentText: task.ContentText,
        RiskLevel: task.RiskLevel,
        RequirementType: task.RequirementType,
        RequirementSource: task.RequirementSource,
        StoryPoints: task.StoryPoints,
        MentionUserIds: task.MentionUserIds,
        FollowerUserIds: task.FollowerUserIds);
    internal static ProjectManagementProjectUpsertRequest ToUpsert(ProjectManagementProjectResponse project) => new(project.ProjectCode, project.ProjectName, project.Description, project.Status, project.Priority, project.OwnerUserId, project.StartDate, project.DueDate, project.WipLimit, project.ProgressPercent, project.VersionNo);
    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new ValidationException("可逆命令载荷无效");
}

internal static class ProjectManagementTaskParticipantOperations
{
    public const string Add = "Add";
    public const string Remove = "Remove";
}

internal sealed record ProjectManagementTaskUpdateCommand(string TaskId, ProjectManagementTaskUpsertRequest Request);
internal sealed record ProjectManagementTaskParticipantCommand(string TaskId, string Operation, ProjectManagementTaskParticipantUpsertRequest? AddRequest, string? ParticipantId, long VersionNo);
internal sealed record ProjectManagementTaskLabelsCommand(string TaskId, IReadOnlyList<string> LabelIds, long VersionNo);
internal sealed record ProjectManagementTaskDeleteCommand(string TaskId, string Mode, long VersionNo);
internal sealed record ProjectManagementTaskRestoreCommand(string TaskId, bool RestoreDescendants, long VersionNo);
internal sealed record ProjectManagementProjectUpdateCommand(string ProjectId, ProjectManagementProjectUpsertRequest Request);
internal sealed record ProjectManagementProjectDeleteCommand(string ProjectId, long VersionNo);
internal sealed record ProjectManagementProjectRestoreCommand(string ProjectId, long VersionNo);
