using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser.Converter;

public static class ProcessExport
{
    public static void WriteProcess(BpmnModelNs.Process process, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_PROCESS, BpmnXMLConstants.BPMN2_NAMESPACE);
        xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, process.Id ?? string.Empty);

        if (!string.IsNullOrEmpty(process.Name))
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, process.Name);

        xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_PROCESS_EXECUTABLE, process.IsExecutable.ToString().ToLowerInvariant());

        if (!string.IsNullOrEmpty(process.CandidateStarterUsers))
            xtw.WriteAttributeString(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, BpmnXMLConstants.ATTRIBUTE_PROCESS_CANDIDATE_USERS, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, process.CandidateStarterUsers);

        if (!string.IsNullOrEmpty(process.CandidateStarterGroups))
            xtw.WriteAttributeString(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, BpmnXMLConstants.ATTRIBUTE_PROCESS_CANDIDATE_GROUPS, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, process.CandidateStarterGroups);

        if (!string.IsNullOrEmpty(process.Documentation))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_DOCUMENTATION, BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(process.Documentation);
            xtw.WriteEndElement();
        }

        bool didWriteExtensionStartElement = WorkflowExtensionListenerExport.WriteListeners(process, false, xtw);
        didWriteExtensionStartElement = BpmnXMLUtil.WriteExtensionElements(process, didWriteExtensionStartElement, xtw);

        if (didWriteExtensionStartElement)
            xtw.WriteEndElement();

        LaneExport.WriteLanes(process, xtw);
    }
}

public static class CollaborationExport
{
    public static void WritePools(BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        if (model.Pools.Count == 0 && model.MessageFlows.Count == 0) return;

        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_COLLABORATION, BpmnXMLConstants.BPMN2_NAMESPACE);
        xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, "Collaboration");

        foreach (var pool in model.Pools)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_PARTICIPANT, BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, pool.Id ?? string.Empty);
            if (!string.IsNullOrEmpty(pool.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, pool.Name);
            if (!string.IsNullOrEmpty(pool.ProcessRef))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_PROCESS_REF, pool.ProcessRef);
            xtw.WriteEndElement();
        }

        foreach (var messageFlow in model.MessageFlows)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_MESSAGE_FLOW, BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, messageFlow.Id ?? string.Empty);
            if (!string.IsNullOrEmpty(messageFlow.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, messageFlow.Name);
            if (!string.IsNullOrEmpty(messageFlow.SourceRef))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_FLOW_SOURCE_REF, messageFlow.SourceRef);
            if (!string.IsNullOrEmpty(messageFlow.TargetRef))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_FLOW_TARGET_REF, messageFlow.TargetRef);
            if (!string.IsNullOrEmpty(messageFlow.MessageRef))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_MESSAGE_REF, messageFlow.MessageRef);
            xtw.WriteEndElement();
        }

        xtw.WriteEndElement();
    }
}

public static class BPMNDIExport
{
    public static void WriteBPMNDI(BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        xtw.WriteStartElement(BpmnXMLConstants.BPMNDI_PREFIX, BpmnXMLConstants.ELEMENT_DI_DIAGRAM, BpmnXMLConstants.BPMNDI_NAMESPACE);

        string processId;
        if (model.Pools.Count > 0)
            processId = "Collaboration";
        else if (model.Processes.Count > 0)
            processId = model.Processes[0].Id ?? "process";
        else
            processId = "process";

        xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, "BPMNDiagram_" + processId);

