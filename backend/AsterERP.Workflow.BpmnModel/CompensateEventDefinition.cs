using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class CompensateEventDefinition : EventDefinition
{
    public bool WaitForCompletion { get; set; } = true;
    public string? ActivityRef { get; set; }

    public override BaseElement Clone()
    {
        return new CompensateEventDefinition
        {
            Id = Id,
            WaitForCompletion = WaitForCompletion,
            ActivityRef = ActivityRef
        };
    }
}

