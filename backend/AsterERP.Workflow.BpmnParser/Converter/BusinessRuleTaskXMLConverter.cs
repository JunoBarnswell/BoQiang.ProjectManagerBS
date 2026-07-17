using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class BusinessRuleTaskXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.BusinessRuleTask);
    public override string GetXMLElementName() => "businessRuleTask";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model)
    {
        return new BpmnModelNs.BusinessRuleTask
        {
            RuleVariablesInput = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "ruleVariablesInput"),
            Rules = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "rules"),
            ResultVariable = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "resultVariable"),
            Exclude = string.Equals(
                GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "exclude"),
                "true",
                StringComparison.OrdinalIgnoreCase)
        };
    }
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var businessRuleTask = (BpmnModelNs.BusinessRuleTask)element;
        BpmnXMLUtil.WriteQualifiedAttribute("ruleVariablesInput", businessRuleTask.RuleVariablesInput, xtw);
        BpmnXMLUtil.WriteQualifiedAttribute("rules", businessRuleTask.Rules, xtw);
        BpmnXMLUtil.WriteQualifiedAttribute("resultVariable", businessRuleTask.ResultVariable, xtw);
        if (businessRuleTask.Exclude)
            BpmnXMLUtil.WriteQualifiedAttribute("exclude", "true", xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw) { }
}