        xtw.WriteStartElement(BpmnXMLConstants.BPMNDI_PREFIX, BpmnXMLConstants.ELEMENT_DI_PLANE, BpmnXMLConstants.BPMNDI_NAMESPACE);
        xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_BPMNELEMENT, processId);
        xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, "BPMNPlane_" + processId);

        foreach (var kvp in model.LocationMap)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMNDI_PREFIX, BpmnXMLConstants.ELEMENT_DI_SHAPE, BpmnXMLConstants.BPMNDI_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_BPMNELEMENT, kvp.Key);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, "BPMNShape_" + kvp.Key);

            var bounds = kvp.Value;
            xtw.WriteStartElement(BpmnXMLConstants.OMGDC_PREFIX, BpmnXMLConstants.ELEMENT_DI_BOUNDS, BpmnXMLConstants.OMGDC_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_HEIGHT, bounds.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_WIDTH, bounds.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_X, bounds.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_Y, bounds.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            xtw.WriteEndElement();

            if (model.LabelLocationMap.TryGetValue(kvp.Key, out var labelBounds))
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMNDI_PREFIX, "BPMNLabel", BpmnXMLConstants.BPMNDI_NAMESPACE);
                xtw.WriteStartElement(BpmnXMLConstants.OMGDC_PREFIX, BpmnXMLConstants.ELEMENT_DI_BOUNDS, BpmnXMLConstants.OMGDC_NAMESPACE);
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_HEIGHT, labelBounds.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_WIDTH, labelBounds.Width.ToString(System.Globalization.CultureInfo.InvariantCulture));
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_X, labelBounds.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_Y, labelBounds.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                xtw.WriteEndElement();
                xtw.WriteEndElement();
            }

            xtw.WriteEndElement();
        }

        foreach (var kvp in model.FlowLocationMap)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMNDI_PREFIX, BpmnXMLConstants.ELEMENT_DI_EDGE, BpmnXMLConstants.BPMNDI_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_BPMNELEMENT, kvp.Key);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, "BPMNEdge_" + kvp.Key);

            foreach (var wp in kvp.Value)
            {
                xtw.WriteStartElement(BpmnXMLConstants.OMGDI_PREFIX, BpmnXMLConstants.ELEMENT_DI_WAYPOINT, BpmnXMLConstants.OMGDI_NAMESPACE);
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_X, wp.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_DI_Y, wp.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                xtw.WriteEndElement();
            }

            xtw.WriteEndElement();
        }

        xtw.WriteEndElement();
        xtw.WriteEndElement();
    }
}

public static class DefinitionsRootExport
{
    public static void WriteRootElement(BpmnModelNs.BpmnModel model, XmlWriter xtw, string encoding)
    {
        xtw.WriteStartDocument();

        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_DEFINITIONS, BpmnXMLConstants.BPMN2_NAMESPACE);
        xtw.WriteAttributeString("xmlns", BpmnXMLConstants.BPMN2_PREFIX, null, BpmnXMLConstants.BPMN2_NAMESPACE);
        xtw.WriteAttributeString("xmlns", BpmnXMLConstants.XSI_PREFIX, null, BpmnXMLConstants.XSI_NAMESPACE);
        xtw.WriteAttributeString("xmlns", BpmnXMLConstants.XSD_PREFIX, null, BpmnXMLConstants.SCHEMA_NAMESPACE);
        xtw.WriteAttributeString("xmlns", BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, null, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);
        xtw.WriteAttributeString("xmlns", BpmnXMLConstants.BPMNDI_PREFIX, null, BpmnXMLConstants.BPMNDI_NAMESPACE);
        xtw.WriteAttributeString("xmlns", BpmnXMLConstants.OMGDC_PREFIX, null, BpmnXMLConstants.OMGDC_NAMESPACE);
        xtw.WriteAttributeString("xmlns", BpmnXMLConstants.OMGDI_PREFIX, null, BpmnXMLConstants.OMGDI_NAMESPACE);

        xtw.WriteAttributeString(BpmnXMLConstants.TYPE_LANGUAGE_ATTRIBUTE, BpmnXMLConstants.SCHEMA_NAMESPACE);
        xtw.WriteAttributeString(BpmnXMLConstants.EXPRESSION_LANGUAGE_ATTRIBUTE, BpmnXMLConstants.XPATH_NAMESPACE);

