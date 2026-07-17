using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class IOParameter : BaseElement
{
    public string? Target { get; set; }
    public string? Source { get; set; }
    public string? TargetExpression { get; set; }
    public string? SourceExpression { get; set; }

    public override BaseElement Clone()
    {
        return new IOParameter
        {
            Id = Id,
            Target = Target,
            Source = Source,
            TargetExpression = TargetExpression,
            SourceExpression = SourceExpression
        };
    }
}

