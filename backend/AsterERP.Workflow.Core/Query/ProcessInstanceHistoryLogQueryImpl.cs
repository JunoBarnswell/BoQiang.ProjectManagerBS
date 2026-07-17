using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.History;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public class ProcessInstanceHistoryLogImpl
{
    public HistoricProcessInstance? HistoricProcessInstance { get; }
    public List<HistoricData> HistoricData { get; } = new();

    public ProcessInstanceHistoryLogImpl(HistoricProcessInstance? historicProcessInstance)
    {
        HistoricProcessInstance = historicProcessInstance;
    }

    public void AddHistoricData(IEnumerable<HistoricData> data)
    {
        HistoricData.AddRange(data);
    }

    public void OrderHistoricData()
    {
        HistoricData.Sort((a, b) =>
        {
            if (a.Timestamp == null && b.Timestamp == null) return 0;
            if (a.Timestamp == null) return -1;
            if (b.Timestamp == null) return 1;
            return a.Timestamp.Value.CompareTo(b.Timestamp.Value);
        });
    }
}

public class ProcessInstanceHistoryLogQueryImpl : IProcessInstanceHistoryLogQuery
{
    protected ICommandExecutor? CommandExecutor { get; set; }
    private string? _processInstanceId;
    protected bool _includeTasks;
    protected bool _includeActivities;
    protected bool _includeVariables;
    protected bool _includeComments;
    protected bool _includeVariableUpdates;
    protected bool _includeFormProperties;

    public ProcessInstanceHistoryLogQueryImpl() { }

    public ProcessInstanceHistoryLogQueryImpl(ICommandExecutor commandExecutor, string processInstanceId)
    {
        CommandExecutor = commandExecutor;
        _processInstanceId = processInstanceId;
    }

    public ProcessInstanceHistoryLogQueryImpl IncludeTasks()
    {
        _includeTasks = true;
        return this;
    }

    public ProcessInstanceHistoryLogQueryImpl IncludeComments()
    {
        _includeComments = true;
        return this;
    }

    public ProcessInstanceHistoryLogQueryImpl IncludeActivities()
    {
        _includeActivities = true;
        return this;
    }

    public ProcessInstanceHistoryLogQueryImpl IncludeVariables()
    {
        _includeVariables = true;
        return this;
    }

    public ProcessInstanceHistoryLogQueryImpl IncludeVariableUpdates()
    {
        _includeVariableUpdates = true;
        return this;
    }

    public ProcessInstanceHistoryLogQueryImpl IncludeFormProperties()
    {
        _includeFormProperties = true;
        return this;
    }

    public ProcessInstanceHistoryLogQueryImpl ProcessInstanceId(string processInstanceId)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("processInstanceId is null");

        _processInstanceId = processInstanceId;
        return this;
    }

    public Task<ProcessInstanceHistoryLogImpl?> SingleResultAsync(CancellationToken cancellationToken = default)
    {
        if (_processInstanceId == null || CommandExecutor == null)
            return Task.FromResult<ProcessInstanceHistoryLogImpl?>(null);
        return BuildFromCommandPathAsync(cancellationToken);
    }

    IProcessInstanceHistoryLogQuery IProcessInstanceHistoryLogQuery.ProcessInstanceId(string processInstanceId) => ProcessInstanceId(processInstanceId);
    IProcessInstanceHistoryLogQuery IProcessInstanceHistoryLogQuery.IncludeActivities() => IncludeActivities();
    IProcessInstanceHistoryLogQuery IProcessInstanceHistoryLogQuery.IncludeVariables() => IncludeVariables();
    IProcessInstanceHistoryLogQuery IProcessInstanceHistoryLogQuery.IncludeTasks() => IncludeTasks();

    public async Task<List<HistoricData>> ListAsync(CancellationToken cancellationToken = default)
    {
        var log = await SingleResultAsync(cancellationToken);
        return log?.HistoricData ?? new List<HistoricData>();
    }

    private async Task<ProcessInstanceHistoryLogImpl?> BuildFromCommandPathAsync(CancellationToken cancellationToken)
    {
        var historicProcessInstance = await CommandExecutor!.ExecuteAsync(
            new GetHistoricProcessInstanceCmd(_processInstanceId!),
            cancellationToken);
        var log = new ProcessInstanceHistoryLogImpl(historicProcessInstance);

        if (_includeTasks)
        {
            var tasks = await CommandExecutor.ExecuteAsync(new GetHistoricTaskInstancesCmd(), cancellationToken);
            log.AddHistoricData(tasks
                .Where(t => t.ProcessInstanceId == _processInstanceId)
                .Select(t => new HistoricData
                {
                    Id = t.Id,
                    Type = "task",
                    Timestamp = t.StartTime,
                    ProcessInstanceId = t.ProcessInstanceId,
                    TaskId = t.Id,
                    TaskName = t.Name
                }));
        }

        if (_includeActivities)
        {
            var activities = await CommandExecutor.ExecuteAsync(new GetHistoricActivityInstancesCmd(), cancellationToken);
            log.AddHistoricData(activities
                .Where(a => a.ProcessInstanceId == _processInstanceId)
                .Select(a => new HistoricData
                {
                    Id = a.Id,
                    Type = "activity",
                    Timestamp = a.StartTime,
                    ProcessInstanceId = a.ProcessInstanceId,
                    ActivityId = a.ActivityId,
                    ActivityName = a.ActivityName,
                    ActivityType = a.ActivityType
                }));
        }

        if (_includeVariables)
        {
            var variables = await CommandExecutor.ExecuteAsync(new GetHistoricVariableInstancesCmd(), cancellationToken);
            log.AddHistoricData(variables
                .Where(v => v.ProcessInstanceId == _processInstanceId)
                .Select(v => new HistoricData
                {
                    Id = v.Id,
                    Type = "variable",
                    Timestamp = v.CreateTime,
                    ProcessInstanceId = v.ProcessInstanceId,
                    VariableName = v.Name,
                    VariableValue = v.Value
                }));
        }

        log.OrderHistoricData();
        return log;
    }
}
