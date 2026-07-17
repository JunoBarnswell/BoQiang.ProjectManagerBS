using System.Xml;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class BusinessRuleParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "businessRuleTask" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var businessRuleTask = new BusinessRuleTask
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            RuleVariablesInput = GetAttributeValue(xmlNode, "activiti", "ruleVariablesInput"),
            Rules = GetAttributeValue(xmlNode, "activiti", "rules"),
            ResultVariable = GetAttributeValue(xmlNode, "activiti", "resultVariable"),
            Exclude = GetAttributeValue(xmlNode, "activiti", "exclude") == "true"
        };

        BpmnXMLUtil.ParseChildElements("businessRuleTask", businessRuleTask, xmlNode, model);

        return businessRuleTask;
    }
}
