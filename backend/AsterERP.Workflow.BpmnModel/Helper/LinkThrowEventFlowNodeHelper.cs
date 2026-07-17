using System.Collections.Generic;

namespace AsterERP.Workflow.BpmnModel;

public static class LinkThrowEventFlowNodeHelper
{
    public static FlowNode? FindRelatedIntermediateCatchEventForLinkEvent(ThrowEvent throwEvent)
    {
        if (throwEvent.EventDefinitions.Count == 0)
            return null;

        if (throwEvent.EventDefinitions[0] is not LinkEventDefinition linkDef)
            return null;

        var linkEventTarget = linkDef.Name;
        var parentContainer = throwEvent.ParentContainer;
        if (parentContainer == null)
            return null;

        foreach (var flowElement in parentContainer.FlowElements)
        {
            if (flowElement is IntermediateCatchEvent intermediateCatchEvent)
            {
                if (intermediateCatchEvent.EventDefinitions.Count > 0 &&
                    intermediateCatchEvent.EventDefinitions[0] is LinkEventDefinition destinationEvent)
                {
                    if (destinationEvent.Name == linkEventTarget)
                    {
                        return intermediateCatchEvent;
                    }
                }
            }
        }

        return null;
    }
}
