using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SqlSugar;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Core.Variable;

namespace AsterERP.Workflow.Core.History;

public class HistoricEntityServiceImplementation : IHistoricEntityService
{
    private static readonly VariableTypes VariableTypes = new DefaultVariableTypes();

    public IProcessEngineConfiguration? Configuration { get; set; }

    private ISqlSugarClient RequireSqlSugarClient()
    {
        if (ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(Configuration)
                is IWorkflowSqlSugarClientAccessor workflowDbAccessor)
        {
            return workflowDbAccessor.Db;
        }

        var dbClient = ProcessEngineServiceProviderAccessor.GetService<ISqlSugarClient>(Configuration);
        if (dbClient == null)
        {
            throw new WorkflowEngineException("Historic persistence requires ISqlSugarClient.");
        }

        return dbClient;
    }

    private static (AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity Variable, AsterERP.Workflow.Persistence.Entities.HistoricDetailEntity Detail) ToHistoricVariableAndDetailEntities(
        string variableId,
        string detailId,
        string? processInstanceId,
        string? taskId,
        string name,
        object? value,
        DateTime createTime,
        DateTime lastUpdatedTime)
    {
        var runtimeVariable = new AsterERP.Workflow.Core.Variable.VariableInstanceEntity
        {
            Id = variableId,
            Name = name,
            ProcessInstanceId = processInstanceId,
            TaskId = taskId,
            CreateTime = createTime,
            LastUpdatedTime = lastUpdatedTime
        };

        var variableType = VariableTypes.FindVariableType(value);
        runtimeVariable.Type = variableType.TypeName;
        variableType.SetValue(value, runtimeVariable);

        var variableEntity = new AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity
        {
            Id = runtimeVariable.Id!,
            Name = runtimeVariable.Name,
            ProcessInstanceId = runtimeVariable.ProcessInstanceId,
            TaskId = runtimeVariable.TaskId,
            Type = runtimeVariable.Type,
            TextValue = runtimeVariable.TextValue,
            TextValue2 = runtimeVariable.TextValue2,
            LongValue = runtimeVariable.LongValue,
            DoubleValue = runtimeVariable.DoubleValue,
            CreateTime = runtimeVariable.CreateTime,
            LastUpdatedTime = runtimeVariable.LastUpdatedTime
        };

        var detailEntity = new AsterERP.Workflow.Persistence.Entities.HistoricDetailEntity
        {
            Id = detailId,
            Type = "VariableUpdate",
            ProcessInstanceId = processInstanceId,
            VariableId = runtimeVariable.Id,
            VariableInstanceId = runtimeVariable.Id,
            Name = name,
            VariableType = runtimeVariable.Type,
            Time = lastUpdatedTime,
            TextValue = runtimeVariable.TextValue,
            TextValue2 = runtimeVariable.TextValue2,
            LongValue = runtimeVariable.LongValue,
            DoubleValue = runtimeVariable.DoubleValue
        };

        return (variableEntity, detailEntity);
    }

