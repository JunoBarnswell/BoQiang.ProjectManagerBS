using AsterERP.Api.Modules.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Workflows.Callbacks;

public sealed class WorkflowCallbackExecutionException : BusinessException
{
    public WorkflowCallbackExecutionException(WorkflowCallbackLogEntity failureLog, Exception cause)
        : base(ErrorCodes.WorkflowActionInvalid, $"审批回调执行失败: {cause.Message}")
    {
        FailureLog = failureLog;
        Cause = cause;
    }

    public WorkflowCallbackLogEntity FailureLog { get; }

    public Exception Cause { get; }
}
