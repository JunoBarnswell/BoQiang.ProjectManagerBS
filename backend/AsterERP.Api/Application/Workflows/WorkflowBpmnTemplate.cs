using System.Security;

namespace AsterERP.Api.Application.Workflows;

public static class WorkflowBpmnTemplate
{
    public static string Create(string processKey, string processName, string assignee)
    {
        var key = Escape(processKey);
        var name = Escape(processName);
        var user = Escape(assignee);

        return $$"""
<?xml version="1.0" encoding="UTF-8"?>
<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
             xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
             xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
             xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"
             xmlns:di="http://www.omg.org/spec/DD/20100524/DI"
             xmlns:activiti="http://activiti.org/bpmn"
             targetNamespace="http://astererp/workflow">
  <process id="{{key}}" name="{{name}}" isExecutable="true">
    <startEvent id="start" name="开始" />
    <sequenceFlow id="flow_start_approve" sourceRef="start" targetRef="approveTask" />
    <userTask id="approveTask" name="审批" activiti:assignee="{{user}}" />
    <sequenceFlow id="flow_approve_end" sourceRef="approveTask" targetRef="end" />
    <endEvent id="end" name="结束" />
  </process>
  <bpmndi:BPMNDiagram id="BPMNDiagram_{{key}}">
    <bpmndi:BPMNPlane id="BPMNPlane_{{key}}" bpmnElement="{{key}}">
      <bpmndi:BPMNShape id="start_di" bpmnElement="start">
        <dc:Bounds x="160" y="120" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="approveTask_di" bpmnElement="approveTask">
        <dc:Bounds x="260" y="98" width="120" height="80" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNShape id="end_di" bpmnElement="end">
        <dc:Bounds x="460" y="120" width="36" height="36" />
      </bpmndi:BPMNShape>
      <bpmndi:BPMNEdge id="flow_start_approve_di" bpmnElement="flow_start_approve">
        <di:waypoint x="196" y="138" />
        <di:waypoint x="260" y="138" />
      </bpmndi:BPMNEdge>
      <bpmndi:BPMNEdge id="flow_approve_end_di" bpmnElement="flow_approve_end">
        <di:waypoint x="380" y="138" />
        <di:waypoint x="460" y="138" />
      </bpmndi:BPMNEdge>
    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</definitions>
""";
    }

    private static string Escape(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
    }
}
