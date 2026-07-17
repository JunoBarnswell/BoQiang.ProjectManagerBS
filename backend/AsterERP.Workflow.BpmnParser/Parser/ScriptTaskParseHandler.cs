using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class ScriptTaskParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "scriptTask" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var scriptTask = new ScriptTask
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            ScriptFormat = GetAttributeValue(xmlNode, "scriptFormat"),
            ResultVariable = GetAttributeValue(xmlNode, "activiti", "resultVariable"),
            AutoStoreVariables = GetAttributeValue(xmlNode, "activiti", "autoStoreVariables") == "true"
        };

        BpmnXMLUtil.ParseChildElements("scriptTask", scriptTask, xmlNode, model);

        return scriptTask;
    }
}
