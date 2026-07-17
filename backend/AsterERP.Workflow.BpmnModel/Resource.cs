using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class Resource : BaseElement
{
    public string? Name { get; set; }

    public override BaseElement Clone()
    {
        return new Resource { Id = Id, Name = Name };
    }
}

