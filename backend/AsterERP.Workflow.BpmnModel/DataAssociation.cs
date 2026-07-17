using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class DataAssociation : BaseElement
{
    public string? SourceRef { get; set; }
    public string? TargetRef { get; set; }
    public string? Transformation { get; set; }
    public List<Assignment> Assignments { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new DataAssociation
        {
            Id = Id,
            SourceRef = SourceRef,
            TargetRef = TargetRef,
            Transformation = Transformation
        };
        clone.Assignments.AddRange(Assignments.Select(a => (Assignment)a.Clone()));
        return clone;
    }
}

