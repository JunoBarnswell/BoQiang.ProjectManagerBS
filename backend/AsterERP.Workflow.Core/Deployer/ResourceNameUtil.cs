using System;

namespace AsterERP.Workflow.Core.Deployer;

public static class ResourceNameUtil
{
    public static readonly string[] BpmnResourceSuffixes = { "bpmn20.xml", "bpmn" };
    public static readonly string[] DiagramSuffixes = { "png", "jpg", "gif", "svg" };

    public static bool IsBpmnResource(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return false;

        foreach (var suffix in BpmnResourceSuffixes)
        {
            if (resourceName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static string StripBpmnFileSuffix(string bpmnFileResource)
    {
        foreach (var suffix in BpmnResourceSuffixes)
        {
            if (bpmnFileResource.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return bpmnFileResource.Substring(0, bpmnFileResource.Length - suffix.Length);
            }
        }

        return bpmnFileResource;
    }

    public static string GetProcessDiagramResourceName(string bpmnFileResource, string processKey, string diagramSuffix)
    {
        var bpmnFileResourceBase = StripBpmnFileSuffix(bpmnFileResource);
        return bpmnFileResourceBase + processKey + "." + diagramSuffix;
    }
}
