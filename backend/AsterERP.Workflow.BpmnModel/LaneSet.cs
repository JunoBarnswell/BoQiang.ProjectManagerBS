using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class LaneSet : BaseElement
{
    public string? Name { get; set; }
    public List<Lane> Lanes { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new LaneSet
        {
            Id = Id,
            Name = Name
        };
        clone.Lanes.AddRange(Lanes.Select(l => (Lane)l.Clone()));
        return clone;
    }
}

