using System.Security;
using System.Text;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowBpmnDraftMapper
{
    public string Map(AiWorkflowDraftDto draft)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        builder.AppendLine("""<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:activiti="http://activiti.org/bpmn" targetNamespace="http://astererp.ai/workflow">""");
        builder.AppendLine($"""  <process id="{Escape(draft.WorkflowKey)}" name="{Escape(draft.WorkflowName)}" isExecutable="true">""");
        foreach (var node in draft.Nodes)
        {
            AppendNode(builder, node);
        }

        foreach (var edge in draft.Edges)
        {
            AppendEdge(builder, edge);
        }

        builder.AppendLine("  </process>");
        builder.AppendLine("</definitions>");
        return builder.ToString();
    }

    private static void AppendNode(StringBuilder builder, AiWorkflowDraftNodeDto node)
    {
        var id = Escape(node.Id);
        var name = Escape(node.Name);
        if (node.Type.Equals("startEvent", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"""    <startEvent id="{id}" name="{name}" />""");
            return;
        }

        if (node.Type.Equals("endEvent", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"""    <endEvent id="{id}" name="{name}" />""");
            return;
        }

        var groups = string.Join(',', node.CandidateRoles);
        var users = string.Join(',', node.CandidateUsers);
        var candidates = string.IsNullOrWhiteSpace(groups)
            ? string.IsNullOrWhiteSpace(users) ? string.Empty : $""" activiti:candidateUsers="{Escape(users)}" """
            : $""" activiti:candidateGroups="{Escape(groups)}" """;
        builder.AppendLine($"""    <userTask id="{id}" name="{name}"{candidates}/>""");
    }

    private static void AppendEdge(StringBuilder builder, AiWorkflowDraftEdgeDto edge)
    {
        var id = Escape(edge.Id);
        var source = Escape(edge.SourceId);
        var target = Escape(edge.TargetId);
        var name = string.IsNullOrWhiteSpace(edge.Name) ? string.Empty : $""" name="{Escape(edge.Name)}" """;
        if (string.IsNullOrWhiteSpace(edge.Condition))
        {
            builder.AppendLine($"""    <sequenceFlow id="{id}"{name}sourceRef="{source}" targetRef="{target}" />""");
            return;
        }

        builder.AppendLine($"""    <sequenceFlow id="{id}"{name}sourceRef="{source}" targetRef="{target}">""");
        builder.AppendLine("      <conditionExpression xsi:type=\"tFormalExpression\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">${" + Escape(edge.Condition) + "}</conditionExpression>");
        builder.AppendLine("    </sequenceFlow>");
    }

    private static string Escape(string? value) => SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
}
