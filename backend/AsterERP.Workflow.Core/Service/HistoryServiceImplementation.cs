using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Services;
using QueryNs = AsterERP.Workflow.Core.Query;

namespace AsterERP.Workflow.Core.Service;

public class HistoryServiceImplementation : ServiceImpl, IHistoryService
{
    public HistoryServiceImplementation() : base(AsterERP.Workflow.Core.Engine.ProcessEngineConfiguration.CreateDefault()) { }

    public HistoryServiceImplementation(IProcessEngineConfiguration processEngineConfiguration)
        : base(processEngineConfiguration) { }

    public HistoryServiceImplementation(ICommandExecutor commandExecutor)
        : base(commandExecutor) { }

    public Task<List<HistoricProcessInstance>> GetHistoricProcessInstancesAsync(CancellationToken cancellationToken = default)
        => CommandExecutor.ExecuteAsync(new GetHistoricProcessInstancesCmd(), cancellationToken);

    public async Task<HistoricProcessInstance?> GetHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");
        return await CommandExecutor.ExecuteAsync(new GetHistoricProcessInstanceCmd(processInstanceId), cancellationToken);
    }

    public Task<List<HistoricTaskInstance>> GetHistoricTaskInstancesAsync(CancellationToken cancellationToken = default)
        => CommandExecutor.ExecuteAsync(new GetHistoricTaskInstancesCmd(), cancellationToken);

    public Task<List<HistoricActivityInstance>> GetHistoricActivityInstancesAsync(CancellationToken cancellationToken = default)
        => CommandExecutor.ExecuteAsync(new GetHistoricActivityInstancesCmd(), cancellationToken);

    public Task<List<HistoricVariableInstance>> GetHistoricVariableInstancesAsync(CancellationToken cancellationToken = default)
        => CommandExecutor.ExecuteAsync(new GetHistoricVariableInstancesCmd(), cancellationToken);

    public async Task<List<HistoricDetail>> GetHistoricDetailsAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");
        return await CommandExecutor.ExecuteAsync(new GetHistoricDetailsCmd(processInstanceId), cancellationToken);
    }

    public async Task<List<HistoricIdentityLink>> GetHistoricIdentityLinksAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");
        return await CommandExecutor.ExecuteAsync(new GetHistoricIdentityLinksCmd(processInstanceId), cancellationToken);
    }

    public IHistoricProcessInstanceQuery CreateHistoricProcessInstanceQuery()
        => new QueryNs.HistoricProcessInstanceQueryImpl(CommandExecutor.Execute(new GetHistoricProcessInstancesCmd()));

    public IHistoricTaskInstanceQuery CreateHistoricTaskInstanceQuery()
        => new QueryNs.HistoricTaskInstanceQueryImpl(CommandExecutor.Execute(new GetHistoricTaskInstancesCmd()));

    public IHistoricActivityInstanceQuery CreateHistoricActivityInstanceQuery()
        => new QueryNs.HistoricActivityInstanceQueryImpl(CommandExecutor.Execute(new GetHistoricActivityInstancesCmd()));

    public IHistoricVariableInstanceQuery CreateHistoricVariableInstanceQuery()
        => new QueryNs.HistoricVariableInstanceQueryImpl(CommandExecutor.Execute(new GetHistoricVariableInstancesCmd()));

    public IHistoricDetailQuery CreateHistoricDetailQuery()
        => new QueryNs.HistoricDetailQueryImpl(
            CommandExecutor.Execute(new GetHistoricVariableInstancesCmd())
                .ConvertAll(record => new HistoricDetail
                {
                    Id = record.Id,
                    Type = "VariableUpdate",
                    ProcessInstanceId = record.ProcessInstanceId,
                    VariableName = record.Name,
                    VariableValue = record.Value,
                    Time = record.CreateTime,
                    TaskId = record.TaskId
                }));

    public IProcessInstanceHistoryLogQuery CreateProcessInstanceHistoryLogQuery(string processInstanceId)
        => new QueryNs.ProcessInstanceHistoryLogQueryImpl(ProcessEngineConfiguration?.CommandExecutor, processInstanceId).IncludeActivities();

    public Task DeleteHistoricProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
        => CommandExecutor.ExecuteAsync(new DeleteHistoricProcessInstanceCmd(processInstanceId), cancellationToken);

    public Task DeleteHistoricTaskInstanceAsync(string taskId, CancellationToken cancellationToken = default)
        => CommandExecutor.ExecuteAsync(new DeleteHistoricTaskInstanceCmd(taskId), cancellationToken);

    public async Task<List<IdentityLinkEntity>> GetHistoricIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
            throw new WorkflowEngineArgumentException("processInstanceId is null");
        return await CommandExecutor.ExecuteAsync(new GetHistoricIdentityLinksForProcessInstanceCmd(processInstanceId), cancellationToken);
    }

    public async Task<List<IdentityLinkEntity>> GetHistoricIdentityLinksForTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new WorkflowEngineArgumentException("taskId is null");
        return await CommandExecutor.ExecuteAsync(new GetHistoricIdentityLinksForTaskCmd(taskId), cancellationToken);
    }

    private IWorkflowPersistenceStore ResolveWorkflowPersistenceStore()
    {
        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(ProcessEngineConfiguration);
        if (store == null)
            throw new WorkflowEngineException("Workflow persistence store is not configured.");
        return store;
    }
}
