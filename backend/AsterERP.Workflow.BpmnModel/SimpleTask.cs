using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class SimpleTask : Activity
{
    public override BaseElement Clone()
    {
        var clone = new SimpleTask
        {
            Id = Id,
            Name = Name,
            DefaultFlow = DefaultFlow
        };
        return clone;
    }
}

