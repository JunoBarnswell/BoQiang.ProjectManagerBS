using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class LinkEventDefinition : EventDefinition
{
    public string? Name { get; set; }
    public List<string> Sources { get; set; } = new();
    public string? Target { get; set; }

    public override BaseElement Clone()
    {
        var clone = new LinkEventDefinition
        {
            Id = Id,
            Name = Name,
            Target = Target
        };
        clone.Sources.AddRange(Sources);
        return clone;
    }
}

