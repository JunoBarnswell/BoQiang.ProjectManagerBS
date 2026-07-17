using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class CallActivityParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "callActivity" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var callActivity = new CallActivity
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            CalledElement = GetAttributeValue(xmlNode, "calledElement") ?? GetAttributeValue(xmlNode, "activiti", "calledElement"),
            CalledElementSameDeployment = GetAttributeValue(xmlNode, "activiti", "sameDeployment") == "true",
            InheritVariables = GetAttributeValue(xmlNode, "activiti", "inheritVariables") == "true",
            BusinessKey = GetAttributeValue(xmlNode, "activiti", "businessKey"),
            InheritBusinessKey = GetAttributeValue(xmlNode, "activiti", "inheritBusinessKey") == "true",
            ProcessInstanceName = GetAttributeValue(xmlNode, "activiti", "processInstanceName")
        };

        BpmnXMLUtil.ParseChildElements("callActivity", callActivity, xmlNode, model);

        return callActivity;
    }
}
