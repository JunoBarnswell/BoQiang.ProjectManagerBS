using System.Collections.Generic;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public class VariablesPropagator
{
    public IVariablesCalculator Calculator { get; }

    public VariablesPropagator(IVariablesCalculator calculator)
    {
        Calculator = calculator;
    }

    public void Propagate(ExecutionEntity execution, Dictionary<string, object> availableVariables)
    {
        if (availableVariables == null || availableVariables.Count == 0) return;

        if (execution.Parent != null && IsMultiInstanceRoot(execution.Parent))
        {
            foreach (var kv in availableVariables)
            {
                execution.SetVariableLocal(kv.Key, kv.Value);
            }
        }
        else if (execution.ProcessInstanceId != null)
        {
            var outputVariables = Calculator.CalculateOutPutVariables(
                MappingExecutionContext.BuildMappingExecutionContext(execution),
                availableVariables);

            if (outputVariables.Count > 0)
            {
                foreach (var kv in outputVariables)
                {
                    execution.SetVariable(kv.Key, kv.Value);
                }
            }
        }
    }

    private bool IsMultiInstanceRoot(ExecutionEntity execution)
    {
        var loopCounter = execution.GetVariableLocal("loopCounter");
        var nrOfInstances = execution.GetVariableLocal(MultiInstanceActivityBehavior.NumberOfInstances);
        return nrOfInstances != null;
    }
}
