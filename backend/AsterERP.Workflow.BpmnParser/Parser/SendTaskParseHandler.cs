using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class SendTaskParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "sendTask" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var implementationAttr = GetAttributeValue(xmlNode, "implementation");
        var workflowExtensionClass = GetAttributeValue(xmlNode, "activiti", "class");
        var delegateExpression = GetAttributeValue(xmlNode, "activiti", "delegateExpression");
        var expression = GetAttributeValue(xmlNode, "activiti", "expression");

        var implementationType = (string?)null;
        var implementation = (string?)null;

        if (workflowExtensionClass != null)
        {
            implementationType = ImplementationTypeConstants.ImplementationTypeClass;
            implementation = workflowExtensionClass;
        }
        else if (expression != null)
        {
            implementationType = ImplementationTypeConstants.ImplementationTypeExpression;
            implementation = expression;
        }
        else if (delegateExpression != null)
        {
            implementationType = ImplementationTypeConstants.ImplementationTypeDelegateExpression;
            implementation = delegateExpression;
        }
        else if (implementationAttr != null)
        {
            implementation = implementationAttr;
        }

        var sendTask = new SendTask
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            Implementation = implementation,
            ImplementationType = implementationType,
            OperationRef = GetAttributeValue(xmlNode, "operationRef")
        };

        BpmnXMLUtil.ParseChildElements("sendTask", sendTask, xmlNode, model);
        return sendTask;
    }
}