    private static object? ReadVariableValue(
        string? type,
        string? textValue,
        string? textValue2,
        long? longValue,
        double? doubleValue)
    {
        if (!string.IsNullOrWhiteSpace(type))
        {
            var variableType = VariableTypes.GetVariableType(type);
            if (variableType != null)
            {
                var runtimeVariable = new AsterERP.Workflow.Core.Variable.VariableInstanceEntity
                {
                    Type = type,
                    TextValue = textValue,
                    TextValue2 = textValue2,
                    LongValue = longValue,
                    DoubleValue = doubleValue
                };
                return variableType.GetValue(runtimeVariable);
            }
        }

        if (!string.IsNullOrWhiteSpace(textValue2) && (textValue2.StartsWith("{") || textValue2.StartsWith("[")))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<object>(textValue2);
            }
            catch {}
        }

        if (textValue != null) return textValue;
        if (longValue.HasValue) return longValue.Value;
        if (doubleValue.HasValue) return doubleValue.Value;
        return null;
    }

    public async global::System.Threading.Tasks.Task RecordProcessInstanceStartAsync(string processInstanceId, string processDefinitionId, string? businessKey, string? startUserId, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        await UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity
        {
            Id = processInstanceId,
            ProcessDefinitionId = processDefinitionId,
            BusinessKey = businessKey,
            StartUserId = startUserId,
            StartTime = AbpTimeIdProvider.UtcNow
        }, cancellationToken, dbClient);
    }

    public async global::System.Threading.Tasks.Task RecordProcessInstanceEndAsync(string processInstanceId, string? deleteReason, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        var entity = await dbClient.Queryable<AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity>().InSingleAsync(processInstanceId);
        if (entity == null)
        {
            return;
        }

        var endTime = AbpTimeIdProvider.UtcNow;
        entity.EndTime = endTime;
        if (entity.StartTime.HasValue)
        {
            entity.DurationInMillis = (long)(endTime - entity.StartTime.Value).TotalMilliseconds;
        }

        entity.DeleteReason = deleteReason;
        await dbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async global::System.Threading.Tasks.Task RecordTaskCreatedAsync(string taskId, string taskName, string? assignee, string processInstanceId, string? taskDefinitionKey, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        await UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity
        {
            Id = taskId,
            Name = taskName,
            Assignee = assignee,
            ProcessInstanceId = processInstanceId,
            TaskDefinitionKey = taskDefinitionKey,
            StartTime = AbpTimeIdProvider.UtcNow
        }, cancellationToken, dbClient);
    }

    public async global::System.Threading.Tasks.Task RecordTaskCompletedAsync(string taskId, string? assignee, string? deleteReason, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        var entity = await dbClient.Queryable<AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity>().InSingleAsync(taskId);
        if (entity == null)
        {
            return;
        }

        var endTime = AbpTimeIdProvider.UtcNow;
        entity.Assignee = assignee ?? entity.Assignee;
        entity.EndTime = endTime;
        if (entity.StartTime.HasValue)
        {
            entity.DurationInMillis = (long)(endTime - entity.StartTime.Value).TotalMilliseconds;
        }

        entity.DeleteReason = deleteReason;
        await dbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async global::System.Threading.Tasks.Task RecordActivityStartAsync(string id, string activityId, string activityName, string activityType, string executionId, string processInstanceId, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        await UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity
        {
            Id = string.IsNullOrEmpty(id) ? AbpTimeIdProvider.NewGuid("N") : id,
            ActivityId = activityId,
            ActivityName = activityName,
            ActivityType = activityType,
            ExecutionId = executionId,
            ProcessInstanceId = processInstanceId,
            StartTime = AbpTimeIdProvider.UtcNow
        }, cancellationToken, dbClient);
    }

    public async global::System.Threading.Tasks.Task RecordActivityEndAsync(string activityId, string executionId, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        var entity = await dbClient.Queryable<AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity>()
            .Where(it => it.ExecutionId == executionId && it.ActivityId == activityId && it.EndTime == null)
            .FirstAsync(cancellationToken);
        if (entity == null)
        {
            return;
        }

        var endTime = AbpTimeIdProvider.UtcNow;
        entity.EndTime = endTime;
        if (entity.StartTime.HasValue)
        {
            entity.DurationInMillis = (long)(endTime - entity.StartTime.Value).TotalMilliseconds;
        }

        await dbClient.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async global::System.Threading.Tasks.Task RecordVariableAsync(
        string id,
        string variableName,
        object? value,
        string processInstanceId,
        string? taskId,
        bool includeDetail = true,
        CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        var now = AbpTimeIdProvider.UtcNow;
        var variableId = string.IsNullOrEmpty(id) ? AbpTimeIdProvider.NewGuid("N") : id;
        var detailId = AbpTimeIdProvider.NewGuid("N");
        var entities = ToHistoricVariableAndDetailEntities(variableId, detailId, processInstanceId, taskId, variableName, value, now, now);
        await UpsertAsync(entities.Variable, cancellationToken, dbClient);
        if (includeDetail)
        {
            await dbClient.Insertable(entities.Detail).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async global::System.Threading.Tasks.Task<HistoricProcessInstanceRecord?> GetHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var entity = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity>()
            .Where(item => item.Id == processInstanceId)
            .FirstAsync(cancellationToken);
        return entity == null ? null : new HistoricProcessInstanceRecord
        {
            Id = entity.Id,
            ProcessDefinitionId = entity.ProcessDefinitionId,
            BusinessKey = entity.BusinessKey,
            StartUserId = entity.StartUserId,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            DurationInMillis = entity.DurationInMillis,
            DeleteReason = entity.DeleteReason
        };
    }

    public async global::System.Threading.Tasks.Task<List<HistoricProcessInstanceRecord>> GetHistoricProcessInstancesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity>()
            .ToListAsync(cancellationToken);
        return entities
            .Select(entity => new HistoricProcessInstanceRecord
            {
                Id = entity.Id,
                ProcessDefinitionId = entity.ProcessDefinitionId,
                BusinessKey = entity.BusinessKey,
                StartUserId = entity.StartUserId,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                DurationInMillis = entity.DurationInMillis,
                DeleteReason = entity.DeleteReason
            }).ToList();
    }

    public async global::System.Threading.Tasks.Task<HistoricTaskInstanceRecord?> GetHistoricTaskInstanceAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var entity = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity>()
            .Where(item => item.Id == taskId)
            .FirstAsync(cancellationToken);
        return entity == null ? null : new HistoricTaskInstanceRecord
        {
            Id = entity.Id,
            Name = entity.Name,
            Assignee = entity.Assignee,
            Owner = entity.Owner,
            ProcessInstanceId = entity.ProcessInstanceId,
            TaskDefinitionKey = entity.TaskDefinitionKey,
            StartTime = entity.StartTime,
            EndTime = entity.EndTime,
            DurationInMillis = entity.DurationInMillis,
            DeleteReason = entity.DeleteReason
        };
    }

    public async global::System.Threading.Tasks.Task<List<HistoricTaskInstanceRecord>> GetHistoricTaskInstancesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity>()
            .ToListAsync(cancellationToken);
        return entities
            .Select(entity => new HistoricTaskInstanceRecord
            {
                Id = entity.Id,
                Name = entity.Name,
                Assignee = entity.Assignee,
                Owner = entity.Owner,
                ProcessInstanceId = entity.ProcessInstanceId,
                TaskDefinitionKey = entity.TaskDefinitionKey,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                DurationInMillis = entity.DurationInMillis,
                DeleteReason = entity.DeleteReason
            }).ToList();
    }

    public async global::System.Threading.Tasks.Task<List<HistoricActivityInstanceRecord>> GetHistoricActivityInstancesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity>()
            .ToListAsync(cancellationToken);
        return entities
            .Select(entity => new HistoricActivityInstanceRecord
            {
                Id = entity.Id,
                ActivityId = entity.ActivityId,
                ActivityName = entity.ActivityName,
                ActivityType = entity.ActivityType,
                ExecutionId = entity.ExecutionId,
                ProcessInstanceId = entity.ProcessInstanceId,
                StartTime = entity.StartTime,
                EndTime = entity.EndTime,
                DurationInMillis = entity.DurationInMillis
            }).ToList();
    }

    public async global::System.Threading.Tasks.Task<List<HistoricVariableInstanceRecord>> GetHistoricVariableInstancesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity>()
            .ToListAsync(cancellationToken);
        return entities
            .Select(entity => new HistoricVariableInstanceRecord
            {
                Id = entity.Id,
                Name = entity.Name,
                VariableType = entity.Type,
                Value = ReadVariableValue(entity.Type, entity.TextValue, entity.TextValue2, entity.LongValue, entity.DoubleValue),
                ProcessInstanceId = entity.ProcessInstanceId,
                TaskId = entity.TaskId,
                CreateTime = entity.CreateTime,
                LastUpdatedTime = entity.LastUpdatedTime
            }).ToList();
    }

    public async global::System.Threading.Tasks.Task ClearAsync(CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity>().ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity>().ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity>().ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity>().ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricDetailEntity>().ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity>().ExecuteCommandAsync(cancellationToken);
    }

    public global::System.Threading.Tasks.Task RestoreHistoricProcessInstanceAsync(HistoricProcessInstanceRecord record, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity
        {
            Id = record.Id,
            ProcessDefinitionId = record.ProcessDefinitionId,
            BusinessKey = record.BusinessKey,
            StartUserId = record.StartUserId,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            DurationInMillis = record.DurationInMillis,
            DeleteReason = record.DeleteReason
        }, cancellationToken, RequireSqlSugarClient());
    }

    public global::System.Threading.Tasks.Task RestoreHistoricTaskInstanceAsync(HistoricTaskInstanceRecord record, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity
        {
            Id = record.Id,
            Name = record.Name,
            Assignee = record.Assignee,
            Owner = record.Owner,
            ProcessInstanceId = record.ProcessInstanceId,
            TaskDefinitionKey = record.TaskDefinitionKey,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            DurationInMillis = record.DurationInMillis,
            DeleteReason = record.DeleteReason
        }, cancellationToken, RequireSqlSugarClient());
    }

    public global::System.Threading.Tasks.Task RestoreHistoricActivityInstanceAsync(HistoricActivityInstanceRecord record, CancellationToken cancellationToken = default)
    {
        return UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity
        {
            Id = record.Id,
            ActivityId = record.ActivityId,
            ActivityName = record.ActivityName,
            ActivityType = record.ActivityType,
            ExecutionId = record.ExecutionId,
            ProcessInstanceId = record.ProcessInstanceId,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            DurationInMillis = record.DurationInMillis
        }, cancellationToken, RequireSqlSugarClient());
    }

    public async global::System.Threading.Tasks.Task RestoreHistoricVariableInstanceAsync(HistoricVariableInstanceRecord record, CancellationToken cancellationToken = default)
    {
        var entities = ToHistoricVariableAndDetailEntities(
            record.Id,
            AbpTimeIdProvider.NewGuid("N"),
            record.ProcessInstanceId,
            record.TaskId,
            record.Name!,
            record.Value,
            record.CreateTime ?? AbpTimeIdProvider.UtcNow,
            record.LastUpdatedTime ?? AbpTimeIdProvider.UtcNow);
        var dbClient = RequireSqlSugarClient();
        await UpsertAsync(entities.Variable, cancellationToken, dbClient);
        await UpsertAsync(entities.Detail, cancellationToken, dbClient);
    }

    public global::System.Threading.Tasks.Task RestoreHistoricIdentityLinkAsync(HistoricIdentityLinkRecord record, CancellationToken cancellationToken = default)
    {
        var key = record.Id ?? AbpTimeIdProvider.NewGuid("N");
        return UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity
        {
            Id = key,
            ProcessInstanceId = record.ProcessInstanceId,
            TaskId = record.TaskId,
            Type = record.Type,
            UserId = record.UserId,
            GroupId = record.GroupId
        }, cancellationToken, RequireSqlSugarClient());
    }

    public async global::System.Threading.Tasks.Task<bool> DeleteHistoricTaskInstanceAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity>().In(taskId).ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity>().Where(it => it.TaskId == taskId).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async global::System.Threading.Tasks.Task<bool> DeleteHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var dbClient = RequireSqlSugarClient();
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricProcessInstanceEntity>().In(processInstanceId).ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricTaskInstanceEntity>().Where(it => it.ProcessInstanceId == processInstanceId).ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricActivityInstanceEntity>().Where(it => it.ProcessInstanceId == processInstanceId).ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricVariableInstanceEntity>().Where(it => it.ProcessInstanceId == processInstanceId).ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricDetailEntity>().Where(it => it.ProcessInstanceId == processInstanceId).ExecuteCommandAsync(cancellationToken);
        await dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity>().Where(it => it.ProcessInstanceId == processInstanceId).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public global::System.Threading.Tasks.Task RecordIdentityLinkAsync(string id, string? processInstanceId, string? type, string? userId, string? groupId, CancellationToken cancellationToken = default)
    {
        var key = id ?? AbpTimeIdProvider.NewGuid("N");
        return UpsertAsync(new AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity
        {
            Id = key,
            ProcessInstanceId = processInstanceId,
            Type = type,
            UserId = userId,
            GroupId = groupId
        }, cancellationToken, RequireSqlSugarClient());
    }

    public async global::System.Threading.Tasks.Task<List<HistoricIdentityLinkRecord>> GetHistoricIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        var entities = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity>()
            .Where(it => it.ProcessInstanceId == processInstanceId)
            .ToListAsync(cancellationToken);
        return entities
            .Select(entity => new HistoricIdentityLinkRecord
            {
                Id = entity.Id,
                ProcessInstanceId = entity.ProcessInstanceId,
                TaskId = entity.TaskId,
                Type = entity.Type,
                UserId = entity.UserId,
                GroupId = entity.GroupId
            }).ToList();
    }

    public async global::System.Threading.Tasks.Task<List<HistoricIdentityLinkRecord>> GetHistoricIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var entities = await RequireSqlSugarClient().Queryable<AsterERP.Workflow.Persistence.Entities.HistoricIdentityLinkEntity>()
            .Where(it => it.TaskId == taskId)
            .ToListAsync(cancellationToken);
        return entities
            .Select(entity => new HistoricIdentityLinkRecord
            {
                Id = entity.Id,
                ProcessInstanceId = entity.ProcessInstanceId,
                TaskId = entity.TaskId,
                Type = entity.Type,
                UserId = entity.UserId,
                GroupId = entity.GroupId
            }).ToList();
    }

    private static async global::System.Threading.Tasks.Task UpsertAsync<T>(T entity, CancellationToken cancellationToken, ISqlSugarClient dbClient) where T : class, new()
    {
        var affectedRows = await dbClient.Updateable(entity)
            .WhereColumns(nameof(AsterERP.Workflow.Persistence.Entities.IEntity.Id))
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows == 0)
        {
            await dbClient.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
    }
}

