using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class DataStoreReference : BaseElement
{
    public string? Name { get; set; }
    public string? DataStoreRef { get; set; }
    public string? ItemSubjectRef { get; set; }

    public override BaseElement Clone()
    {
        return new DataStoreReference
        {
            Id = Id,
            Name = Name,
            DataStoreRef = DataStoreRef,
            ItemSubjectRef = ItemSubjectRef
        };
    }
}

