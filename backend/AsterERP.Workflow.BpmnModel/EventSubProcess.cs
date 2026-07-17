using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class EventSubProcess : SubProcess
{
    public override BaseElement Clone()
    {
        var clone = new EventSubProcess
        {
            Id = Id,
            Name = Name,
            TriggeredByEvent = TriggeredByEvent,
            DefaultFlow = DefaultFlow
        };
        clone.FlowElements.AddRange(FlowElements.Select(fe => (FlowElement)fe.Clone()));
        return clone;
    }
}

