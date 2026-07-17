using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public class MultiInstanceParser : BaseChildElementParser
{
    public override string ElementName => "multiInstanceLoopCharacteristics";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.Activity;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        var mi = new BpmnModelNs.MultiInstanceLoopCharacteristics();

        var isSequential = GetAttributeValue(xmlNode, "isSequential");
        mi.IsSequential = isSequential == "true";

        var collection = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "collection");
        if (!string.IsNullOrEmpty(collection))
            mi.Collection = collection;

        var elementVariable = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "elementVariable");
        if (!string.IsNullOrEmpty(elementVariable))
            mi.ElementVariable = elementVariable;

        var elementIndexVariable = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "elementIndexVariable");
        if (!string.IsNullOrEmpty(elementIndexVariable))
            mi.ElementIndexVariable = elementIndexVariable;

        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;
            switch (child.LocalName)
            {
                case "loopCardinality":
                    mi.LoopCardinality = child.InnerText;
                    break;
                case "loopDataOutputRef":
                    mi.OutputDataItem = child.InnerText;
                    break;
                case "completionCondition":
                    mi.CompletionCondition = child.InnerText;
                    break;
                case "dataInput":
                    mi.InputDataItem = child.InnerText;
                    break;
                case "inputDataItem":
                    if (string.IsNullOrEmpty(mi.InputDataItem))
                        mi.InputDataItem = GetAttributeValue(child, "name") ?? child.InnerText;
                    break;
                case "outputDataItem":
                    mi.OutputDataItem = GetAttributeValue(child, "name") ?? child.InnerText;
                    break;
            }
        }

        if (parentElement is BpmnModelNs.Activity activity)
            activity.LoopCharacteristics = mi;
    }
}

public class MultiInstanceAttributesParser : BaseChildElementParser
{
    public override string ElementName => "multiInstanceLoopCharacteristics";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.Activity;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is not BpmnModelNs.Activity activity) return;
        if (activity.LoopCharacteristics == null)
            activity.LoopCharacteristics = new BpmnModelNs.MultiInstanceLoopCharacteristics();

        var mi = activity.LoopCharacteristics;

        var isSequential = GetAttributeValue(xmlNode, "isSequential");
        mi.IsSequential = isSequential == "true";

        var collection = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "collection");
        if (!string.IsNullOrEmpty(collection))
            mi.Collection = collection;

        var elementVariable = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "elementVariable");
        if (!string.IsNullOrEmpty(elementVariable))
            mi.ElementVariable = elementVariable;

        var collectionVariable = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "collectionVariable");
        if (!string.IsNullOrEmpty(collectionVariable))
            mi.CollectionVariable = collectionVariable;

        var elementIndexVariable = GetAttributeValue(xmlNode, BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, "elementIndexVariable");
        if (!string.IsNullOrEmpty(elementIndexVariable))
            mi.ElementIndexVariable = elementIndexVariable;
    }
}

public class LoopCardinalityParser : BaseChildElementParser
{
    public override string ElementName => "loopCardinality";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.MultiInstanceLoopCharacteristics;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.MultiInstanceLoopCharacteristics mi)
            mi.LoopCardinality = xmlNode.InnerText;
    }
}

public class LoopDataOutputRefParser : BaseChildElementParser
{
    public override string ElementName => "loopDataOutputRef";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.MultiInstanceLoopCharacteristics;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
    }
}

public class MultiInstanceCompletionConditionParser : BaseChildElementParser
{
    public override string ElementName => "completionCondition";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.MultiInstanceLoopCharacteristics;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.MultiInstanceLoopCharacteristics mi)
            mi.CompletionCondition = xmlNode.InnerText;
    }
}

public class MultiInstanceDataInputParser : BaseChildElementParser
{
    public override string ElementName => "dataInput";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.MultiInstanceLoopCharacteristics;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.MultiInstanceLoopCharacteristics mi)
            mi.InputDataItem = GetAttributeValue(xmlNode, "name") ?? xmlNode.InnerText;
    }
}

public class MultiInstanceInputDataItemParser : BaseChildElementParser
{
    public override string ElementName => "inputDataItem";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.MultiInstanceLoopCharacteristics;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.MultiInstanceLoopCharacteristics mi)
        {
            if (string.IsNullOrEmpty(mi.InputDataItem))
                mi.InputDataItem = GetAttributeValue(xmlNode, "name") ?? xmlNode.InnerText;
        }
    }
}

public class MultiInstanceOutputDataItemParser : BaseChildElementParser
{
    public override string ElementName => "outputDataItem";

    public override bool Accepts(BpmnModelNs.BaseElement element)
    {
        return element is BpmnModelNs.MultiInstanceLoopCharacteristics;
    }

    public override void ParseChildElement(XmlNode xmlNode, BpmnModelNs.BaseElement parentElement, BpmnModelNs.BpmnModel model)
    {
        if (parentElement is BpmnModelNs.MultiInstanceLoopCharacteristics mi)
            mi.OutputDataItem = GetAttributeValue(xmlNode, "name") ?? xmlNode.InnerText;
    }
}
