using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Job;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class HandleFailedJobCmd : ICommand<object?>
{
    private readonly string _jobId;
    private readonly string? _exceptionMessage;

    public HandleFailedJobCmd(string jobId, string? exceptionMessage)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new WorkflowEngineArgumentException("jobId is null");

        _jobId = jobId;
        _exceptionMessage = exceptionMessage;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        await JobRetryExecution.ExecuteAsync(context, _jobId, _exceptionMessage, cancellationToken);

        JobCommandEvents.DispatchEntityEvent(context, WorkflowEventType.JOB_EXECUTION_FAILURE, new { JobId = _jobId, ExceptionMessage = _exceptionMessage });
        return null;
    }
}

