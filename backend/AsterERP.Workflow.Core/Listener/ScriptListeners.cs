using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Listener;

public interface ITaskListener
{
    string? Event { get; }
    Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}

public interface IExecutionListener
{
    string? Event { get; }
    Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}

public class ScriptTaskListener : ITaskListener
{
    public string? Event { get; set; }
    public string? Script { get; set; }
    public string? ScriptFormat { get; set; }
    public bool AutoStoreVariables { get; set; }

    private readonly IExpressionManager? _expressionManager;

    public ScriptTaskListener() { }

    public ScriptTaskListener(
        string? @event = null,
        string? script = null,
        string? scriptFormat = null,
        bool autoStoreVariables = false,
        IExpressionManager? expressionManager = null)
    {
        Event = @event;
        Script = script;
        ScriptFormat = scriptFormat;
        AutoStoreVariables = autoStoreVariables;
        _expressionManager = expressionManager;
    }

    public async Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(Script)) return;

        var scriptToExecute = ResolveScript(execution);
        if (string.IsNullOrEmpty(scriptToExecute)) return;

        if (_expressionManager != null)
        {
            var result = _expressionManager.Evaluate(scriptToExecute, execution.Variables);
            if (AutoStoreVariables && result != null)
            {
                execution.SetVariable("_scriptTaskListenerResult", result);
            }
        }

        await Task.CompletedTask;
    }

    private string? ResolveScript(IDelegateExecution execution)
    {
        if (_expressionManager != null && (Script!.StartsWith("${") || Script.StartsWith("#{")))
        {
            var result = _expressionManager.Evaluate(Script, execution.Variables);
            return result?.ToString();
        }
        return Script;
    }
}

public class ScriptExecutionListener : IExecutionListener
{
    public string? Event { get; set; }
    public string? Script { get; set; }
    public string? ScriptFormat { get; set; }
    public bool AutoStoreVariables { get; set; }

    private readonly IExpressionManager? _expressionManager;

    public ScriptExecutionListener() { }

    public ScriptExecutionListener(
        string? @event = null,
        string? script = null,
        string? scriptFormat = null,
        bool autoStoreVariables = false,
        IExpressionManager? expressionManager = null)
    {
        Event = @event;
        Script = script;
        ScriptFormat = scriptFormat;
        AutoStoreVariables = autoStoreVariables;
        _expressionManager = expressionManager;
    }

    public async Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(Script)) return;

        var scriptToExecute = ResolveScript(execution);
        if (string.IsNullOrEmpty(scriptToExecute)) return;

        if (_expressionManager != null)
        {
            var result = _expressionManager.Evaluate(scriptToExecute, execution.Variables);
            if (AutoStoreVariables && result != null)
            {
                execution.SetVariable("_scriptExecutionListenerResult", result);
            }
        }

        await Task.CompletedTask;
    }

    private string? ResolveScript(IDelegateExecution execution)
    {
        if (_expressionManager != null && (Script!.StartsWith("${") || Script.StartsWith("#{")))
        {
            var result = _expressionManager.Evaluate(Script, execution.Variables);
            return result?.ToString();
        }
        return Script;
    }
}
