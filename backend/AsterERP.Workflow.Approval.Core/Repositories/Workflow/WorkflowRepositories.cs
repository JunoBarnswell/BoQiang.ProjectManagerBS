using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Persistence.Entities;
using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Workflow;

public class CommentInfoRepository : SqlSugarRepository<CommentInfo>, ICommentInfoRepository
{
    public CommentInfoRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<CommentInfo>> GetCommentInfosByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await Db.Queryable<CommentInfo>()
            .Where(c => c.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(processInstanceId), c => c.ProcessInstanceId == processInstanceId)
            .OrderBy(c => c.Time, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }
}

public class ExtendHisprocinstRepository : SqlSugarRepository<ExtendHisprocinst>, IExtendHisprocinstRepository
{
    public ExtendHisprocinstRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ExtendProcinstRepository : SqlSugarRepository<ExtendProcinst>, IExtendProcinstRepository
{
    public ExtendProcinstRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class FlowListenerRepository : SqlSugarRepository<FlowListener>, IFlowListenerRepository
{
    public FlowListenerRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class FlowListenerParamRepository : SqlSugarRepository<FlowListenerParam>, IFlowListenerParamRepository
{
    public FlowListenerParamRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class WorkflowHistoricActivityRepository : SqlSugarRepository<WorkflowHistoricActivityRecord>, IWorkflowHistoricActivityRepository
{
    public WorkflowHistoricActivityRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class WorkflowRuntimeActivityRepository : SqlSugarRepository<WorkflowRuntimeActivityRecord>, IWorkflowRuntimeActivityRepository
{
    public WorkflowRuntimeActivityRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class WorkflowProcessDefinitionRepository : SqlSugarRepository<WorkflowProcessDefinition>, IWorkflowProcessDefinitionRepository
{
    public WorkflowProcessDefinitionRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ModelInfoRepository : SqlSugarRepository<ModelInfo>, IModelInfoRepository
{
    public ModelInfoRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<RefAsync<Page<ModelInfo>>> GetPagerModelAsync(ModelInfo modelInfo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var content = await Db.Queryable<ModelInfo>()
            .Where(m => m.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.Keyword), m => m.Name.Contains(modelInfo.Keyword) || m.ModelKey.Contains(modelInfo.Keyword))
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.CategoryCode), m => m.CategoryCode == modelInfo.CategoryCode)
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.ModelKey), m => m.ModelKey == modelInfo.ModelKey)
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.Name), m => m.Name.Contains(modelInfo.Name))
            .WhereIF(!string.IsNullOrWhiteSpace(modelInfo.AppSn), m => m.AppSn == modelInfo.AppSn)
            .WhereIF(modelInfo.ModelType.HasValue, m => m.ModelType == modelInfo.ModelType)
            .WhereIF(modelInfo.Status.HasValue, m => m.Status == modelInfo.Status)
            .OrderBy(m => m.CreateTime, OrderByType.Desc)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        return WorkflowRepositoryHelpers.BuildPage(content, total.Value);
    }
}

public class WorkflowProcessInstanceRepository : IWorkflowProcessInstanceRepository
{
    public WorkflowProcessInstanceRepository(ISqlSugarClient db)
    {
        Db = db;
    }

    public ISqlSugarClient Db { get; }

