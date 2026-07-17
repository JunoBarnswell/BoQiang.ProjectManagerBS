using System.Xml;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class AdhocSubProcessParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "adHocSubProcess" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var adhocSubProcess = new AdhocSubProcess
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            Ordering = GetAttributeValue(xmlNode, "ordering"),
            CancelRemainingInstances = GetAttributeValue(xmlNode, "cancelRemainingInstances")
        };

        var handlers = new BpmnParseHandlers();
        foreach (XmlNode child in xmlNode.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element) continue;

            if (child.LocalName == "completionCondition")
            {
                adhocSubProcess.CompletionCondition = child.InnerText.Trim();
                continue;
            }

            var handler = handlers.GetHandler(child.LocalName);
            if (handler != null)
            {
                handler.Parse(child, model, activeProcess);
                var lastAdded = activeProcess.FlowElements.LastOrDefault();
                if (lastAdded != null)
                {
                    activeProcess.FlowElements.Remove(lastAdded);
                    adhocSubProcess.FlowElements.Add(lastAdded);
                }
            }
            else if (child.LocalName == "dataObject" || child.LocalName == "dataObjectReference")
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

                adhocSubProcess.DataObjects.Add(dataObj);
            }
            else if (!IsContainerMetadataElement(child.LocalName))
            {
                var childId = GetAttributeValue(child, "id") ?? "<unknown>";
                throw new WorkflowEngineException(
                    $"Unsupported BPMN element '{child.LocalName}' (id='{childId}') inside adHocSubProcess '{adhocSubProcess.Id ?? "<unknown>"}'.");
            }
        }

        BpmnXMLUtil.ParseChildElements("adHocSubProcess", adhocSubProcess, xmlNode, model);

        return adhocSubProcess;
    }

    private static bool IsContainerMetadataElement(string localName)
    {
        return localName is "documentation" or "extensionElements" or "multiInstanceLoopCharacteristics";
    }
}