public record HistoricProcessInstanceRecord
{
    public string Id { get; init; } = null!;
    public string? ProcessDefinitionId { get; init; }
    public string? BusinessKey { get; init; }
    public string? StartUserId { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public long? DurationInMillis { get; init; }
    public string? DeleteReason { get; init; }
}

public record HistoricTaskInstanceRecord
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Assignee { get; init; }
    public string? Owner { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? TaskDefinitionKey { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public long? DurationInMillis { get; init; }
    public string? DeleteReason { get; init; }
}

public record HistoricActivityInstanceRecord
{
    public string Id { get; init; } = null!;
    public string? ActivityId { get; init; }
    public string? ActivityName { get; init; }
    public string? ActivityType { get; init; }
    public string? ExecutionId { get; init; }
    public string? ProcessInstanceId { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public long? DurationInMillis { get; init; }
}

public record HistoricVariableInstanceRecord
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? VariableType { get; init; }
    public object? Value { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? TaskId { get; init; }
    public DateTime? CreateTime { get; init; }
    public DateTime? LastUpdatedTime { get; init; }
}

public record HistoricIdentityLinkRecord
{
    public string? Id { get; init; }
    public string? ProcessInstanceId { get; init; }
    public string? TaskId { get; init; }
    public string? Type { get; init; }
    public string? UserId { get; init; }
    public string? GroupId { get; init; }
}