    public async Task<RefAsync<Page<ProcessInstanceVo>>> FindMyProcessinstancesPagerModelAsync(InstanceQueryParamsVo @params, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Db.Queryable<ExtendHisprocinst>()
            .Where(e => e.DelFlag == 1)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.UserCode), e => e.CurrentUserCode == @params.UserCode)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.AppSn), e => e.TenantId == @params.AppSn)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ProcessInstanceId), e => e.ProcessInstanceId == @params.ProcessInstanceId)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.BusinessKey), e => e.BusinessKey == @params.BusinessKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ProcessDefinitionKey), e => e.ModelKey == @params.ProcessDefinitionKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.Keyword), e => e.ProcessName!.Contains(@params.Keyword) || e.BusinessKey!.Contains(@params.Keyword));

        var startTime = WorkflowRepositoryHelpers.ParseDateTime(@params.StartTime);
        if (startTime.HasValue)
        {
            query = query.Where(e => e.CreateTime >= startTime.Value);
        }

        var endTime = WorkflowRepositoryHelpers.ParseDateTime(@params.EndTime);
        if (endTime.HasValue)
        {
            query = query.Where(e => e.CreateTime <= endTime.Value);
        }

        RefAsync<int> total = new();
        var content = await query
            .OrderByIF(@params.OrderFlag == 1, e => e.CreateTime, OrderByType.Asc)
            .OrderByIF(@params.OrderFlag != 1, e => e.CreateTime, OrderByType.Desc)
            .Select(e => new ProcessInstanceVo
            {
                ProcessInstanceId = e.ProcessInstanceId ?? string.Empty,
                ProcessDefinitionId = e.ProcessDefinitionId ?? string.Empty,
                ProcessDefinitionName = e.ProcessName ?? string.Empty,
                ProcessDefinitionKey = e.ModelKey ?? string.Empty,
                BusinessKey = e.BusinessKey ?? string.Empty,
                AppSn = e.TenantId ?? string.Empty,
                AppName = e.TenantId ?? string.Empty,
                CreateTime = e.CreateTime,
                StartTime = e.CreateTime,
                EndTime = e.UpdateTime,
                StartedUserId = e.CurrentUserCode ?? string.Empty,
                StartedUserName = e.CurrentUserCode ?? string.Empty,
                StartPersonName = e.CurrentUserCode ?? string.Empty,
                FormName = e.ProcessName ?? string.Empty,
                ProcessStatus = e.ProcessStatus ?? string.Empty,
                ProcessStatusName = e.ProcessStatus ?? string.Empty
            })
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        return WorkflowRepositoryHelpers.BuildPage(content, total.Value);
    }
}

public class WorkflowTaskRepository : IWorkflowTaskRepository
{
    private static readonly DateTime ActiveEndTime = new(1900, 1, 1);

    public WorkflowTaskRepository(ISqlSugarClient db)
    {
        Db = db;
    }

    public ISqlSugarClient Db { get; }

