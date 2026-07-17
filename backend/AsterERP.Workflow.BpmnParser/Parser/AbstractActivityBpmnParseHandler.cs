using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public abstract class AbstractActivityBpmnParseHandler : AbstractFlowNodeBpmnParseHandler
{
    protected override void PostProcessElement(XmlNode xmlNode, FlowElement element, BpmnModel.BpmnModel model, Process activeProcess)
    {
        base.PostProcessElement(xmlNode, element, model, activeProcess);

        if (element is Activity activity)
        {
            activity.IsForCompensation = ParseBoolean(xmlNode, "isForCompensation");
            activity.DefaultFlow = GetAttributeValue(xmlNode, "default");

            ParseMultiInstanceLoopCharacteristics(xmlNode, activity);
        }
    }

    private void ParseMultiInstanceLoopCharacteristics(XmlNode xmlNode, Activity activity)
    {
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            if (child.LocalName == "multiInstanceLoopCharacteristics")
            {
                var mi = new MultiInstanceLoopCharacteristics
                {
                    IsSequential = GetAttributeValue(child, "isSequential") == "true",
                    LoopCardinality = GetAttributeValue(child, "loopCardinality"),
                    CompletionCondition = GetAttributeValue(child, "completionCondition"),
                    Collection = GetAttributeValue(child, "activiti", "collection"),
                    CollectionVariable = GetAttributeValue(child, "activiti", "collectionVariable"),
                    ElementVariable = GetAttributeValue(child, "activiti", "elementVariable"),
                    ElementIndexVariable = GetAttributeValue(child, "activiti", "elementIndexVariable")
                };

                foreach (XmlNode miChild in child.ChildNodes)
                {
                    if (miChild.NodeType != XmlNodeType.Element) continue;
                    switch (miChild.LocalName)
                    {
                        case "loopCardinality":
                            mi.LoopCardinality = miChild.InnerText.Trim();
                            break;
                        case "completionCondition":
                            mi.CompletionCondition = miChild.InnerText.Trim();
                            break;
                        case "inputDataItem":
                            mi.InputDataItem = GetAttributeValue(miChild, "name");
                            break;
                    }
                }

                activity.LoopCharacteristics = mi;
            }
        }
    }
}
