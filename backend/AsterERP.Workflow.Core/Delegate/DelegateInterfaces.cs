using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Delegate;

public interface IDelegateExpression
{
    global::System.Threading.Tasks.Task ExecuteAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}

public interface IVariableScope
{
    Dictionary<string, object?> Variables { get; }
    object? GetVariable(string name);
    void SetVariable(string name, object? value);
    object? GetVariableLocal(string variableName);
    void SetVariableLocal(string variableName, object? value);
    bool HasVariable(string variableName);
    bool HasVariableLocal(string variableName);
    void RemoveVariable(string variableName);
    void RemoveVariableLocal(string variableName);
    void SetVariables(Dictionary<string, object?> variables);
    void SetVariablesLocal(Dictionary<string, object?> variables);
}

public interface IDelegateTask : IVariableScope
{
    string Id { get; }
    string? Name { get; set; }
    string? Description { get; set; }
    int Priority { get; set; }
    string? ProcessInstanceId { get; }
    string? ExecutionId { get; }
    string? ProcessDefinitionId { get; }
    DateTime? CreateTime { get; }
    string? TaskDefinitionKey { get; }
    bool IsSuspended { get; }
    string? TenantId { get; }
    string? FormKey { get; set; }
    IDelegateExecution? Execution { get; }
    string? EventName { get; }
    string? Owner { get; set; }
    string? Assignee { get; set; }
    DateTime? DueDate { get; set; }
    string? Category { get; set; }
    void AddCandidateUser(string userId);
    void AddCandidateUsers(IEnumerable<string> candidateUsers);
    void AddCandidateGroup(string groupId);
    void AddCandidateGroups(IEnumerable<string> candidateGroups);
    void DeleteCandidateUser(string userId);
    void DeleteCandidateGroup(string groupId);
    void AddUserIdentityLink(string userId, string identityLinkType);
    void AddGroupIdentityLink(string groupId, string identityLinkType);
    void DeleteUserIdentityLink(string userId, string identityLinkType);
    void DeleteGroupIdentityLink(string groupId, string identityLinkType);
}

public interface IExpression
{
    string ExpressionText { get; }
    object? GetValue(IVariableScope variableScope);
    void SetValue(IVariableScope variableScope, object? value);
}

public interface IExecutionListener
{
    Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}

public interface ITaskListener
{
    Task NotifyAsync(IDelegateTask delegateTask, CancellationToken cancellationToken = default);
}

public interface ITransactionDependentExecutionListener
{
    const string ON_TRANSACTION_BEFORE_COMMIT = "before-commit";
    const string ON_TRANSACTION_COMMITTED = "committed";
    const string ON_TRANSACTION_ROLLED_BACK = "rolled-back";

    Task NotifyAsync(
        string processInstanceId,
        string executionId,
        string? activityId,
        Dictionary<string, object?>? executionVariables,
        Dictionary<string, object?>? customPropertiesMap,
        CancellationToken cancellationToken = default);
}

public interface ITransactionDependentTaskListener
{
    const string ON_TRANSACTION_COMMITTING = "before-commit";
    const string ON_TRANSACTION_COMMITTED = "committed";
    const string ON_TRANSACTION_ROLLED_BACK = "rolled-back";

    Task NotifyAsync(
        string processInstanceId,
        string taskId,
        Dictionary<string, object?>? executionVariables,
        Dictionary<string, object?>? customPropertiesMap,
        CancellationToken cancellationToken = default);
}

public class BpmnError : Exception
{
    public string ErrorCode { get; }

    public BpmnError(string errorCode) : base("")
    {
        if (string.IsNullOrEmpty(errorCode))
            throw new ArgumentException("Error Code must not be null or empty.");
        ErrorCode = errorCode;
    }

    public BpmnError(string errorCode, string message) : base(message)
    {
        if (string.IsNullOrEmpty(errorCode))
            throw new ArgumentException("Error Code must not be null or empty.");
        ErrorCode = errorCode;
    }
}

public interface ICustomPropertiesResolver
{
    Task<Dictionary<string, object?>> ResolveAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}

public interface IBusinessRuleTaskDelegate
{
    Task ExecuteAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
    void AddRuleVariableInputIdExpression(IExpression inputId);
    void AddRuleIdExpression(IExpression inputId);
    void SetExclude(bool exclude);
    void SetResultVariable(string resultVariableName);
}

public abstract class BaseTaskListener
{
    public const string EVENTNAME_CREATE = "create";
    public const string EVENTNAME_ASSIGNMENT = "assignment";
    public const string EVENTNAME_COMPLETE = "complete";
    public const string EVENTNAME_DELETE = "delete";
    public const string EVENTNAME_ALL_EVENTS = "all";

    public virtual Task NotifyAsync(IDelegateTask delegateTask, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public abstract class BaseExecutionListener
{
    public const string EVENTNAME_START = "start";
    public const string EVENTNAME_END = "end";
    public const string EVENTNAME_TAKE = "take";

    public virtual Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
