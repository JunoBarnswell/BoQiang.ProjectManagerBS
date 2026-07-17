using System;
using System.Text;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public class BpmnModelExporter
{
    public string ExportToXml(BpmnModelNs.BpmnModel model)
    {
        var document = new XmlDocument();

        var declaration = document.CreateXmlDeclaration("1.0", "UTF-8", null);
        document.AppendChild(declaration);

        var definitions = document.CreateElement("bpmn", "definitions", BpmnConstants.BpmnNamespace);
        definitions.SetAttribute("xmlns:bpmn", BpmnConstants.BpmnNamespace);
        definitions.SetAttribute("xmlns:activiti", BpmnConstants.WorkflowExtensionNamespace);
        definitions.SetAttribute("xmlns:bpmndi", BpmnConstants.BpmnDiNamespace);
        definitions.SetAttribute("xmlns:omgdc", BpmnConstants.OmgDcNamespace);
        definitions.SetAttribute("xmlns:omgdi", BpmnConstants.OmgDiNamespace);
        definitions.SetAttribute("targetNamespace", BpmnConstants.TargetNamespace);
        document.AppendChild(definitions);

        ExportSignals(model, definitions, document);
        ExportMessages(model, definitions, document);
        ExportEscalations(model, definitions, document);
        ExportDataStores(model, definitions, document);
        ExportImports(model, definitions, document);
        ExportItemDefinitions(model, definitions, document);
        ExportInterfaces(model, definitions, document);
        ExportResources(model, definitions, document);
        ExportCollaboration(model, definitions, document);
        ExportErrors(model, definitions, document);

        foreach (var process in model.Processes)
        {
            ExportProcess(process, definitions, document);
        }

        return FormatXml(document);
    }

    public string ConvertToXml(BpmnModelNs.BpmnModel model)
    {
        return ExportToXml(model);
    }

    private void ExportProcess(BpmnModelNs.Process process, XmlElement definitions, XmlDocument document)
    {
        var processElement = document.CreateElement("bpmn", "process", BpmnConstants.BpmnNamespace);
        processElement.SetAttribute("id", process.Id ?? string.Empty);
        if (process.Name != null)
            processElement.SetAttribute("name", process.Name);
        processElement.SetAttribute("isExecutable", process.IsExecutable ? "true" : "false");

        foreach (var flowElement in process.FlowElements)
        {
            var converter = BpmnConverterRegistry.GetConverterForElement(flowElement);
            if (converter == null)
                throw new WorkflowEngineException($"Unsupported BPMN model element '{flowElement.GetType().Name}' (id='{flowElement.Id ?? "<unknown>"}').");
            converter.ConvertToXml(flowElement, processElement, document);
        }

        definitions.AppendChild(processElement);
    }

    private void ExportSignals(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var signal in model.Signals)
        {
            var signalElement = document.CreateElement("bpmn", "signal", BpmnConstants.BpmnNamespace);
            if (signal.Id != null)
                signalElement.SetAttribute("id", signal.Id);
            if (signal.Name != null)
                signalElement.SetAttribute("name", signal.Name);
            if (signal.Scope != null)
                SetWorkflowExtensionAttribute(signalElement, "scope", signal.Scope);
            definitions.AppendChild(signalElement);
        }
    }

    private void ExportMessages(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var message in model.MessageMap.Values)
        {
            var messageElement = document.CreateElement("bpmn", "message", BpmnConstants.BpmnNamespace);
            if (message.Id != null)
                messageElement.SetAttribute("id", message.Id);
            if (message.Name != null)
                messageElement.SetAttribute("name", message.Name);
            if (message.ItemRef != null)
                messageElement.SetAttribute("itemRef", message.ItemRef);
            definitions.AppendChild(messageElement);
        }
    }

    private void ExportEscalations(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var escalation in model.EscalationMap.Values)
        {
            var escalationElement = document.CreateElement("bpmn", "escalation", BpmnConstants.BpmnNamespace);
            if (escalation.Id != null)
                escalationElement.SetAttribute("id", escalation.Id);
            if (escalation.Name != null)
                escalationElement.SetAttribute("name", escalation.Name);
            if (escalation.EscalationCode != null)
                escalationElement.SetAttribute("escalationCode", escalation.EscalationCode);
            definitions.AppendChild(escalationElement);
        }
    }

    private void ExportImports(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var import in model.Imports)
        {
            var importElement = document.CreateElement("bpmn", "import", BpmnConstants.BpmnNamespace);
            if (import.ImportType != null)
                importElement.SetAttribute("importType", import.ImportType);
            if (import.Location != null)
                importElement.SetAttribute("location", import.Location);
            if (import.Namespace != null)
                importElement.SetAttribute("namespace", import.Namespace);
            definitions.AppendChild(importElement);
        }
    }

    private void ExportItemDefinitions(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var itemDefinition in model.ItemDefinitionMap.Values)
        {
            if (itemDefinition.Id == null)
                continue;

            var itemDefinitionElement = document.CreateElement("bpmn", "itemDefinition", BpmnConstants.BpmnNamespace);
            itemDefinitionElement.SetAttribute("id", itemDefinition.Id);
            if (itemDefinition.StructureRef != null)
                itemDefinitionElement.SetAttribute("structureRef", itemDefinition.StructureRef);
            if (itemDefinition.ItemKind != null)
                itemDefinitionElement.SetAttribute("itemKind", itemDefinition.ItemKind);
            if (itemDefinition.IsCollection)
                itemDefinitionElement.SetAttribute("isCollection", "true");
            definitions.AppendChild(itemDefinitionElement);
        }
    }

    private void ExportInterfaces(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var iface in model.Interfaces)
        {
            if (iface.Id == null)
                continue;

            var interfaceElement = document.CreateElement("bpmn", "interface", BpmnConstants.BpmnNamespace);
            interfaceElement.SetAttribute("id", iface.Id);
            if (iface.Name != null)
                interfaceElement.SetAttribute("name", iface.Name);

            foreach (var operation in iface.Operations)
            {
                var operationElement = document.CreateElement("bpmn", "operation", BpmnConstants.BpmnNamespace);
                if (operation.Id != null)
                    operationElement.SetAttribute("id", operation.Id);
                if (operation.Name != null)
                    operationElement.SetAttribute("name", operation.Name);
                if (operation.ImplementationRef != null)
                    operationElement.SetAttribute("implementationRef", operation.ImplementationRef);
                if (operation.InMessage?.Id != null)
                    operationElement.SetAttribute("inMessageRef", operation.InMessage.Id);
                if (operation.OutMessage?.Id != null)
                    operationElement.SetAttribute("outMessageRef", operation.OutMessage.Id);
                interfaceElement.AppendChild(operationElement);
            }

            definitions.AppendChild(interfaceElement);
        }
    }

    private void ExportResources(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var resource in model.Resources)
        {
            if (resource.Id == null)
                continue;

            var resourceElement = document.CreateElement("bpmn", "resource", BpmnConstants.BpmnNamespace);
            resourceElement.SetAttribute("id", resource.Id);
            if (resource.Name != null)
                resourceElement.SetAttribute("name", resource.Name);
            definitions.AppendChild(resourceElement);
        }
    }

    private void ExportDataStores(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var dataStore in model.DataStoreMap.Values)
        {
            if (dataStore.Id == null)
                continue;

            var dataStoreElement = document.CreateElement("bpmn", "dataStore", BpmnConstants.BpmnNamespace);
            dataStoreElement.SetAttribute("id", dataStore.Id);
            if (dataStore.Name != null)
                dataStoreElement.SetAttribute("name", dataStore.Name);
            if (dataStore.ItemSubjectRef != null)
                dataStoreElement.SetAttribute("itemSubjectRef", dataStore.ItemSubjectRef);
            if (dataStore.Capacity != null)
                dataStoreElement.SetAttribute("capacity", dataStore.Capacity);
            if (!dataStore.IsUnlimited)
                dataStoreElement.SetAttribute("isUnlimited", "false");
            definitions.AppendChild(dataStoreElement);
        }
    }

    private void ExportCollaboration(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        if (model.Pools.Count == 0 && model.MessageFlows.Count == 0)
            return;

        var collaborationElement = document.CreateElement("bpmn", "collaboration", BpmnConstants.BpmnNamespace);
        collaborationElement.SetAttribute("id", "Collaboration");

        foreach (var pool in model.Pools)
        {
            var participantElement = document.CreateElement("bpmn", "participant", BpmnConstants.BpmnNamespace);
            if (pool.Id != null)
                participantElement.SetAttribute("id", pool.Id);
            if (pool.Name != null)
                participantElement.SetAttribute("name", pool.Name);
            if (pool.ProcessRef != null)
                participantElement.SetAttribute("processRef", pool.ProcessRef);
            collaborationElement.AppendChild(participantElement);
        }

        foreach (var messageFlow in model.MessageFlows)
        {
            var messageFlowElement = document.CreateElement("bpmn", "messageFlow", BpmnConstants.BpmnNamespace);
            if (messageFlow.Id != null)
                messageFlowElement.SetAttribute("id", messageFlow.Id);
            if (messageFlow.Name != null)
                messageFlowElement.SetAttribute("name", messageFlow.Name);
            if (messageFlow.SourceRef != null)
                messageFlowElement.SetAttribute("sourceRef", messageFlow.SourceRef);
            if (messageFlow.TargetRef != null)
                messageFlowElement.SetAttribute("targetRef", messageFlow.TargetRef);
            if (messageFlow.MessageRef != null)
                messageFlowElement.SetAttribute("messageRef", messageFlow.MessageRef);
            collaborationElement.AppendChild(messageFlowElement);
        }

        definitions.AppendChild(collaborationElement);
    }

    private void ExportErrors(BpmnModelNs.BpmnModel model, XmlElement definitions, XmlDocument document)
    {
        foreach (var error in model.ErrorMap.Values)
        {
            if (error.Id == null)
                continue;

            var errorElement = document.CreateElement("bpmn", "error", BpmnConstants.BpmnNamespace);
            errorElement.SetAttribute("id", error.Id);
            if (error.Name != null)
                errorElement.SetAttribute("name", error.Name);
            if (error.ErrorCode != null)
                errorElement.SetAttribute("errorCode", error.ErrorCode);
            definitions.AppendChild(errorElement);
        }
    }

    private static string FormatXml(XmlDocument document)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false
        };

        using var stringWriter = new System.IO.StringWriter();
        using var xmlTextWriter = XmlWriter.Create(stringWriter, settings);
        document.WriteTo(xmlTextWriter);
        xmlTextWriter.Flush();
        return stringWriter.GetStringBuilder().ToString();
    }

    private static void SetWorkflowExtensionAttribute(XmlElement element, string localName, string value)
    {
        var attr = element.OwnerDocument.CreateAttribute("activiti", localName, BpmnConstants.WorkflowExtensionNamespace);
        attr.Value = value;
        element.SetAttributeNode(attr);
    }
}
