using System.Text.Json;
using AsterERP.Api.Modules.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Workflows.Callbacks;

/// <summary>Reads and writes only the versioned workflow callback contract.</summary>
public sealed class WorkflowCallbackConfigParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public WorkflowCallbackConfigDto? ParsePersisted(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("version", out var versionElement) ||
                !string.Equals(versionElement.GetString(), "latest", StringComparison.Ordinal))
            {
                throw new ValidationException("Workflow callback configuration requires migration before execution.", ErrorCodes.ParameterInvalid);
            }

            var config = EmptyToNull(JsonSerializer.Deserialize<WorkflowCallbackConfigDto>(json, JsonOptions));
            if (config is not null && !string.Equals(config.Version, "latest", StringComparison.Ordinal))
            {
                throw new ValidationException("Workflow callback configuration requires migration before execution.", ErrorCodes.ParameterInvalid);
            }

            return config;
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"Workflow callback configuration is invalid and is MigrationBlocked: {ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    public WorkflowCallbackConfigDto? ResolveEffectiveConfig(WorkflowBindingEntity binding) => ParsePersisted(binding.BindingConfigJson);

    public string? Serialize(WorkflowCallbackConfigDto? config)
    {
        if (config is not null && !string.Equals(config.Version, "latest", StringComparison.Ordinal))
        {
            throw new ValidationException("Workflow callback configuration version must be latest.", ErrorCodes.ParameterInvalid);
        }

        if (config?.Rules is null || config.Rules.Count == 0) return null;

        return JsonSerializer.Serialize(config, JsonOptions);
    }

    private static WorkflowCallbackConfigDto? EmptyToNull(WorkflowCallbackConfigDto? config) =>
        config?.Rules is { Count: > 0 } ? config : null;
}
