using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class DataInputSet : BaseElement
{
    public List<string> DataInputRefs { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new DataInputSet { Id = Id };
        clone.DataInputRefs.AddRange(DataInputRefs);
        return clone;
    }
}

