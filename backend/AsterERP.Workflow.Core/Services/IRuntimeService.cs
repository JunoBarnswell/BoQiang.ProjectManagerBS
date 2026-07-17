using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Cmd;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Variable;

namespace AsterERP.Workflow.Core.Services;

public interface IRuntimeService
{
    Task<string> StartProcessInstanceByKeyAsync(string processDefinitionKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<string> StartProcessInstanceByKeyAsync(string processDefinitionKey, string? businessKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<Execution.ExecutionEntity> StartProcessInstanceAndGetExecutionAsync(
        string processDefinitionKey,
        string? businessKey,
        Dictionary<string, object?>? variables = null,
        string? tenantId = null,
        Execution.ExecutionEntity? superExecution = null,
        CancellationToken cancellationToken = default);
    Task<string> StartProcessInstanceByKeyAndTenantIdAsync(string processDefinitionKey, string tenantId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<string> StartProcessInstanceByIdAsync(string processDefinitionId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<string> StartProcessInstanceByIdAsync(string processDefinitionId, string? businessKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<string> StartProcessInstanceByMessageAsync(string messageName, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<string> StartProcessInstanceByMessageAsync(string messageName, string? businessKey, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task<string> StartProcessInstanceByMessageAndTenantIdAsync(string messageName, string tenantId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task DeleteProcessInstanceAsync(string processInstanceId, string? deleteReason = null, CancellationToken cancellationToken = default);
    Task SignalAsync(string executionId, Dictionary<string, object?>? processVariables = null, CancellationToken cancellationToken = default);
    Task TriggerAsync(string executionId, Dictionary<string, object?>? processVariables = null, CancellationToken cancellationToken = default);
    Task TriggerAsync(string executionId, Dictionary<string, object?>? processVariables, Dictionary<string, object?>? transientVariables, CancellationToken cancellationToken = default);
    Task SignalEventReceivedAsync(string signalName, string? executionId = null, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task SignalEventReceivedAsync(string signalName, Dictionary<string, object?>? processVariables, CancellationToken cancellationToken = default);
    Task MessageEventReceivedAsync(string messageName, string executionId, Dictionary<string, object?>? variables = null, CancellationToken cancellationToken = default);
    Task SuspendProcessInstanceByIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task ActivateProcessInstanceByIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<object?> GetVariableAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task<T?> GetVariableAsync<T>(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task<object?> GetVariableLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task<VariableInstanceEntity?> GetVariableInstanceAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task<VariableInstanceEntity?> GetVariableInstanceLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object?>> GetVariablesAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object?>> GetVariablesLocalAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, VariableInstanceEntity>> GetVariableInstancesAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, VariableInstanceEntity>> GetVariableInstancesLocalAsync(string executionId, ICollection<string>? variableNames = null, CancellationToken cancellationToken = default);
    Task<bool> HasVariableAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task<bool> HasVariableLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task SetVariableAsync(string executionId, string variableName, object? variableValue, CancellationToken cancellationToken = default);
    Task SetVariableLocalAsync(string executionId, string variableName, object? value, CancellationToken cancellationToken = default);
    Task SetVariablesAsync(string executionId, Dictionary<string, object?> variables, CancellationToken cancellationToken = default);
    Task SetVariablesLocalAsync(string executionId, Dictionary<string, object?> variables, CancellationToken cancellationToken = default);
    Task RemoveVariableAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task RemoveVariableLocalAsync(string executionId, string variableName, CancellationToken cancellationToken = default);
    Task RemoveVariablesAsync(string executionId, ICollection<string> variableNames, CancellationToken cancellationToken = default);
    Task RemoveVariablesLocalAsync(string executionId, ICollection<string> variableNames, CancellationToken cancellationToken = default);
    Task<ExecutionRecord?> GetExecutionAsync(string executionId, CancellationToken cancellationToken = default);
    Task<List<ExecutionRecord>> GetExecutionsAsync(CancellationToken cancellationToken = default);
    Task<List<VariableInstanceRecord>> GetVariableInstancesAsync(string? executionId = null, CancellationToken cancellationToken = default);
    Task SetProcessInstanceNameAsync(string processInstanceId, string name, CancellationToken cancellationToken = default);
    Task SetProcessInstanceBusinessKeyAsync(string processInstanceId, string businessKey, CancellationToken cancellationToken = default);
    Task<List<string>> GetActiveActivityIdsAsync(string executionId, CancellationToken cancellationToken = default);
    Task AddUserIdentityLinkAsync(string processInstanceId, string userId, string identityLinkType, CancellationToken cancellationToken = default);
    Task AddGroupIdentityLinkAsync(string processInstanceId, string groupId, string identityLinkType, CancellationToken cancellationToken = default);
    Task DeleteUserIdentityLinkAsync(string processInstanceId, string userId, string identityLinkType, CancellationToken cancellationToken = default);
    Task DeleteGroupIdentityLinkAsync(string processInstanceId, string groupId, string identityLinkType, CancellationToken cancellationToken = default);
    Task<List<IdentityLinkEntity>> GetIdentityLinksForProcessInstanceAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<List<EventEntity>> GetProcessInstanceEventsAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task AddEventListenerAsync(IWorkflowEventListener listener, CancellationToken cancellationToken = default);
    Task RemoveEventListenerAsync(IWorkflowEventListener listener, CancellationToken cancellationToken = default);
    Task DispatchEventAsync(IWorkflowEvent evt, CancellationToken cancellationToken = default);
}
