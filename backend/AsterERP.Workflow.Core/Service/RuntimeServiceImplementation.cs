using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Core.Variable;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Core.Service;

public class RuntimeServiceImplementation : ServiceImpl, IRuntimeService
{
    public RuntimeServiceImplementation() : base(AsterERP.Workflow.Core.Engine.ProcessEngineConfiguration.CreateDefault()) { }

    public RuntimeServiceImplementation(IProcessEngineConfiguration processEngineConfiguration)
        : base(processEngineConfiguration) { }

    public RuntimeServiceImplementation(ICommandExecutor commandExecutor)
        : base(commandExecutor) { }

    public async Task<string> StartProcessInstanceByKeyAsync(string processDefinitionKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceCmd(processDefinitionKey, null, null, variables, null), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task<string> StartProcessInstanceByKeyAsync(string processDefinitionKey, string? businessKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceCmd(processDefinitionKey, null, businessKey, variables, null), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task<ExecutionEntity> StartProcessInstanceAndGetExecutionAsync(
        string processDefinitionKey,
        string? businessKey,
        Dictionary<string, object?>? variables = null,
        string? tenantId = null,
        ExecutionEntity? superExecution = null,
        CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceCmd(
                processDefinitionKey,
                null,
                businessKey,
                variables,
                tenantId,
                superExecution),
            cancellationToken);
        return result?.Execution
            ?? throw new AsterERP.Workflow.Common.WorkflowEngineException(
                $"Started process '{processDefinitionKey}', but no execution entity was recorded");
    }

    public async Task<string> StartProcessInstanceByKeyAndTenantIdAsync(string processDefinitionKey, string tenantId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceCmd(processDefinitionKey, null, null, variables, tenantId), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task<string> StartProcessInstanceByIdAsync(string processDefinitionId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceCmd(null, processDefinitionId, null, variables, null), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task<string> StartProcessInstanceByIdAsync(string processDefinitionId, string? businessKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceCmd(null, processDefinitionId, businessKey, variables, null), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task<string> StartProcessInstanceByMessageAsync(string messageName, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceByMessageCmd(messageName, null, variables, null), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task<string> StartProcessInstanceByMessageAsync(string messageName, string? businessKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceByMessageCmd(messageName, businessKey, variables, null), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task<string> StartProcessInstanceByMessageAndTenantIdAsync(string messageName, string tenantId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var result = await CommandExecutor.ExecuteAsync(
            new StartProcessInstanceByMessageCmd(messageName, null, variables, tenantId), cancellationToken);
        if (result == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        return result.Id;
    }

    public async Task DeleteProcessInstanceAsync(string processInstanceId, string? deleteReason = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await CommandExecutor.ExecuteAsync(new DeleteProcessInstanceCmd(processInstanceId, deleteReason), cancellationToken);
        }
        catch (AsterERP.Workflow.Common.WorkflowEngineObjectNotFoundException)
        {
            // Keep runtime delete idempotent for non-existent instances.
        }
    }

    public async Task SignalAsync(string executionId, Dictionary<string, object?>? processVariables = null, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new TriggerCmd(executionId, processVariables), cancellationToken);
    }

    public async Task TriggerAsync(string executionId, Dictionary<string, object?>? processVariables = null, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new TriggerCmd(executionId, processVariables), cancellationToken);
    }

    public async Task TriggerAsync(string executionId, Dictionary<string, object?>? processVariables, Dictionary<string, object?>? transientVariables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new TriggerWithTransientCmd(executionId, processVariables, transientVariables), cancellationToken);
    }

    public async Task SignalEventReceivedAsync(string signalName, string? executionId = null, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SignalEventReceivedCmd(signalName, executionId, variables), cancellationToken);
    }

    public async Task SignalEventReceivedAsync(string signalName, Dictionary<string, object?>? processVariables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SignalEventReceivedCmd(signalName, null, processVariables), cancellationToken);
    }

    public async Task MessageEventReceivedAsync(string messageName, string executionId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new MessageEventReceivedCmd(messageName, executionId, variables), cancellationToken);
    }

    public async Task SuspendProcessInstanceByIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SuspendProcessInstanceCmd(processInstanceId), cancellationToken);
    }

    public async Task ActivateProcessInstanceByIdAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new ActivateProcessInstanceCmd(processInstanceId), cancellationToken);
    }

    public async Task<object?> GetVariableAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariableCmd(executionId, variableName, false), cancellationToken);
    }

    public async Task<T?> GetVariableAsync<T>(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        var value = await GetVariableAsync(executionId, variableName, cancellationToken);
        if (value == null) return default;
        return (T)value;
    }

    public async Task<object?> GetVariableLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariableCmd(executionId, variableName, true), cancellationToken);
    }

    public async Task<VariableInstanceEntity?> GetVariableInstanceAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariableInstanceCmd(executionId, variableName, false), cancellationToken);
    }

    public async Task<VariableInstanceEntity?> GetVariableInstanceLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariableInstanceCmd(executionId, variableName, true), cancellationToken);
    }

    public async Task<Dictionary<string, object?>> GetVariablesAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariablesCmd(executionId, false), cancellationToken);
    }

    public async Task<Dictionary<string, object?>> GetVariablesLocalAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariablesCmd(executionId, true), cancellationToken);
    }

    public async Task<Dictionary<string, VariableInstanceEntity>> GetVariableInstancesAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariableInstancesCmd(executionId, variableNames, false), cancellationToken);
    }

    public async Task<Dictionary<string, VariableInstanceEntity>> GetVariableInstancesLocalAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetExecutionVariableInstancesCmd(executionId, variableNames, true), cancellationToken);
    }

    public async Task<bool> HasVariableAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new HasExecutionVariableCmd(executionId, variableName, false), cancellationToken);
    }

    public async Task<bool> HasVariableLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new HasExecutionVariableCmd(executionId, variableName, true), cancellationToken);
    }

    public async Task SetVariableAsync(string executionId, string variableName, object? value, CancellationToken cancellationToken = default)
    {
        if (variableName == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("variableName is null");
        var variables = new Dictionary<string, object?> { [variableName] = value };
        await CommandExecutor.ExecuteAsync(new SetExecutionVariablesCmd(executionId, variables, false), cancellationToken);
    }

    public async Task SetVariableLocalAsync(string executionId, string variableName, object? value, CancellationToken cancellationToken = default)
    {
        if (variableName == null)
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("variableName is null");
        var variables = new Dictionary<string, object?> { [variableName] = value };
        await CommandExecutor.ExecuteAsync(new SetExecutionVariablesCmd(executionId, variables, true), cancellationToken);
    }

    public async Task SetVariablesAsync(string executionId, Dictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetExecutionVariablesCmd(executionId, variables, false), cancellationToken);
    }

    public async Task SetVariablesLocalAsync(string executionId, Dictionary<string, object?> variables, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetExecutionVariablesCmd(executionId, variables, true), cancellationToken);
    }

    public async Task RemoveVariableAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        var variableNames = new List<string> { variableName };
        await CommandExecutor.ExecuteAsync(new RemoveExecutionVariablesCmd(executionId, variableNames, false), cancellationToken);
    }

    public async Task RemoveVariableLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default)
    {
        var variableNames = new List<string> { variableName };
        await CommandExecutor.ExecuteAsync(new RemoveExecutionVariablesCmd(executionId, variableNames, true), cancellationToken);
    }

    public async Task RemoveVariablesAsync(string executionId, ICollection<string> variableNames, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new RemoveExecutionVariablesCmd(executionId, variableNames, false), cancellationToken);
    }

    public async Task RemoveVariablesLocalAsync(string executionId, ICollection<string> variableNames, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new RemoveExecutionVariablesCmd(executionId, variableNames, true), cancellationToken);
    }

    public async Task<ExecutionRecord?> GetExecutionAsync(string executionId, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetExecutionByIdCmd(executionId), cancellationToken);
    }

    public async Task<List<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetAllExecutionsCmd(), cancellationToken);
    }

    public async Task<List<VariableInstanceRecord>> GetVariableInstancesAsync(string? executionId = null, CancellationToken cancellationToken = default)
    {
return await CommandExecutor.ExecuteAsync(new GetVariableInstancesByExecutionCmd(executionId), cancellationToken);
    }

    public async Task SetProcessInstanceNameAsync(string processInstanceId, string name, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetProcessInstanceNameCmd(processInstanceId, name), cancellationToken);
    }

    public async Task SetProcessInstanceBusinessKeyAsync(string processInstanceId, string businessKey, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new SetProcessInstanceBusinessKeyCmd(processInstanceId, businessKey), cancellationToken);
    }

    public async Task<List<string>> GetActiveActivityIdsAsync(string executionId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new FindActiveActivityIdsCmd(executionId), cancellationToken);
    }

    public async Task AddUserIdentityLinkAsync(string processInstanceId, string userId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkForProcessInstanceCmd(processInstanceId, userId, null, identityLinkType), cancellationToken);
    }

    public async Task AddGroupIdentityLinkAsync(string processInstanceId, string groupId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new AddIdentityLinkForProcessInstanceCmd(processInstanceId, null, groupId, identityLinkType), cancellationToken);
    }

    public async Task DeleteUserIdentityLinkAsync(string processInstanceId, string userId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new DeleteIdentityLinkForProcessInstanceCmd(processInstanceId, userId, null, identityLinkType), cancellationToken);
    }

    public async Task DeleteGroupIdentityLinkAsync(string processInstanceId, string groupId, string identityLinkType, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(
            new DeleteIdentityLinkForProcessInstanceCmd(processInstanceId, null, groupId, identityLinkType), cancellationToken);
    }

    public async Task<List<IdentityLinkEntity>> GetIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("processInstanceId is null");
        return await CommandExecutor.ExecuteAsync(
            new GetIdentityLinksForProcessInstanceCmd(processInstanceId),
            cancellationToken);
    }

    public async Task<List<EventEntity>> GetProcessInstanceEventsAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(new GetProcessInstanceEventsCmd(processInstanceId), cancellationToken);
    }

    public async Task AddEventListenerAsync(IWorkflowEventListener listener, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new AddEventListenerCommand(listener), cancellationToken);
    }

    public async Task RemoveEventListenerAsync(IWorkflowEventListener listener, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new RemoveEventListenerCommand(listener), cancellationToken);
    }

    public async Task DispatchEventAsync(IWorkflowEvent evt, CancellationToken cancellationToken = default)
    {
        await CommandExecutor.ExecuteAsync(new DispatchEventCommand(evt), cancellationToken);
    }

    public ProcessInstanceBuilder CreateProcessInstanceBuilder()
    {
        return new ProcessInstanceBuilder(this);
    }

    public async Task<ExecutionEntity?> FindExecutionEntityByProcessInstanceIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        return await CommandExecutor.ExecuteAsync(
            new GetExecutionEntityByProcessInstanceIdCmd(processInstanceId), cancellationToken);
    }

    private class GetExecutionEntityByProcessInstanceIdCmd : ICommand<ExecutionEntity?>
    {
        private readonly string _processInstanceId;
        public GetExecutionEntityByProcessInstanceIdCmd(string processInstanceId)
        {
            _processInstanceId = processInstanceId;
        }

        public ExecutionEntity? Execute(ICommandContext context) =>
            throw new NotSupportedException("GetExecutionEntityByProcessInstanceIdCmd is async-only. Use ExecuteAsync.");

        public async Task<ExecutionEntity?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default) =>
            await context.GetCurrentExecutionAsync(_processInstanceId, cancellationToken);
    }
}

