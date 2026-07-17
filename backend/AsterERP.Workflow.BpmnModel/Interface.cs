using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class Interface : BaseElement
{
    public string? Name { get; set; }
    public List<Operation> Operations { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new Interface { Id = Id, Name = Name };
        clone.Operations.AddRange(Operations.Select(o => (Operation)o.Clone()));
        return clone;
    }
}

