using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AsterERP.Workflow.BpmnModel;

public class TextAnnotation : Artifact
{
    public string? Text { get; set; }

    public override BaseElement Clone()
    {
        return new TextAnnotation { Id = Id, Text = Text };
    }
}