public class ProcessInstanceBuilder
{
    private readonly RuntimeServiceImplementation _runtimeService;
    public string? ProcessDefinitionId { get; private set; }
    public string? ProcessDefinitionKey { get; private set; }
    public string? BusinessKey { get; private set; }
    public string? MessageName { get; private set; }
    public string? TenantId { get; private set; }
    public string? ProcessInstanceName { get; private set; }
    public Dictionary<string, object?> Variables { get; private set; } = new();

    public ProcessInstanceBuilder(RuntimeServiceImplementation runtimeService)
    {
        _runtimeService = runtimeService;
    }

    public ProcessInstanceBuilder WithProcessDefinitionId(string processDefinitionId)
    {
        ProcessDefinitionId = processDefinitionId;
        return this;
    }

    public ProcessInstanceBuilder WithProcessDefinitionKey(string processDefinitionKey)
    {
        ProcessDefinitionKey = processDefinitionKey;
        return this;
    }

    public ProcessInstanceBuilder WithBusinessKey(string businessKey)
    {
        BusinessKey = businessKey;
        return this;
    }

    public ProcessInstanceBuilder WithMessageName(string messageName)
    {
        MessageName = messageName;
        return this;
    }

    public ProcessInstanceBuilder WithTenantId(string tenantId)
    {
        TenantId = tenantId;
        return this;
    }

    public ProcessInstanceBuilder WithName(string name)
    {
        ProcessInstanceName = name;
        return this;
    }

    public ProcessInstanceBuilder WithVariables(Dictionary<string, object?> variables)
    {
        Variables = variables;
        return this;
    }

    public ProcessInstanceBuilder AddVariable(string name, object? value)
    {
        Variables[name] = value;
        return this;
    }

    public bool HasProcessDefinitionIdOrKey()
    {
        return !string.IsNullOrEmpty(ProcessDefinitionId) || !string.IsNullOrEmpty(ProcessDefinitionKey);
    }

    public async Task<string> StartAsync(CancellationToken cancellationToken = default)
    {
        if (HasProcessDefinitionIdOrKey())
        {
            var result = await _runtimeService.CommandExecutor.ExecuteAsync(
                new StartProcessInstanceCmd(ProcessDefinitionKey, ProcessDefinitionId, BusinessKey, Variables, TenantId), cancellationToken);
            return result?.Id ?? throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        }
        else if (MessageName != null)
        {
            var result = await _runtimeService.CommandExecutor.ExecuteAsync(
                new StartProcessInstanceByMessageCmd(MessageName, BusinessKey, Variables, TenantId), cancellationToken);
            return result?.Id ?? throw new AsterERP.Workflow.Common.WorkflowEngineException("Failed to start process instance");
        }
        else
        {
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException(
                "No processDefinitionId, processDefinitionKey nor messageName provided");
        }
    }
}

