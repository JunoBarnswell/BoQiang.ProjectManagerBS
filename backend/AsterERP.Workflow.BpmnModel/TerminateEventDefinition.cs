using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class TerminateEventDefinition : EventDefinition
{
    public bool TerminateAll { get; set; }
    public bool TerminateMultiInstance { get; set; }

    public override BaseElement Clone()
    {
        return new TerminateEventDefinition
        {
            Id = Id,
            TerminateAll = TerminateAll,
            TerminateMultiInstance = TerminateMultiInstance
        };
    }
}