    public async Task<RefAsync<Page<TaskVo>>> GetApplyedTasksPagerModelAsync(TaskQueryParamsVo @params, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var activeEndTime = ActiveEndTime;
        var query = Db.Queryable<WorkflowHistoricActivityRecord, ExtendHisprocinst, TaskEntity>((a, e, t) => new object[]
            {
                JoinType.Inner, a.ProcInstId == e.ProcessInstanceId,
                JoinType.Left, a.TaskId == t.Id
            })
            .Where((a, e, t) => !string.IsNullOrEmpty(a.TaskId) && e.DelFlag == 1 && t.Id == null)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.AppSn), (a, e, t) => e.TenantId == @params.AppSn)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ModelKey), (a, e, t) => e.ModelKey == @params.ModelKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.Assignee), (a, e, t) => a.Assignee == @params.Assignee)
            .WhereIF(string.IsNullOrWhiteSpace(@params.Assignee) && !string.IsNullOrWhiteSpace(@params.UserCode), (a, e, t) => a.Assignee == @params.UserCode)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ProcessInstanceId), (a, e, t) => a.ProcInstId == @params.ProcessInstanceId)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.BusinessKey), (a, e, t) => e.BusinessKey == @params.BusinessKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.FormName), (a, e, t) => e.ProcessName!.Contains(@params.FormName))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.TaskName), (a, e, t) => a.ActName!.Contains(@params.TaskName))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.Keyword), (a, e, t) => a.ActName!.Contains(@params.Keyword) || a.ProcInstId!.Contains(@params.Keyword) || a.TaskId!.Contains(@params.Keyword) || e.BusinessKey!.Contains(@params.Keyword) || e.ProcessName!.Contains(@params.Keyword));

        var startTime = WorkflowRepositoryHelpers.ParseDateTime(@params.StartTime);
        if (startTime.HasValue)
        {
            query = query.Where((a, e, t) => a.StartTime >= startTime.Value);
        }

        var endTime = WorkflowRepositoryHelpers.ParseDateTime(@params.EndTime);
        if (endTime.HasValue)
        {
            query = query.Where((a, e, t) => a.StartTime <= endTime.Value);
        }

        RefAsync<int> total = new();
        var content = await query
            .OrderByIF(@params.OrderFlag == 1, (a, e, t) => a.StartTime, OrderByType.Asc)
            .OrderByIF(@params.OrderFlag != 1, (a, e, t) => a.StartTime, OrderByType.Desc)
            .Select((a, e, t) => new TaskVo
            {
                TaskId = a.TaskId ?? string.Empty,
                TaskDefKey = a.ActId ?? string.Empty,
                Name = a.ActName ?? string.Empty,
                Assignee = a.Assignee ?? string.Empty,
                AssigneeName = a.Assignee ?? string.Empty,
                CreateTime = a.StartTime,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Finished = a.EndTime != null && a.EndTime != activeEndTime,
                FinishedTime = a.EndTime,
                ProcessInstanceId = a.ProcInstId ?? string.Empty,
                ProcessDefinitionId = a.ProcDefId ?? string.Empty,
                ProcessDefinitionKey = e.ModelKey ?? string.Empty,
                TaskType = a.ActType ?? string.Empty,
                ProcessStatus = a.EndTime == null || a.EndTime == activeEndTime ? ProcessStatusEnum.SPZ.ToString() : ProcessStatusEnum.BJ.ToString(),
                ProcessStatusName = a.EndTime == null || a.EndTime == activeEndTime ? ProcessStatusEnum.SPZ.GetMsg() : ProcessStatusEnum.BJ.GetMsg(),
                BusinessKey = e.BusinessKey ?? string.Empty,
                FormName = e.ProcessName ?? string.Empty,
                AppName = e.TenantId ?? string.Empty,
                StartPersonCode = e.CurrentUserCode ?? string.Empty,
                StartPersonName = e.CurrentUserCode ?? string.Empty
            })
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        return WorkflowRepositoryHelpers.BuildPage(content, total.Value);
    }

    public async Task<RefAsync<Page<TaskVo>>> GetAppingTasksPagerModelAsync(TaskQueryParamsVo @params, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var activeEndTime = ActiveEndTime;
        var activeEndTimeUpperBound = activeEndTime.AddDays(1);
        var candidateTaskIds = string.IsNullOrWhiteSpace(@params.UserCode) || !string.IsNullOrWhiteSpace(@params.Assignee)
            ? new List<string>()
            : await GetCandidateTaskIdsAsync(@params.UserCode, cancellationToken);

        var query = Db.Queryable<WorkflowRuntimeActivityRecord, ExtendHisprocinst, TaskEntity>((a, e, t) => new object[]
            {
                JoinType.Inner, a.ProcInstId == e.ProcessInstanceId,
                JoinType.Inner, a.TaskId == t.Id
            })
            .Where((a, e, t) => !string.IsNullOrEmpty(a.TaskId) && e.DelFlag == 1)
            .Where((a, e, t) => a.EndTime == null || a.EndTime <= activeEndTimeUpperBound)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.AppSn), (a, e, t) => e.TenantId == @params.AppSn)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ModelKey), (a, e, t) => e.ModelKey == @params.ModelKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.Assignee), (a, e, t) => a.Assignee == @params.Assignee)
            .WhereIF(string.IsNullOrWhiteSpace(@params.Assignee) && !string.IsNullOrWhiteSpace(@params.UserCode) && candidateTaskIds.Count == 0, (a, e, t) => a.Assignee == @params.UserCode)
            .WhereIF(string.IsNullOrWhiteSpace(@params.Assignee) && !string.IsNullOrWhiteSpace(@params.UserCode) && candidateTaskIds.Count > 0, (a, e, t) => a.Assignee == @params.UserCode || candidateTaskIds.Contains(a.TaskId!))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ProcessInstanceId), (a, e, t) => a.ProcInstId == @params.ProcessInstanceId)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.BusinessKey), (a, e, t) => e.BusinessKey == @params.BusinessKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.FormName), (a, e, t) => e.ProcessName!.Contains(@params.FormName))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.TaskName), (a, e, t) => a.ActName!.Contains(@params.TaskName))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.Keyword), (a, e, t) => a.ActName!.Contains(@params.Keyword) || a.ProcInstId!.Contains(@params.Keyword) || a.TaskId!.Contains(@params.Keyword) || e.BusinessKey!.Contains(@params.Keyword) || e.ProcessName!.Contains(@params.Keyword));

        var startTime = WorkflowRepositoryHelpers.ParseDateTime(@params.StartTime);
        if (startTime.HasValue)
        {
            query = query.Where((a, e, t) => a.StartTime >= startTime.Value);
        }

        var endTime = WorkflowRepositoryHelpers.ParseDateTime(@params.EndTime);
        if (endTime.HasValue)
        {
            query = query.Where((a, e, t) => a.StartTime <= endTime.Value);
        }

        RefAsync<int> total = new();
        var content = await query
            .OrderByIF(@params.OrderFlag == 1, (a, e, t) => a.StartTime, OrderByType.Asc)
            .OrderByIF(@params.OrderFlag != 1, (a, e, t) => a.StartTime, OrderByType.Desc)
            .Select((a, e, t) => new TaskVo
            {
                TaskId = a.TaskId ?? string.Empty,
                TaskDefKey = a.ActId ?? string.Empty,
                Name = a.ActName ?? string.Empty,
                Assignee = a.Assignee ?? string.Empty,
                AssigneeName = a.Assignee ?? string.Empty,
                CreateTime = a.StartTime,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Finished = false,
                ProcessInstanceId = a.ProcInstId ?? string.Empty,
                ProcessDefinitionId = a.ProcDefId ?? string.Empty,
                ProcessDefinitionKey = e.ModelKey ?? string.Empty,
                TaskType = a.ActType ?? string.Empty,
                ProcessStatus = ProcessStatusEnum.SPZ.ToString(),
                ProcessStatusName = ProcessStatusEnum.SPZ.GetMsg(),
                BusinessKey = e.BusinessKey ?? string.Empty,
                FormName = e.ProcessName ?? string.Empty,
                AppName = e.TenantId ?? string.Empty,
                StartPersonCode = e.CurrentUserCode ?? string.Empty,
                StartPersonName = e.CurrentUserCode ?? string.Empty
            })
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        return WorkflowRepositoryHelpers.BuildPage(content, total.Value);
    }

    public async Task<long> GetAppingTaskContAsync(TaskQueryParamsVo @params, CancellationToken cancellationToken = default)
    {
        var activeEndTime = ActiveEndTime;
        var activeEndTimeUpperBound = activeEndTime.AddDays(1);
        var candidateTaskIds = string.IsNullOrWhiteSpace(@params.UserCode) || !string.IsNullOrWhiteSpace(@params.Assignee)
            ? new List<string>()
            : await GetCandidateTaskIdsAsync(@params.UserCode, cancellationToken);

        var query = Db.Queryable<WorkflowRuntimeActivityRecord, ExtendHisprocinst, TaskEntity>((a, e, t) => new object[]
            {
                JoinType.Inner, a.ProcInstId == e.ProcessInstanceId,
                JoinType.Inner, a.TaskId == t.Id
            })
            .Where((a, e, t) => !string.IsNullOrEmpty(a.TaskId) && e.DelFlag == 1)
            .Where((a, e, t) => a.EndTime == null || a.EndTime <= activeEndTimeUpperBound)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.AppSn), (a, e, t) => e.TenantId == @params.AppSn)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ModelKey), (a, e, t) => e.ModelKey == @params.ModelKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.Assignee), (a, e, t) => a.Assignee == @params.Assignee)
            .WhereIF(string.IsNullOrWhiteSpace(@params.Assignee) && !string.IsNullOrWhiteSpace(@params.UserCode) && candidateTaskIds.Count == 0, (a, e, t) => a.Assignee == @params.UserCode)
            .WhereIF(string.IsNullOrWhiteSpace(@params.Assignee) && !string.IsNullOrWhiteSpace(@params.UserCode) && candidateTaskIds.Count > 0, (a, e, t) => a.Assignee == @params.UserCode || candidateTaskIds.Contains(a.TaskId!))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.ProcessInstanceId), (a, e, t) => a.ProcInstId == @params.ProcessInstanceId)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.BusinessKey), (a, e, t) => e.BusinessKey == @params.BusinessKey)
            .WhereIF(!string.IsNullOrWhiteSpace(@params.FormName), (a, e, t) => e.ProcessName!.Contains(@params.FormName))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.TaskName), (a, e, t) => a.ActName!.Contains(@params.TaskName))
            .WhereIF(!string.IsNullOrWhiteSpace(@params.Keyword), (a, e, t) => a.ActName!.Contains(@params.Keyword) || a.ProcInstId!.Contains(@params.Keyword) || a.TaskId!.Contains(@params.Keyword) || e.BusinessKey!.Contains(@params.Keyword) || e.ProcessName!.Contains(@params.Keyword));

        var startTime = WorkflowRepositoryHelpers.ParseDateTime(@params.StartTime);
        if (startTime.HasValue)
        {
            query = query.Where((a, e, t) => a.StartTime >= startTime.Value);
        }

        var endTime = WorkflowRepositoryHelpers.ParseDateTime(@params.EndTime);
        if (endTime.HasValue)
        {
            query = query.Where((a, e, t) => a.StartTime <= endTime.Value);
        }

        return await query.CountAsync(cancellationToken);
    }

    public async Task<TaskVo?> GetPendingTaskForUserAsync(string taskId, string appSn, string userCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId) || string.IsNullOrWhiteSpace(appSn) || string.IsNullOrWhiteSpace(userCode))
        {
            return null;
        }

        var activeEndTime = ActiveEndTime;
        var activeEndTimeUpperBound = activeEndTime.AddDays(1);
        var candidateTaskIds = await GetCandidateTaskIdsAsync(userCode, cancellationToken);

        return await Db.Queryable<WorkflowRuntimeActivityRecord, ExtendHisprocinst, TaskEntity>((a, e, t) => new object[]
            {
                JoinType.Inner, a.ProcInstId == e.ProcessInstanceId,
                JoinType.Inner, a.TaskId == t.Id
            })
            .Where((a, e, t) => !string.IsNullOrEmpty(a.TaskId) && e.DelFlag == 1)
            .Where((a, e, t) => a.TaskId == taskId && e.TenantId == appSn)
            .Where((a, e, t) => a.EndTime == null || a.EndTime <= activeEndTimeUpperBound)
            .Where((a, e, t) => a.Assignee == userCode || candidateTaskIds.Contains(a.TaskId!))
            .Select((a, e, t) => new TaskVo
            {
                TaskId = a.TaskId ?? string.Empty,
                TaskDefKey = a.ActId ?? string.Empty,
                Name = a.ActName ?? string.Empty,
                Assignee = a.Assignee ?? string.Empty,
                AssigneeName = a.Assignee ?? string.Empty,
                CreateTime = a.StartTime,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                Finished = false,
                ProcessInstanceId = a.ProcInstId ?? string.Empty,
                ProcessDefinitionId = a.ProcDefId ?? string.Empty,
                ProcessDefinitionKey = e.ModelKey ?? string.Empty,
                TaskType = a.ActType ?? string.Empty,
                ProcessStatus = ProcessStatusEnum.SPZ.ToString(),
                ProcessStatusName = ProcessStatusEnum.SPZ.GetMsg(),
                BusinessKey = e.BusinessKey ?? string.Empty,
                FormName = e.ProcessName ?? string.Empty,
                AppName = e.TenantId ?? string.Empty,
                StartPersonCode = e.CurrentUserCode ?? string.Empty,
                StartPersonName = e.CurrentUserCode ?? string.Empty
            })
            .FirstAsync(cancellationToken);
    }

    private async Task<List<string>> GetCandidateTaskIdsAsync(string userCode, CancellationToken cancellationToken)
    {
        var roleSns = await Db.Queryable<Personal, PersonalRole, Role>((personal, personalRole, role) => new object[]
            {
                JoinType.Inner, personal.Id == personalRole.PersonalId,
                JoinType.Inner, personalRole.RoleId == role.Id
            })
            .Where((personal, personalRole, role) => personal.DelFlag == 1 && personalRole.DelFlag == 1 && role.DelFlag == 1)
            .Where((personal, personalRole, role) => personal.Code == userCode)
            .Select((personal, personalRole, role) => role.Sn)
            .ToListAsync(cancellationToken);

        var normalizedRoleSns = roleSns
            .Where(roleSn => !string.IsNullOrWhiteSpace(roleSn))
            .Select(roleSn => roleSn!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var taskIds = await Db.Queryable<IdentityLinkEntity>()
            .Where(link => link.Type == "candidate" && !string.IsNullOrEmpty(link.TaskId))
            .Where(link => link.UserId == userCode || normalizedRoleSns.Contains(link.GroupId!))
            .Select(link => link.TaskId)
            .ToListAsync(cancellationToken);

        return taskIds
            .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
            .Select(taskId => taskId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public async Task UpdateHisAssigneeAsync(string taskId, string assignee, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        await Db.Updateable<WorkflowHistoricActivityRecord>()
            .SetColumns(a => a.Assignee == assignee)
            .Where(a => a.TaskId == taskId)
            .ExecuteCommandAsync(cancellationToken);

        await Db.Updateable<WorkflowRuntimeActivityRecord>()
            .SetColumns(a => a.Assignee == assignee)
            .Where(a => a.TaskId == taskId)
            .ExecuteCommandAsync(cancellationToken);
    }
}

internal static class WorkflowRepositoryHelpers
{
    public static RefAsync<Page<T>> BuildPage<T>(List<T> content, int totalElements) where T : class, new()
    {
        return new RefAsync<Page<T>>
        {
            Value = new Page<T>
            {
                Content = content,
                TotalElements = totalElements
            }
        };
    }

    public static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, out var parsed) ? parsed : null;
    }
}
