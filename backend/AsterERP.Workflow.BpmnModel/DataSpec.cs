using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class DataSpec : BaseElement
{
    public string? Name { get; set; }
    public string? ItemSubjectRef { get; set; }
    public bool IsCollection { get; set; }

    public override BaseElement Clone()
    {
        return new DataSpec
        {
            Id = Id,
            Name = Name,
            ItemSubjectRef = ItemSubjectRef,
            IsCollection = IsCollection
        };
    }
}

