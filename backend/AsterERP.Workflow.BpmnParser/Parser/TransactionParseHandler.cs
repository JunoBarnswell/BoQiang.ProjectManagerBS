using System.Xml;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class TransactionParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "transaction" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var transaction = new Transaction
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            TriggeredByEvent = false
        };

        var handlers = new BpmnParseHandlers();
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;

            var handler = handlers.GetHandler(child.LocalName);
            if (handler != null)
            {
                handler.Parse(child, model, activeProcess);
                var lastAdded = activeProcess.FlowElements.LastOrDefault();
                if (lastAdded != null)
                {
                    activeProcess.FlowElements.Remove(lastAdded);
                    transaction.FlowElements.Add(lastAdded);
                }
            }
            else if (child.LocalName is "dataObject" or "dataObjectReference")
            {
                var itemSubjectRef = GetAttributeValue(child, "itemSubjectRef");
                var dataObj = BpmnDataObjects.CreateDataObject(itemSubjectRef);
                dataObj.Id = GetAttributeValue(child, "id");
                dataObj.Name = GetAttributeValue(child, "name");
                dataObj.ItemSubjectRef = itemSubjectRef;

                foreach (XmlNode dataChild in child.ChildNodes)
                {
                    if (dataChild.NodeType != XmlNodeType.Element) continue;
                    if (dataChild.LocalName == "extensionElements")
                    {
                        foreach (XmlNode extChild in dataChild.ChildNodes)
                        {
                            if (extChild.NodeType != XmlNodeType.Element) continue;
                            if (extChild.LocalName == "value")
                            {
                                BpmnDataObjects.SetDataObjectValue(dataObj, extChild.InnerText);
                            }
                        }
                    }
                }

                transaction.DataObjects.Add(dataObj);
            }
            else if (!IsContainerMetadataElement(child.LocalName))
            {
                var childId = GetAttributeValue(child, "id") ?? "<unknown>";
                throw new WorkflowEngineException(
                    $"Unsupported BPMN element '{child.LocalName}' (id='{childId}') inside transaction '{transaction.Id ?? "<unknown>"}'.");
            }
        }

        BpmnXMLUtil.ParseChildElements("transaction", transaction, xmlNode, model);

        return transaction;
    }

    private static bool IsContainerMetadataElement(string localName)
    {
        return localName is "documentation" or "extensionElements" or "multiInstanceLoopCharacteristics";
    }
}
