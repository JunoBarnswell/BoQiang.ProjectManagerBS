using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Cmd;

public class CreateProcessInstanceCmd : ICommand<ProcessInstanceResult>
{
    private readonly string? _processDefinitionKey;
    private readonly string? _processDefinitionId;
    private readonly string? _businessKey;
    private readonly Dictionary<string, object?>? _variables;
    private readonly string? _tenantId;
    public CreateProcessInstanceCmd(
        string? processDefinitionKey,
        string? processDefinitionId,
        string? businessKey,
        Dictionary<string, object?>? variables)
    {
        _processDefinitionKey = processDefinitionKey;
        _processDefinitionId = processDefinitionId;
        _businessKey = businessKey;
        _variables = variables;
    }

    public CreateProcessInstanceCmd(
        string? processDefinitionKey,
        string? processDefinitionId,
        string? businessKey,
        Dictionary<string, object?>? variables,
        string? tenantId) : this(processDefinitionKey, processDefinitionId, businessKey, variables)
    {
        _tenantId = tenantId;
    }

    public ProcessInstanceResult Execute(ICommandContext context)
    {
        var processDefinition = ResolveProcessDefinition(context);

        var execution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid("N"),
            ProcessDefinitionId = processDefinition.Id,
            ProcessInstanceId = AbpTimeIdProvider.NewGuid("N"),
            IsActive = false,
            IsEnded = false,
            IsScope = true,
            IsConcurrent = false,
            IsProcessInstanceType = true,
            BusinessKey = _businessKey,
            Variables = new Dictionary<string, object?>()
        };

        if (_variables != null)
        {
            foreach (var kvp in _variables)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        context.AddExecution(execution);

        return new ProcessInstanceResult
        {
            Id = execution.Id,
            ProcessDefinitionId = processDefinition.Id,
            ProcessDefinitionKey = processDefinition.Key,
            BusinessKey = _businessKey,
            ProcessInstanceId = execution.ProcessInstanceId ?? execution.Id,
            IsStarted = false,
            IsEnded = false,
            TenantId = _tenantId,
            Name = null
        };
    }

    public async Task<ProcessInstanceResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Execute(context);
    }

    private Services.ProcessDefinitionRecord ResolveProcessDefinition(ICommandContext context)
    {
        if (!string.IsNullOrEmpty(_processDefinitionId))
        {
            var def = context.ProcessEngineConfiguration.CommandExecutor.Execute(
                new GetProcessDefinitionByIdCmd(_processDefinitionId));
            if (def != null) return def;
        }

        if (!string.IsNullOrEmpty(_processDefinitionKey))
        {
            var defs = context.ProcessEngineConfiguration.CommandExecutor.Execute(
                new GetProcessDefinitionsCmd());
            var latest = defs.FirstOrDefault(d => d.Key == _processDefinitionKey);
            if (latest != null) return latest;
        }

        throw new WorkflowEngineObjectNotFoundException(
            $"No process definition found for key '{_processDefinitionKey}' or id '{_processDefinitionId}'");
    }
}

