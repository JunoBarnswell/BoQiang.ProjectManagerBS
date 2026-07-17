using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Service;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence;
using CoreAttachmentEntity = AsterERP.Workflow.Core.Cmd.AttachmentEntity;
using CoreCommentEntity = AsterERP.Workflow.Core.Cmd.CommentEntity;
using CoreIdentityLinkEntity = AsterERP.Workflow.Core.Cmd.IdentityLinkEntity;
using CoreTaskImplementation = AsterERP.Workflow.Core.Services.TaskImplementation;
using PersistentAttachmentEntity = AsterERP.Workflow.Persistence.Entities.AttachmentEntity;
using PersistentCommentEntity = AsterERP.Workflow.Persistence.Entities.CommentEntity;
using PersistentExecutionEntity = AsterERP.Workflow.Persistence.Entities.ExecutionEntity;
using PersistentHistoricActivityEntity = AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity;
using PersistentHistoricDetailEntity = AsterERP.Workflow.Persistence.Entities.HistoricDetailEntity;
using PersistentHistoricIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity;
using PersistentHistoricProcessEntity = AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity;
using PersistentHistoricTaskEntity = AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity;
using PersistentHistoricVariableEntity = AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity;
using PersistentIdentityLinkEntity = AsterERP.Workflow.Persistence.Entities.IdentityLinkEntity;
using PersistentTaskEntity = AsterERP.Workflow.Persistence.Entities.TaskEntity;
using PersistentVariableEntity = AsterERP.Workflow.Persistence.Entities.VariableInstanceEntity;

namespace AsterERP.Workflow.DependencyInjection.Persistence;

internal static class SqlSugarWorkflowPersistenceMapper
{
    private const string CandidateIdentityLinkType = "candidate";

    internal static ExecutionRecord MapExecution(PersistentExecutionEntity entity) => new()
    {
        Id = entity.Id,
        ProcessInstanceId = entity.ProcessInstanceId,
        ProcessDefinitionId = entity.ProcessDefinitionId,
        ParentId = entity.ParentId,
        CurrentActivityId = entity.ActivityId,
        CurrentActivityName = entity.Name,
        Name = entity.Name,
        IsActive = entity.IsActive,
        IsEnded = entity.IsEnded,
        BusinessKey = entity.BusinessKey
    };

