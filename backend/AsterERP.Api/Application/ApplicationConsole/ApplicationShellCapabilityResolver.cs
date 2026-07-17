using System.Text.Json;

namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationShellCapabilityResolver
{
    public const string AiCapability = "AI";
    public const string AsterSceneCapability = "AsterScene";
    public const string ImCapability = "IM";
    public const string SystemAdministrationCapability = "SystemAdministration";
    public const string WorkflowCapability = "Workflow";

    private static readonly IReadOnlySet<string> CanonicalCapabilities =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AiCapability,
            AsterSceneCapability,
            ImCapability,
            SystemAdministrationCapability,
            WorkflowCapability
        };

    public IReadOnlySet<string> Resolve(string? tenantAppConfigJson)
    {
        if (string.IsNullOrWhiteSpace(tenantAppConfigJson))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(tenantAppConfigJson);
            if (!document.RootElement.TryGetProperty("shellCapabilities", out var capabilitiesElement) ||
                capabilitiesElement.ValueKind != JsonValueKind.Array)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var capabilityElement in capabilitiesElement.EnumerateArray())
            {
                if (capabilityElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var rawCapability = capabilityElement.GetString();
                if (string.IsNullOrWhiteSpace(rawCapability))
                {
                    continue;
                }

                var canonical = CanonicalCapabilities.FirstOrDefault(item =>
                    string.Equals(item, rawCapability.Trim(), StringComparison.OrdinalIgnoreCase));
                if (canonical is not null)
                {
                    capabilities.Add(canonical);
                }
            }

            return capabilities;
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public bool HasExplicitShellCapabilities(string? tenantAppConfigJson)
    {
        if (string.IsNullOrWhiteSpace(tenantAppConfigJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(tenantAppConfigJson);
            return document.RootElement.TryGetProperty("shellCapabilities", out var capabilitiesElement) &&
                capabilitiesElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
