using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AsterERP.Workflow.Common;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.BpmnParser;

public static class BpmnConstants
{
    public const string BpmnNamespace = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    public const string BpmnDiNamespace = "http://www.omg.org/spec/BPMN/20100524/DI";
    public const string OmgDcNamespace = "http://www.omg.org/spec/DD/20100524/DC";
    public const string OmgDiNamespace = "http://www.omg.org/spec/DD/20100524/DI";
    public const string WorkflowExtensionNamespace = "http://AsterERP.Workflow.org/bpmn";
    public const string TargetNamespace = "http://AsterERP.Workflow.org/bpmn";
    public const string BpmnPrefix = "bpmn";
    public const string WorkflowExtensionPrefix = "activiti";
}

