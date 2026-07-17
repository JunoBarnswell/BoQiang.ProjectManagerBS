using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public class ConditionalEventDefinition : EventDefinition
{
    public string? Condition { get; set; }
    public string? ConditionExpression { get; set; }

    public override BaseElement Clone()
    {
        return new ConditionalEventDefinition
        {
            Id = Id,
            Condition = Condition,
            ConditionExpression = ConditionExpression
        };
    }
}

