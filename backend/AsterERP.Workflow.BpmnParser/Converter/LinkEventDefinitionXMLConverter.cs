using System;
using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class LinkEventDefinitionXMLConverter : BaseBpmnXMLConverter
{
    public override Type GetBpmnElementType() => typeof(BpmnModelNs.LinkEventDefinition);
    public override string GetXMLElementName() => "linkEventDefinition";
    protected override BpmnModelNs.BaseElement ConvertXMLToElement(XmlNode xmlNode, BpmnModelNs.BpmnModel model) =>
        Parser.EventDefinitionParserHelper.ParseLinkEventDefinition(xmlNode);
    protected override void WriteAdditionalAttributes(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var linkDef = (BpmnModelNs.LinkEventDefinition)element;
        BpmnXMLUtil.WriteDefaultAttribute("name", linkDef.Name, xtw);
    }
    protected override void WriteAdditionalChildElements(BpmnModelNs.BaseElement element, BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        var linkDef = (BpmnModelNs.LinkEventDefinition)element;
        foreach (var source in linkDef.Sources)
        {
            xtw.WriteStartElement("source");
            xtw.WriteString(source);
            xtw.WriteEndElement();
        }

        if (!string.IsNullOrEmpty(linkDef.Target))
        {
            xtw.WriteStartElement("target");
            xtw.WriteString(linkDef.Target);
            xtw.WriteEndElement();
        }
    }
}