        if (!string.IsNullOrEmpty(model.TargetNamespace))
            xtw.WriteAttributeString(BpmnXMLConstants.TARGET_NAMESPACE_ATTRIBUTE, model.TargetNamespace);
        else
            xtw.WriteAttributeString(BpmnXMLConstants.TARGET_NAMESPACE_ATTRIBUTE, BpmnXMLConstants.PROCESS_NAMESPACE);
    }
}

public static class SignalAndMessageDefinitionExport
{
    public static void WriteSignalsAndMessages(BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        foreach (var signal in model.Signals)
        {
            xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_SIGNAL);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, signal.Id ?? string.Empty);
            if (!string.IsNullOrEmpty(signal.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, signal.Name);
            if (!string.IsNullOrEmpty(signal.Scope))
                xtw.WriteAttributeString(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, BpmnXMLConstants.ATTRIBUTE_SCOPE, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE, signal.Scope);
            xtw.WriteEndElement();
        }

        foreach (var message in model.MessageMap.Values)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_MESSAGE, BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, message.Id ?? string.Empty);
            if (!string.IsNullOrEmpty(message.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, message.Name);
            xtw.WriteEndElement();
        }

        foreach (var escalation in model.EscalationMap.Values)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "escalation", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, escalation.Id ?? string.Empty);
            if (!string.IsNullOrEmpty(escalation.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, escalation.Name);
            if (!string.IsNullOrEmpty(escalation.EscalationCode))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ESCALATION_CODE, escalation.EscalationCode);
            xtw.WriteEndElement();
        }
    }
}

public static class DefinitionsSupplementExport
{
    public static void WriteDefinitionsSupplement(BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        foreach (var import in model.Imports)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "import", BpmnXMLConstants.BPMN2_NAMESPACE);
            if (!string.IsNullOrEmpty(import.ImportType))
                xtw.WriteAttributeString("importType", import.ImportType);
            if (!string.IsNullOrEmpty(import.Location))
                xtw.WriteAttributeString("location", import.Location);
            if (!string.IsNullOrEmpty(import.Namespace))
                xtw.WriteAttributeString("namespace", import.Namespace);
            xtw.WriteEndElement();
        }

        foreach (var itemDefinition in model.ItemDefinitionMap.Values)
        {
            if (string.IsNullOrWhiteSpace(itemDefinition.Id))
                continue;

            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "itemDefinition", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, itemDefinition.Id);
            if (!string.IsNullOrWhiteSpace(itemDefinition.StructureRef))
                xtw.WriteAttributeString("structureRef", itemDefinition.StructureRef);
            if (!string.IsNullOrWhiteSpace(itemDefinition.ItemKind))
                xtw.WriteAttributeString("itemKind", itemDefinition.ItemKind);
            if (itemDefinition.IsCollection)
                xtw.WriteAttributeString("isCollection", "true");
            xtw.WriteEndElement();
        }

        foreach (var iface in model.Interfaces)
        {
            if (string.IsNullOrWhiteSpace(iface.Id))
                continue;

            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "interface", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, iface.Id);
            if (!string.IsNullOrWhiteSpace(iface.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, iface.Name);

            foreach (var operation in iface.Operations)
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "operation", BpmnXMLConstants.BPMN2_NAMESPACE);
                if (!string.IsNullOrWhiteSpace(operation.Id))
                    xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, operation.Id);
                if (!string.IsNullOrWhiteSpace(operation.Name))
                    xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, operation.Name);
                if (!string.IsNullOrWhiteSpace(operation.ImplementationRef))
                    xtw.WriteAttributeString("implementationRef", operation.ImplementationRef);
                if (!string.IsNullOrWhiteSpace(operation.InMessage?.Id))
                    xtw.WriteAttributeString("inMessageRef", operation.InMessage.Id);
                if (!string.IsNullOrWhiteSpace(operation.OutMessage?.Id))
                    xtw.WriteAttributeString("outMessageRef", operation.OutMessage.Id);
                xtw.WriteEndElement();
            }

            xtw.WriteEndElement();
        }

        foreach (var resource in model.Resources)
        {
            if (string.IsNullOrWhiteSpace(resource.Id))
                continue;

            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "resource", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, resource.Id);
            if (!string.IsNullOrWhiteSpace(resource.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, resource.Name);
            xtw.WriteEndElement();
        }
    }
}

