using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class EscalationEventDefinition : EventDefinition
{
    public string? EscalationRef { get; set; }
    public string? EscalationCode { get; set; }

    public override BaseElement Clone()
    {
        return new EscalationEventDefinition
        {
            Id = Id,
            EscalationRef = EscalationRef,
            EscalationCode = EscalationCode
        };
    }
}