    internal static VariableInstanceRecord MapRuntimeVariableRecord(PersistentVariableEntity entity, byte[]? bytes) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Type = entity.Type,
        ExecutionId = entity.ExecutionId,
        ProcessInstanceId = entity.ProcessInstanceId,
        TaskId = entity.TaskId,
        Value = PersistenceVariableCodec.ReadValue(
            entity.Type,
            entity.TextValue,
            entity.TextValue2,
            entity.LongValue,
            entity.DoubleValue,
            bytes)
    };

    internal static CoreTaskImplementation MapTask(PersistentTaskEntity task, IReadOnlyCollection<PersistentIdentityLinkEntity> links) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Assignee = task.Assignee,
        Owner = task.Owner,
        ProcessInstanceId = task.ProcessInstanceId,
        ProcessDefinitionId = task.ProcessDefinitionId,
        Priority = task.Priority,
        DueDate = task.DueDate,
        DelegationState = task.DelegationState,
        Description = task.Description,
        TaskDefinitionKey = task.TaskDefinitionKey,
        ParentTaskId = task.ParentTaskId,
        Category = task.Category,
        FormKey = task.FormKey,
        CreateTime = task.CreateTime,
        CandidateUsers = links
            .Where(link => string.Equals(link.Type, CandidateIdentityLinkType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(link.UserId))
            .Select(link => link.UserId!)
            .Distinct(StringComparer.Ordinal)
            .ToList(),
        CandidateGroups = links
            .Where(link => string.Equals(link.Type, CandidateIdentityLinkType, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(link.GroupId))
            .Select(link => link.GroupId!)
            .Distinct(StringComparer.Ordinal)
            .ToList()
    };

    internal static CoreIdentityLinkEntity MapIdentityLink(PersistentIdentityLinkEntity entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        GroupId = entity.GroupId,
        Type = entity.Type,
        TaskId = entity.TaskId,
        ProcessInstanceId = entity.ProcessInstanceId,
        ProcessDefinitionId = entity.ProcessDefinitionId
    };

    internal static CoreCommentEntity MapComment(PersistentCommentEntity entity) => new()
    {
        Id = entity.Id,
        Action = entity.Action,
        FullMessage = entity.FullMessage,
        Message = entity.Message,
        ProcessInstanceId = entity.ProcessInstanceId,
        TaskId = entity.TaskId,
        Time = entity.Time ?? AbpTimeIdProvider.UtcNow,
        Type = entity.Type,
        UserId = entity.UserId
    };

    internal static CoreAttachmentEntity MapAttachment(PersistentAttachmentEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        Type = entity.Type,
        TaskId = entity.TaskId,
        ProcessInstanceId = entity.ProcessInstanceId,
        UserId = entity.UserId,
        Time = entity.CreateTime ?? AbpTimeIdProvider.UtcNow,
        Url = entity.Url,
        ContentId = entity.ContentId
    };

    internal static HistoricProcessInstance MapHistoricProcess(PersistentHistoricProcessEntity entity) => new(
        entity.Id,
        entity.ProcessDefinitionId,
        entity.BusinessKey,
        entity.StartTime,
        entity.EndTime);

    internal static HistoricTaskInstance MapHistoricTask(PersistentHistoricTaskEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Assignee = entity.Assignee,
        ProcessInstanceId = entity.ProcessInstanceId,
        ProcessDefinitionId = entity.ProcessDefinitionId,
        StartTime = entity.StartTime,
        EndTime = entity.EndTime,
        DeleteReason = entity.DeleteReason,
        TaskDefinitionKey = entity.TaskDefinitionKey
    };

    internal static HistoricActivityInstance MapHistoricActivity(PersistentHistoricActivityEntity entity) => new()
    {
        Id = entity.Id,
        ActivityId = entity.ActivityId,
        ActivityName = entity.ActivityName,
        ActivityType = entity.ActivityType,
        ProcessInstanceId = entity.ProcessInstanceId,
        ProcessDefinitionId = entity.ProcessDefinitionId,
        ExecutionId = entity.ExecutionId,
        StartTime = entity.StartTime,
        EndTime = entity.EndTime
    };

    internal static HistoricVariableInstance MapHistoricVariable(PersistentHistoricVariableEntity entity, byte[]? bytes) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Type = entity.Type,
        ProcessInstanceId = entity.ProcessInstanceId,
        TaskId = entity.TaskId,
        CreateTime = entity.CreateTime,
        Value = PersistenceVariableCodec.ReadValue(
            entity.Type,
            entity.TextValue,
            entity.TextValue2,
            entity.LongValue,
            entity.DoubleValue,
            bytes)
    };

    internal static HistoricDetail MapHistoricDetail(PersistentHistoricDetailEntity entity, byte[]? bytes) => new()
    {
        Id = entity.Id,
        Type = entity.Type,
        ProcessInstanceId = entity.ProcessInstanceId,
        VariableName = entity.Name,
        VariableValue = PersistenceVariableCodec.ReadValue(
            entity.VariableType,
            entity.TextValue,
            entity.TextValue2,
            entity.LongValue,
            entity.DoubleValue,
            bytes),
        Time = entity.Time
    };

    internal static HistoricIdentityLink MapHistoricIdentityLink(PersistentHistoricIdentityLinkEntity entity) => new()
    {
        Id = entity.Id,
        ProcessInstanceId = entity.ProcessInstanceId,
        TaskId = entity.TaskId,
        Type = entity.Type,
        UserId = entity.UserId,
        GroupId = entity.GroupId
    };

    internal static CoreIdentityLinkEntity MapHistoricIdentityLinkEntity(PersistentHistoricIdentityLinkEntity entity) => new()
    {
        Id = entity.Id,
        ProcessInstanceId = entity.ProcessInstanceId,
        TaskId = entity.TaskId,
        Type = entity.Type,
        UserId = entity.UserId,
        GroupId = entity.GroupId
    };
}
