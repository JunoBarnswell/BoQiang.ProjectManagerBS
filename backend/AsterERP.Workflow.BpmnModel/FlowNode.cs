using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public abstract class FlowNode : FlowElement
{
    public List<SequenceFlow> IncomingFlows { get; set; } = new();
    public List<SequenceFlow> OutgoingFlows { get; set; } = new();

    [JsonIgnore]
    public object? Behavior { get; set; }

    public bool Asynchronous { get; set; }
    public bool Exclusive { get; set; } = true;
}