public static class MultiInstanceExport
{
    public static void WriteMultiInstance(BpmnModelNs.Activity? activity, XmlWriter xtw)
    {
        if (activity?.LoopCharacteristics == null) return;

        var mi = activity.LoopCharacteristics;

        xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_MULTIINSTANCE, BpmnXMLConstants.BPMN2_NAMESPACE);

        BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_MULTIINSTANCE_SEQUENTIAL, mi.IsSequential.ToString().ToLowerInvariant(), xtw);

        WriteQualifiedAttributeIfNotEmpty(BpmnXMLConstants.ATTRIBUTE_MULTIINSTANCE_COLLECTION, mi.InputDataItem, xtw);
        WriteQualifiedAttributeIfNotEmpty(BpmnXMLConstants.ATTRIBUTE_MULTIINSTANCE_VARIABLE, mi.ElementVariable, xtw);
        WriteQualifiedAttributeIfNotEmpty(BpmnXMLConstants.ATTRIBUTE_MULTIINSTANCE_INDEX_VARIABLE, mi.ElementIndexVariable, xtw);

        WriteElementIfNotEmpty(BpmnXMLConstants.ELEMENT_MULTIINSTANCE_CARDINALITY, mi.LoopCardinality, xtw);
        WriteElementIfNotEmpty(BpmnXMLConstants.ELEMENT_MULTI_INSTANCE_DATA_OUTPUT, mi.OutputDataItem, xtw);

        if (!string.IsNullOrEmpty(mi.OutputDataItem))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_MULTI_INSTANCE_OUTPUT_DATA_ITEM, BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, mi.OutputDataItem);
            xtw.WriteEndElement();
        }

        WriteElementIfNotEmpty(BpmnXMLConstants.ELEMENT_MULTIINSTANCE_CONDITION, mi.CompletionCondition, xtw);

        xtw.WriteEndElement();
    }

    private static void WriteQualifiedAttributeIfNotEmpty(string attributeName, string? value, XmlWriter xtw)
    {
        if (!string.IsNullOrEmpty(value))
            BpmnXMLUtil.WriteQualifiedAttribute(attributeName, value, xtw);
    }

    private static void WriteElementIfNotEmpty(string elementName, string? value, XmlWriter xtw)
    {
        if (!string.IsNullOrEmpty(value))
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, elementName, BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteString(value);
            xtw.WriteEndElement();
        }
    }
}

public static class LaneExport
{
    public static void WriteLanes(BpmnModelNs.Process process, XmlWriter xtw)
    {
        if (!process.ExtensionElements.TryGetValue(BpmnXMLConstants.ELEMENT_LANESET, out var laneSets) || laneSets.Count == 0)
            return;

        foreach (var laneSet in laneSets)
        {
            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_LANESET, BpmnXMLConstants.BPMN2_NAMESPACE);

            foreach (var lane in laneSet.ChildElements.Where(c => c.Name == BpmnXMLConstants.ELEMENT_LANE))
            {
                xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_LANE, BpmnXMLConstants.BPMN2_NAMESPACE);

                var idAttr = lane.Attributes.FirstOrDefault(a => a.Name == BpmnXMLConstants.ATTRIBUTE_ID);
                if (!string.IsNullOrWhiteSpace(idAttr?.Value))
                    xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, idAttr.Value);

