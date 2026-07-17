using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public static class WorkflowJsonOptions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
