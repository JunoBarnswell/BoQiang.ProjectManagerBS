using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class CancelEventDefinition : EventDefinition
{
    public override BaseElement Clone()
    {
        return new CancelEventDefinition { Id = Id };
    }
}

