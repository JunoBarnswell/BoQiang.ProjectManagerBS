using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public static class BpmnConverterRegistry
{
    private static readonly Dictionary<string, BaseBpmnXmlConverter> ConvertersByElementName = new();
    private static readonly Dictionary<Type, BaseBpmnXmlConverter> ConvertersByModelType = new();

    static BpmnConverterRegistry()
    {
        RegisterConverter(new StartEventXmlConverter());
        RegisterConverter(new EndEventXmlConverter());
        RegisterConverter(new UserTaskXmlConverter());
        RegisterConverter(new ServiceTaskXmlConverter());
        RegisterConverter(new ScriptTaskXmlConverter());
        RegisterConverter(new ReceiveTaskXmlConverter());
        RegisterConverter(new SendTaskXmlConverter());
        RegisterConverter(new ManualTaskXmlConverter());
        RegisterConverter(new BusinessRuleTaskXmlConverter());
        RegisterConverter(new ExclusiveGatewayXmlConverter());
        RegisterConverter(new ParallelGatewayXmlConverter());
        RegisterConverter(new InclusiveGatewayXmlConverter());
        RegisterConverter(new ComplexGatewayXmlConverter());
        RegisterConverter(new SequenceFlowXmlConverter());
        RegisterConverter(new BoundaryEventXmlConverter());
        RegisterConverter(new IntermediateCatchEventXmlConverter());
        RegisterConverter(new IntermediateThrowEventXmlConverter());
        RegisterConverter(new EventGatewayXmlConverter());
        RegisterConverter(new AssociationXmlConverter());
        RegisterConverter(new DataStoreReferenceXmlConverter());
        RegisterConverter(new TextAnnotationXmlConverter());
        RegisterConverter(new ValuedDataObjectXmlConverter());
        RegisterConverter(new SubProcessXmlConverter());
        RegisterConverter(new TransactionXmlConverter());
        RegisterConverter(new CallActivityXmlConverter());
    }

    private static void RegisterConverter(BaseBpmnXmlConverter converter)
    {
        foreach (var elementType in converter.ElementTypes)
        {
            ConvertersByElementName[elementType] = converter;
        }
        var modelType = GetModelTypeForConverter(converter);
        if (modelType != null)
        {
            ConvertersByModelType[modelType] = converter;
        }
    }

    private static Type? GetModelTypeForConverter(BaseBpmnXmlConverter converter)
    {
        return converter switch
        {
            StartEventXmlConverter => typeof(BpmnModelNs.StartEvent),
            EndEventXmlConverter => typeof(BpmnModelNs.EndEvent),
            UserTaskXmlConverter => typeof(BpmnModelNs.UserTask),
            ServiceTaskXmlConverter => typeof(BpmnModelNs.ServiceTask),
            ScriptTaskXmlConverter => typeof(BpmnModelNs.ScriptTask),
            ReceiveTaskXmlConverter => typeof(BpmnModelNs.ReceiveTask),
            SendTaskXmlConverter => typeof(BpmnModelNs.SendTask),
            ManualTaskXmlConverter => typeof(BpmnModelNs.ManualTask),
            BusinessRuleTaskXmlConverter => typeof(BpmnModelNs.BusinessRuleTask),
            ExclusiveGatewayXmlConverter => typeof(BpmnModelNs.ExclusiveGateway),
            ParallelGatewayXmlConverter => typeof(BpmnModelNs.ParallelGateway),
            InclusiveGatewayXmlConverter => typeof(BpmnModelNs.InclusiveGateway),
            ComplexGatewayXmlConverter => typeof(BpmnModelNs.ComplexGateway),
            SequenceFlowXmlConverter => typeof(BpmnModelNs.SequenceFlow),
            BoundaryEventXmlConverter => typeof(BpmnModelNs.BoundaryEvent),
            IntermediateCatchEventXmlConverter => typeof(BpmnModelNs.IntermediateCatchEvent),
            IntermediateThrowEventXmlConverter => typeof(BpmnModelNs.IntermediateThrowEvent),
            EventGatewayXmlConverter => typeof(BpmnModelNs.EventGateway),
            AssociationXmlConverter => typeof(BpmnModelNs.Association),
            DataStoreReferenceXmlConverter => typeof(BpmnModelNs.DataStoreReference),
            TextAnnotationXmlConverter => typeof(BpmnModelNs.TextAnnotation),
            ValuedDataObjectXmlConverter => typeof(BpmnModelNs.ValuedDataObject),
            SubProcessXmlConverter => typeof(BpmnModelNs.SubProcess),
            TransactionXmlConverter => typeof(BpmnModelNs.Transaction),
            CallActivityXmlConverter => typeof(BpmnModelNs.CallActivity),
            _ => null
        };
    }

    public static BaseBpmnXmlConverter? GetConverter(string elementName)
    {
        return ConvertersByElementName.GetValueOrDefault(elementName);
    }

    public static BaseBpmnXmlConverter? GetConverterForElement(BpmnModelNs.BaseElement element)
    {
        var type = element.GetType();
        if (ConvertersByModelType.TryGetValue(type, out var converter))
            return converter;
        foreach (var kvp in ConvertersByModelType)
        {
            if (kvp.Key.IsAssignableFrom(type))
                return kvp.Value;
        }
        return null;
    }
}