                var nameAttr = lane.Attributes.FirstOrDefault(a => a.Name == BpmnXMLConstants.ATTRIBUTE_NAME);
                if (!string.IsNullOrWhiteSpace(nameAttr?.Value))
                    xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, nameAttr.Value);

                foreach (var flowRef in lane.ChildElements.Where(c => c.Name == BpmnXMLConstants.ELEMENT_FLOWNODE_REF))
                {
                    if (string.IsNullOrWhiteSpace(flowRef.ElementText))
                        continue;

                    xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, BpmnXMLConstants.ELEMENT_FLOWNODE_REF, BpmnXMLConstants.BPMN2_NAMESPACE);
                    xtw.WriteString(flowRef.ElementText);
                    xtw.WriteEndElement();
                }

                xtw.WriteEndElement();
            }

            xtw.WriteEndElement();
        }
    }
}

public static class FieldExtensionExport
{
    public static bool WriteFieldExtensions(List<BpmnModelNs.FieldExtension> fieldExtensionList, bool didWriteExtensionStartElement, XmlWriter xtw)
    {
        foreach (var fieldExtension in fieldExtensionList)
        {
            if (string.IsNullOrEmpty(fieldExtension.FieldName)) continue;
            if (string.IsNullOrEmpty(fieldExtension.StringValue) && string.IsNullOrEmpty(fieldExtension.Expression)) continue;

            if (!didWriteExtensionStartElement)
            {
                xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EXTENSIONS);
                didWriteExtensionStartElement = true;
            }

            xtw.WriteStartElement(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, BpmnXMLConstants.ELEMENT_FIELD, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);
            BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_FIELD_NAME, fieldExtension.FieldName, xtw);

            if (!string.IsNullOrEmpty(fieldExtension.StringValue))
            {
                xtw.WriteStartElement(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, BpmnXMLConstants.ELEMENT_FIELD_STRING, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);
                xtw.WriteCData(fieldExtension.StringValue);
                xtw.WriteEndElement();
            }
            else
            {
                xtw.WriteStartElement(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, BpmnXMLConstants.ATTRIBUTE_FIELD_EXPRESSION, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);
                xtw.WriteCData(fieldExtension.Expression!);
                xtw.WriteEndElement();
            }

            xtw.WriteEndElement();
        }

        return didWriteExtensionStartElement;
    }
}

public static class FailedJobRetryCountExport
{
    public static bool WriteFailedJobRetryCount(BpmnModelNs.Activity activity, bool didWriteExtensionStartElement, XmlWriter xtw)
    {
        var retryTimeCycle = activity.GetAttributeValue(
            BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE,
            BpmnXMLConstants.FAILED_JOB_RETRY_TIME_CYCLE);

        if (string.IsNullOrWhiteSpace(retryTimeCycle))
            retryTimeCycle = activity.GetAttributeValue(
                null,
                BpmnXMLConstants.FAILED_JOB_RETRY_TIME_CYCLE);

        if (string.IsNullOrWhiteSpace(retryTimeCycle) &&
            activity.ExtensionElements.TryGetValue(BpmnXMLConstants.FAILED_JOB_RETRY_TIME_CYCLE, out var extensionElements))
        {
            foreach (var extensionElement in extensionElements)
            {
                if (!string.IsNullOrWhiteSpace(extensionElement.ElementText))
                {
                    retryTimeCycle = extensionElement.ElementText;
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(retryTimeCycle))
            return didWriteExtensionStartElement;

        if (!didWriteExtensionStartElement)
        {
            xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EXTENSIONS);
            didWriteExtensionStartElement = true;
        }

        xtw.WriteStartElement(
            BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX,
            BpmnXMLConstants.FAILED_JOB_RETRY_TIME_CYCLE,
            BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);
        xtw.WriteString(retryTimeCycle);
        xtw.WriteEndElement();

        return didWriteExtensionStartElement;
    }
}

public static class ErrorExport
{
    public static void WriteError(BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        foreach (var error in model.ErrorMap.Values)
        {
            if (string.IsNullOrWhiteSpace(error.Id))
                continue;

            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "error", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, error.Id);
            if (!string.IsNullOrWhiteSpace(error.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, error.Name);
            if (!string.IsNullOrWhiteSpace(error.ErrorCode))
                xtw.WriteAttributeString("errorCode", error.ErrorCode);
            xtw.WriteEndElement();
        }
    }
}

public static class DataStoreExport
{
    public static void WriteDataStores(BpmnModelNs.BpmnModel model, XmlWriter xtw)
    {
        foreach (var dataStore in model.DataStoreMap.Values)
        {
            if (string.IsNullOrWhiteSpace(dataStore.Id))
                continue;

            xtw.WriteStartElement(BpmnXMLConstants.BPMN2_PREFIX, "dataStore", BpmnXMLConstants.BPMN2_NAMESPACE);
            xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_ID, dataStore.Id);
            if (!string.IsNullOrWhiteSpace(dataStore.Name))
                xtw.WriteAttributeString(BpmnXMLConstants.ATTRIBUTE_NAME, dataStore.Name);
            if (!string.IsNullOrWhiteSpace(dataStore.ItemSubjectRef))
                xtw.WriteAttributeString("itemSubjectRef", dataStore.ItemSubjectRef);
            if (!string.IsNullOrWhiteSpace(dataStore.Capacity))
                xtw.WriteAttributeString("capacity", dataStore.Capacity);

            if (!dataStore.IsUnlimited)
                xtw.WriteAttributeString("isUnlimited", "false");

            xtw.WriteEndElement();
        }
    }
}

