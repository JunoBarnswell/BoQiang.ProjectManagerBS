using System.Xml;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.BpmnParser.Converter;

namespace AsterERP.Workflow.BpmnParser.Parser;

public class EventSubProcessParseHandler : AbstractActivityBpmnParseHandler
{
    public override string[] HandledTypes => new[] { "eventSubProcess" };

    protected override FlowElement? ParseElement(XmlNode xmlNode, BpmnModel.BpmnModel model, Process activeProcess)
    {
        var eventSubProcess = new EventSubProcess
        {
            Id = GetAttributeValue(xmlNode, "id"),
            Name = GetAttributeValue(xmlNode, "name"),
            TriggeredByEvent = true
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
                    eventSubProcess.FlowElements.Add(lastAdded);
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

                eventSubProcess.DataObjects.Add(dataObj);
            }
            else if (!IsContainerMetadataElement(child.LocalName))
            {
                var childId = GetAttributeValue(child, "id") ?? "<unknown>";
                throw new WorkflowEngineException(
                    $"Unsupported BPMN element '{child.LocalName}' (id='{childId}') inside eventSubProcess '{eventSubProcess.Id ?? "<unknown>"}'.");
            }
        }

        BpmnXMLUtil.ParseChildElements("eventSubProcess", eventSubProcess, xmlNode, model);

        return eventSubProcess;
    }

    private static bool IsContainerMetadataElement(string localName)
    {
        return localName is "documentation" or "extensionElements" or "multiInstanceLoopCharacteristics";
    }
}
