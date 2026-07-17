using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class ServiceTaskParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "serviceTask" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var workflowExtensionClass = GetAttributeValue(xmlNode, "activiti", "class");
        var delegateExpression = GetAttributeValue(xmlNode, "activiti", "delegateExpression");
        var expression = GetAttributeValue(xmlNode, "activiti", "expression");
        var implementationAttr = GetAttributeValue(xmlNode, "implementation");

        var serviceTask = new ServiceTask
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            ImplementationType = GetAttributeValue(xmlNode, "activiti", "type"),
            Implementation = implementationAttr ?? workflowExtensionClass ?? delegateExpression ?? expression,
            DelegateExpression = delegateExpression,
            Expression = expression,
            Class = workflowExtensionClass,
            ResultVariableName = GetAttributeValue(xmlNode, "activiti", "resultVariableName"),
            ExtensionId = GetAttributeValue(xmlNode, "activiti", "connectorId"),
            SkippedExpression = GetAttributeValue(xmlNode, "activiti", "skipExpression")
        };

        BpmnXMLUtil.ParseChildElements("serviceTask", serviceTask, xmlNode, model);

        return serviceTask;
    }
}
