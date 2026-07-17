using System;
using System.Collections.Generic;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Helper;

public class SubProcessVariableSnapshotter
{
    public Dictionary<string, object?> CreateSnapshot(ExecutionEntity sourceExecution)
    {
        var snapshot = new Dictionary<string, object?>();

        foreach (var variable in sourceExecution.Variables)
        {
            snapshot[variable.Key] = CloneValue(variable.Value);
        }

        return snapshot;
    }

    public void SetVariablesSnapshots(ExecutionEntity sourceExecution, ExecutionEntity targetExecution)
    {
        var snapshot = CreateSnapshot(sourceExecution);

        foreach (var variable in snapshot)
        {
            targetExecution.SetVariable(variable.Key, variable.Value);
        }
    }

    private object? CloneValue(object? value)
    {
        if (value == null) return null;

        if (value is string s) return s;
        if (value is int i) return i;
        if (value is long l) return l;
        if (value is double d) return d;
        if (value is bool b) return b;
        if (value is DateTime dt) return dt;
        if (value is decimal dec) return dec;
        if (value is float f) return f;

        if (value is Dictionary<string, object?> dict)
        {
            var clone = new Dictionary<string, object?>();
            foreach (var kvp in dict)
            {
                clone[kvp.Key] = CloneValue(kvp.Value);
            }
            return clone;
        }

        if (value is List<object?> list)
        {
            var clone = new List<object?>();
            foreach (var item in list)
            {
                clone.Add(CloneValue(item));
            }
            return clone;
        }

        return value;
    }
}
