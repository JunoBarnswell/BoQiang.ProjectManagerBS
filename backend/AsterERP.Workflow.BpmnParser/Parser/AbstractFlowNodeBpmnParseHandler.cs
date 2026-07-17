using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public abstract class AbstractFlowNodeBpmnParseHandler : AbstractBpmnParseHandler
{
    protected override void PostProcessElement(XmlNode xmlNode, FlowElement element, BpmnModel.BpmnModel model, Process activeProcess)
    {
        base.PostProcessElement(xmlNode, element, model, activeProcess);

        if (element is FlowNode flowNode)
        {
            flowNode.Asynchronous = GetAttributeValue(xmlNode, "activiti", "async") == "true";
            var exclusiveValue = GetAttributeValue(xmlNode, "activiti", "exclusive");
            if (exclusiveValue == "false")
                flowNode.Exclusive = false;
        }
    }
}
