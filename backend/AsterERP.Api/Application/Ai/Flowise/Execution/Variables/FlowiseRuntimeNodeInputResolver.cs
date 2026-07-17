using System.Text.Json;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseRuntimeNodeInputResolver(
    FlowiseRuntimeNodeDataReader nodeDataReader,
    FlowiseVariableResolver variableResolver)
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal IReadOnlyList<KeyValuePair<string, object?>> ReadStartStateUpdates(IReadOnlyDictionary<string, JsonElement> data)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, "startState", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(FlowiseJsonElementReader.NormalizeJsonElement(value));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new ValidationException("Start Flow State 必须是 JSON array", ErrorCodes.ParameterInvalid);
            }

            var states = new List<KeyValuePair<string, object?>>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object ||
                    !item.TryGetProperty("key", out var keyElement) ||
                    keyElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(keyElement.GetString()))
                {
                    continue;
                }

                var key = keyElement.GetString()!.Trim();
                object? stateValue = null;
                if (item.TryGetProperty("value", out var valueElement) && valueElement.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
                {
                    stateValue = valueElement.ValueKind == JsonValueKind.String
                        ? valueElement.GetString()
                        : valueElement.Clone();
                }

                states.Add(new KeyValuePair<string, object?>(key, stateValue));
            }

            return states;
        }
        catch (JsonException)
        {
            throw new ValidationException("Start Flow State 必须是 JSON array", ErrorCodes.ParameterInvalid);
        }
    }

    internal IReadOnlyList<RuntimeFilterRequest>? ReadRuntimeFilters(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        var json = ResolveRuntimeNodeInputJson(data, propertyName, context, iterationContext, previousResults);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<RuntimeFilterRequest>>(json, CaseInsensitiveJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new ValidationException("Runtime Data Model filters 必须是 JSON array", ErrorCodes.ParameterInvalid);
        }
    }

    internal IReadOnlyList<RuntimeSortRequest>? ReadRuntimeSorts(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        var json = ResolveRuntimeNodeInputJson(data, propertyName, context, iterationContext, previousResults);
        if (json is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<RuntimeSortRequest>>(json, CaseInsensitiveJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new ValidationException("Runtime Data Model sorts 必须是 JSON array", ErrorCodes.ParameterInvalid);
        }
    }

    internal string? ResolveRuntimeNodeInputJson(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName,
        FlowiseExecutionContext context,
        FlowiseIterationContext? iterationContext,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        var json = FlowiseJsonElementReader.NormalizeJsonElement(value);
        return variableResolver.ReplaceRuntimeVariables(json, context, iterationContext, previousResults);
    }
}
