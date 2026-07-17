using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class ReceiveTaskParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "receiveTask" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var receiveTask = new ReceiveTask
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            Implementation = GetAttributeValue(xmlNode, "implementation")
        };

        BpmnXMLUtil.ParseChildElements("receiveTask", receiveTask, xmlNode, model);
        return receiveTask;
    }
}