public static class WorkflowExtensionListenerExport
{
    public static bool WriteListeners(BpmnModelNs.BaseElement element, bool didWriteExtensionStartElement, XmlWriter xtw)
    {
        if (element is BpmnModelNs.FlowElement flowElement)
        {
            didWriteExtensionStartElement = WriteListenerList(
                BpmnXMLConstants.ELEMENT_EXECUTION_LISTENER,
                flowElement.ExecutionListeners,
                didWriteExtensionStartElement,
                xtw);
        }
        else if (element is BpmnModelNs.Process process)
        {
            didWriteExtensionStartElement = WriteListenerList(
                BpmnXMLConstants.ELEMENT_EXECUTION_LISTENER,
                process.ExecutionListeners,
                didWriteExtensionStartElement,
                xtw);
        }

        if (element is BpmnModelNs.UserTask userTask)
        {
            didWriteExtensionStartElement = WriteListenerList(
                BpmnXMLConstants.ELEMENT_TASK_LISTENER,
                userTask.TaskListeners,
                didWriteExtensionStartElement,
                xtw);
        }

        return didWriteExtensionStartElement;
    }

    private static bool WriteListenerList(
        string xmlElementName,
        List<BpmnModelNs.WorkflowExtensionListener> listenerList,
        bool didWriteExtensionStartElement,
        XmlWriter xtw)
    {
        if (listenerList == null) return didWriteExtensionStartElement;

        foreach (var listener in listenerList)
        {
            if (string.IsNullOrEmpty(listener.Event)) continue;

            if (!didWriteExtensionStartElement)
            {
                xtw.WriteStartElement(BpmnXMLConstants.ELEMENT_EXTENSIONS);
                didWriteExtensionStartElement = true;
            }

            xtw.WriteStartElement(BpmnXMLConstants.WORKFLOW_EXTENSION_PREFIX, xmlElementName, BpmnXMLConstants.WORKFLOW_EXTENSION_NAMESPACE);
            BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_LISTENER_EVENT, listener.Event, xtw);

            if (listener.ImplementationType == "class")
                BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_LISTENER_CLASS, listener.Implementation, xtw);
            else if (listener.ImplementationType == "expression")
                BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_LISTENER_EXPRESSION, listener.Implementation, xtw);
            else if (listener.ImplementationType == "delegateExpression")
                BpmnXMLUtil.WriteDefaultAttribute(BpmnXMLConstants.ATTRIBUTE_LISTENER_DELEGATEEXPRESSION, listener.Implementation, xtw);

            FieldExtensionExport.WriteFieldExtensions(listener.FieldExtensions, true, xtw);

            xtw.WriteEndElement();
        }

        return didWriteExtensionStartElement;
    }
}
