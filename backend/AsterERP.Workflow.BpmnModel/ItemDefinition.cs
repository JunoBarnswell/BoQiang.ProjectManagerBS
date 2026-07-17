using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class ItemDefinition : BaseElement
{
    public string? StructureRef { get; set; }
    public bool IsCollection { get; set; }
    public string? ItemKind { get; set; }

    public override BaseElement Clone()
    {
        return new ItemDefinition
        {
            Id = Id,
            StructureRef = StructureRef,
            IsCollection = IsCollection,
            ItemKind = ItemKind
        };
    }
}

