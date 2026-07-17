using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class TaskActivityBehavior : FlowNodeActivityBehavior
{
    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }

    public string? GetActiveValue(string? originalValue, string propertyName, Dictionary<string, object?>? taskElementProperties)
    {
        if (taskElementProperties == null) return originalValue;

        if (taskElementProperties.TryGetValue(propertyName, out var overrideValue))
        {
            return overrideValue?.ToString();
        }

        return originalValue;
    }

    public List<string>? GetActiveValueList(List<string>? originalValues, string propertyName, Dictionary<string, object?>? taskElementProperties)
    {
        if (taskElementProperties == null) return originalValues;

        if (taskElementProperties.TryGetValue(propertyName, out var overrideValue))
        {
            if (overrideValue == null) return null;

            if (overrideValue is List<string> list) return list;

            if (overrideValue is string str)
            {
                return str.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        return originalValues;
    }
}
