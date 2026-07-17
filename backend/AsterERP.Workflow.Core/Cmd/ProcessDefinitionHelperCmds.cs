using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class GetProcessDefinitionByIdCmd : ICommand<ProcessDefinitionRecord?>
{
    private readonly string _processDefinitionId;

    public GetProcessDefinitionByIdCmd(string processDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new WorkflowEngineArgumentException("processDefinitionId is null");

        _processDefinitionId = processDefinitionId;
    }

    public ProcessDefinitionRecord? Execute(ICommandContext context)
    {
        var processDefinitions = context.ProcessEngineConfiguration.CommandExecutor.Execute(
            new GetProcessDefinitionsCmd());
        var processDefinition = processDefinitions.FirstOrDefault(processDefinition =>
            processDefinition.Id == _processDefinitionId);
        if (processDefinition == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition found for id = '{_processDefinitionId}'",
                typeof(ProcessDefinitionRecord));

        return processDefinition;
    }

    public Task<ProcessDefinitionRecord?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}

public class GetProcessDefinitionsCmd : ICommand<List<ProcessDefinitionRecord>>
{
    public List<ProcessDefinitionRecord> Execute(ICommandContext context)
    {
        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
        {
            return new List<ProcessDefinitionRecord>();
        }

        var entries = deploymentManager.GetProcessDefinitionCacheEntries();
        return entries.Select(entry => new ProcessDefinitionRecord
        {
            Id = entry.ProcessDefinition.Id,
            Key = entry.ProcessDefinition.Key,
            Name = entry.ProcessDefinition.Name,
            DeploymentId = entry.ProcessDefinition.DeploymentId,
            Version = entry.ProcessDefinition.Version,
            Category = entry.ProcessDefinition.Category,
            Description = entry.ProcessDefinition.Description,
            IsSuspended = entry.ProcessDefinition.IsSuspended,
            TenantId = entry.ProcessDefinition.TenantId,
            BpmnModelId = entry.ProcessDefinition.Id
        }).ToList();
    }

    public Task<List<ProcessDefinitionRecord>> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}
