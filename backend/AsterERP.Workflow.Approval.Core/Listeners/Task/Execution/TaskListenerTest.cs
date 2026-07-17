using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Event;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Approval.Core.Listeners.Task.Execution;

public class TaskListenerTest : IWorkflowEventListener
{
    private readonly ILogger<TaskListenerTest> _logger;

    public TaskListenerTest(ILogger<TaskListenerTest> logger)
    {
        _logger = logger;
    }

    public bool IsFailOnException => true;

    public void OnEvent(IWorkflowEvent @event)
    {
        if (@event is IWorkflowEntityEvent entityEvent && entityEvent.Entity is IDictionary<string, object> task)
        {
            var name = task.TryGetValue("name", out var n) ? n?.ToString() : "";
            _logger.LogError("执行了监听:{Name}", name);
        }
    }
}
