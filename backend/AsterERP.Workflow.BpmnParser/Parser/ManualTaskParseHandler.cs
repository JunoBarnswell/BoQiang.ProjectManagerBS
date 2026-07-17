using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class ManualTaskParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "manualTask" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var manualTask = new ManualTask
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name")
        };

        BpmnXMLUtil.ParseChildElements("manualTask", manualTask, xmlNode, model);

        return manualTask;
    }
}
