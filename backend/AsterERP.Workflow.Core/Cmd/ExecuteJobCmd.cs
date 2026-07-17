using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Cmd;

public class ExecuteJobCmd : ICommand<object?>
{
    private readonly string _jobId;

    public ExecuteJobCmd(string jobId)
    {
        _jobId = jobId ?? throw new ArgumentNullException(nameof(jobId));
    }

    public object? Execute(ICommandContext context)
    {
        if (string.IsNullOrEmpty(_jobId))
            throw new WorkflowEngineArgumentException("jobId is null");

        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager == null)
            throw new WorkflowEngineException("Job manager is not configured");

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_UPDATED,
                    new { JobId = _jobId, Action = "Execute" }));
        }

        return null;
    }

    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_jobId))
            throw new WorkflowEngineArgumentException("jobId is null");

        var jobManager = context.ProcessEngineConfiguration.JobManager;
        if (jobManager != null)
        {
            await jobManager.ExecuteJobAsync(_jobId, cancellationToken);
        }

        return null;
    }
}
