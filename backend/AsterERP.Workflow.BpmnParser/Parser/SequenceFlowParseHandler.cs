using System.Xml;
using AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class SequenceFlowParseHandler : AbstractBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "sequenceFlow" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var sf = new SequenceFlow
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            SourceRef = GetAttributeValue(xmlNode, "sourceRef"),
            TargetRef = GetAttributeValue(xmlNode, "targetRef")
        };

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.LocalName == "conditionExpression" && child.NodeType == XmlNodeType.Element)
                sf.ConditionExpression = child.InnerText;
        }

        return sf;
    }
}
