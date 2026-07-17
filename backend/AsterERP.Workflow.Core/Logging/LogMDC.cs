using System;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Core.Logging;

public static class LogMDC
{
    public const string LOG_MDC_PROCESSDEFINITION_ID = "mdcProcessDefinitionID";
    public const string LOG_MDC_EXECUTION_ID = "mdcExecutionId";
    public const string LOG_MDC_PROCESSINSTANCE_ID = "mdcProcessInstanceID";
    public const string LOG_MDC_BUSINESS_KEY = "mdcBusinessKey";
    public const string LOG_MDC_TASK_ID = "mdcTaskId";

    private static bool _enabled;

    public static bool IsMDCEnabled => _enabled;

    public static void SetMDCEnabled(bool enabled)
    {
        _enabled = enabled;
    }

    public static void SetProcessInstanceId(string? processInstanceId)
    {
        if (processInstanceId != null)
        {
            System.Diagnostics.Activity.Current?.AddTag(LOG_MDC_PROCESSINSTANCE_ID, processInstanceId);
        }
    }

    public static void SetExecutionId(string? executionId)
    {
        if (executionId != null)
        {
            System.Diagnostics.Activity.Current?.AddTag(LOG_MDC_EXECUTION_ID, executionId);
        }
    }

    public static void SetTaskId(string? taskId)
    {
        if (taskId != null)
        {
            System.Diagnostics.Activity.Current?.AddTag(LOG_MDC_TASK_ID, taskId);
        }
    }

    public static void SetProcessDefinitionId(string? processDefinitionId)
    {
        if (processDefinitionId != null)
        {
            System.Diagnostics.Activity.Current?.AddTag(LOG_MDC_PROCESSDEFINITION_ID, processDefinitionId);
        }
    }

    public static void SetBusinessKey(string? businessKey)
    {
        if (businessKey != null)
        {
            System.Diagnostics.Activity.Current?.AddTag(LOG_MDC_BUSINESS_KEY, businessKey);
        }
    }

    public static void Clear()
    {
    }

    public static void Remove(string key)
    {
    }
}
