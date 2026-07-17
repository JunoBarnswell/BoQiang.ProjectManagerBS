using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class Escalation : BaseElement
{
    public string? Name { get; set; }
    public string? EscalationCode { get; set; }

    public override BaseElement Clone()
    {
        return new Escalation
        {
            Id = Id,
            Name = Name,
            EscalationCode = EscalationCode
        };
    }
}
