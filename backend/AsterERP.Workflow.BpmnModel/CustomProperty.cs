using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class CustomProperty : BaseElement
{
    public string? Name { get; set; }
    public string? Value { get; set; }

    public override BaseElement Clone()
    {
        return new CustomProperty { Id = Id, Name = Name, Value = Value };
    }
}

