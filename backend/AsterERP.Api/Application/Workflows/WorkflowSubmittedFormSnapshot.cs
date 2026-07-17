using System.Text.Json;
using AsterERP.Contracts.Workflows;

namespace AsterERP.Api.Application.Workflows;

public static class WorkflowSubmittedFormSnapshot
{
    public const string SubmittedSnapshotSource = "submittedSnapshot";
    public const string RuntimeSnapshotFallbackSource = "runtimeSnapshotFallback";

    private static readonly HashSet<string> SystemVariableKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "tenantId",
        "appCode",
        "menuCode",
        "businessType",
        "businessKey",
        "starterUserId",
        "starterUserName",
        "approvalAction",
        "approvalUserId",
        "approvalComment"
    };

    public static string Capture(Dictionary<string, object?>? variables)
    {
        return WorkflowJson.Serialize(FilterSubmittedVariables(variables));
    }

    public static WorkflowSubmittedFormResponse Build(string? submittedFormJson, string? variableSnapshotJson)
    {
        return Build(submittedFormJson, variableSnapshotJson, null);
    }

    public static WorkflowSubmittedFormResponse Build(
        string? submittedFormJson,
        string? variableSnapshotJson,
        IReadOnlyDictionary<string, string>? labels)
    {
        var submittedFields = WorkflowJson.DeserializeVariables(submittedFormJson);
        if (!string.IsNullOrWhiteSpace(submittedFormJson))
        {
            return ToResponse(SubmittedSnapshotSource, submittedFields, labels);
        }

        var fallbackFields = FilterSubmittedVariables(WorkflowJson.DeserializeVariables(variableSnapshotJson));
        return ToResponse(RuntimeSnapshotFallbackSource, fallbackFields, labels);
    }

    public static Dictionary<string, object?> FilterSubmittedVariables(Dictionary<string, object?>? variables)
    {
        if (variables is null || variables.Count == 0)
        {
            return [];
        }

        return variables
            .Where(item => IsBusinessField(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsBusinessField(string key)
    {
        return !string.IsNullOrWhiteSpace(key) &&
               !key.StartsWith('_') &&
               !SystemVariableKeys.Contains(key);
    }

    private static WorkflowSubmittedFormResponse ToResponse(
        string source,
        IReadOnlyDictionary<string, object?> fields,
        IReadOnlyDictionary<string, string>? labels)
    {
        return new WorkflowSubmittedFormResponse(
            source,
            fields
                .Select(item => new WorkflowSubmittedFormFieldResponse(
                    item.Key,
                    ResolveLabel(item.Key, labels),
                    item.Value,
                    ResolveValueType(item.Value)))
                .ToList());
    }

    private static string ResolveLabel(string field, IReadOnlyDictionary<string, string>? labels)
    {
        return labels is not null && labels.TryGetValue(field, out var label) && !string.IsNullOrWhiteSpace(label)
            ? label
            : field;
    }

    private static string? ResolveValueType(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement jsonElement => jsonElement.ValueKind.ToString(),
            _ => value.GetType().Name
        };
    }
}
