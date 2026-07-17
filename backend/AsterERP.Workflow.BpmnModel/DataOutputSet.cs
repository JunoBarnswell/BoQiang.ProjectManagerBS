using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class DataOutputSet : BaseElement
{
    public List<string> DataOutputRefs { get; set; } = new();

    public override BaseElement Clone()
    {
        var clone = new DataOutputSet { Id = Id };
        clone.DataOutputRefs.AddRange(DataOutputRefs);
        return clone;
    }
}
