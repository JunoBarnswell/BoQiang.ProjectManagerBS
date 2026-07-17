using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Helper;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class ErrorBoundaryEventActivityBehavior : BoundaryEventActivityBehavior
{
    public string? ErrorCode { get; set; }

    public ErrorBoundaryEventActivityBehavior() { }

    public ErrorBoundaryEventActivityBehavior(string? errorCode, bool interrupting = true)
        : base(interrupting)
    {
        ErrorCode = errorCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_errorBoundarySubscription", true);
        execution.SetVariableLocal("_errorBoundaryActive", true);
        if (!string.IsNullOrEmpty(ErrorCode))
        {
            execution.SetVariableLocal("_errorBoundaryErrorCode", ErrorCode);
        }
    }

    public virtual async Task CatchErrorAsync(ExecutionEntity execution, string? errorCode, CancellationToken cancellationToken = default)
    {
        if (MatchesError(errorCode))
        {
            execution.SetVariableLocal("_errorBoundaryActive", false);
            execution.SetVariable("_caughtErrorCode", errorCode);

            if (Interrupting)
            {
                await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
            }
            else
            {
                await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
            }
        }
    }

    protected bool MatchesError(string? errorCode)
    {
        if (string.IsNullOrEmpty(ErrorCode))
        {
            return true;
        }
        return ErrorCode == errorCode;
    }
}

public class ErrorEndEventActivityBehavior : EndEventActivityBehavior
{
    public string? ErrorCode { get; set; }

    public ErrorEndEventActivityBehavior() { }

    public ErrorEndEventActivityBehavior(string? errorCode)
    {
        ErrorCode = errorCode;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        execution.IsEnded = true;
        execution.SetVariable("_errorThrown", true);
        if (!string.IsNullOrEmpty(ErrorCode))
        {
            execution.SetVariable("_errorCode", ErrorCode);
        }
        execution.EventName = "error";

        try
        {
            ErrorPropagation.PropagateError(ErrorCode, execution);
        }
        catch (Common.WorkflowEngineException)
        {
        }
    }
}
