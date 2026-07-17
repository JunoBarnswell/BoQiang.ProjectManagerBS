using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsterERP.Contracts.ApplicationDesigner;

public static class RuntimeCapabilityContract
{
    private const string ResourceName = "AsterERP.Contracts.ApplicationDesigner.runtime-capability.latest.json";
    private static readonly Lazy<JsonDocument> Document = new(LoadDocument);
    private static readonly Lazy<IReadOnlySet<string>> ComponentTypesValue = new(() => ReadSet("components"));
    private static readonly Lazy<IReadOnlySet<string>> InspectorComponentTypesValue = new(() => ReadSet("inspectorComponentTypes"));
    private static readonly Lazy<IReadOnlyDictionary<string, JsonElement>> ComponentCapabilitiesValue = new(ReadComponentCapabilities);
    private static readonly Lazy<IReadOnlySet<string>> ActionTypesValue = new(() => ReadSet("actions"));
    private static readonly Lazy<IReadOnlyDictionary<string, JsonElement>> ActionManifestsValue = new(ReadActionManifests);
    private static readonly Lazy<IReadOnlySet<string>> ConverterNamesValue = new(() => ReadSet("converters"));
    private static readonly Lazy<IReadOnlySet<string>> ScopeNamesValue = new(() => ReadSet("scopes"));

    public static string ContractRevision => ReadString("contractRevision");
    public static string CompilerRevision => ReadString("compilerRevision");
    public static string MigrationRevision => ReadString("migrationRevision");
    public static string Renderer => ReadString("renderer");
    public static IReadOnlySet<string> ComponentTypes => ComponentTypesValue.Value;
    public static IReadOnlySet<string> InspectorComponentTypes => InspectorComponentTypesValue.Value;
    public static IReadOnlyDictionary<string, JsonElement> ComponentCapabilities => ComponentCapabilitiesValue.Value;
    public static IReadOnlySet<string> ActionTypes => ActionTypesValue.Value;
    public static IReadOnlyDictionary<string, JsonElement> ActionManifests => ActionManifestsValue.Value;
    public static IReadOnlySet<string> ConverterNames => ConverterNamesValue.Value;
    public static IReadOnlySet<string> ScopeNames => ScopeNamesValue.Value;

    public static JsonObject BuildArtifactManifest(string type)
    {
        if (!ComponentCapabilities.TryGetValue(type, out var capability))
        {
            throw new InvalidOperationException($"Runtime component capability is not declared: {type}.");
        }

        var renderer = capability.GetProperty("renderer").GetString()
            ?? throw new InvalidOperationException($"Runtime component renderer is missing: {type}.");
        var previewRenderer = capability.GetProperty("previewRenderer").GetString()
            ?? throw new InvalidOperationException($"Runtime component preview renderer is missing: {type}.");
        var inspectorSections = CloneNode(capability.GetProperty("inspectorSections"));
        var declaration = new JsonObject
        {
            ["type"] = type,
            ["renderer"] = new JsonObject { ["runtime"] = Renderer, ["preview"] = Renderer },
            ["inspector"] = new JsonObject { ["sections"] = inspectorSections },
            ["binding"] = CloneNode(capability.GetProperty("binding")),
            ["responsive"] = CloneNode(capability.GetProperty("responsive")),
            ["security"] = CloneNode(capability.GetProperty("security")),
            ["events"] = CloneNode(capability.GetProperty("events")),
            ["defaults"] = CloneNode(capability.GetProperty("defaults")),
            ["migration"] = new JsonObject { ["revision"] = MigrationRevision, ["strategy"] = $"{type}:normalize-v1" },
            ["capability"] = new JsonObject
            {
                ["acceptsChildren"] = capability.GetProperty("acceptsChildren").GetBoolean(),
                ["capabilities"] = CloneNode(capability.GetProperty("capabilities"))
            },
            ["editor"] = new JsonObject
            {
                ["inspectorSections"] = inspectorSections.DeepClone(),
                ["previewRenderer"] = previewRenderer,
                ["selectionMode"] = "single"
            },
            ["runtime"] = new JsonObject
            {
                ["renderer"] = renderer,
                ["supportedScopes"] = CloneNode(capability.GetProperty("supportedScopes"))
            },
            ["i18n"] = new JsonObject
            {
                ["diagnosticKey"] = $"lowCode.component.{type.Replace('.', '_')}.diagnostic",
                ["helpKey"] = $"lowCode.component.{type.Replace('.', '_')}.help",
                ["labelKey"] = $"lowCode.component.{type.Replace('.', '_')}.label"
            },
            ["migrations"] = new JsonArray(new JsonObject { ["from"] = $"{type}@0", ["migrate"] = $"{type}:normalize-v1" }),
            ["validation"] = new JsonObject
            {
                ["schema"] = BuildPropertySchema(capability.GetProperty("defaults").GetProperty("props")),
                ["supportsDiagnostics"] = true
            }
        };
        if (capability.TryGetProperty("editing", out var editing)) declaration["editing"] = CloneNode(editing);
        return declaration;
    }

    private static JsonObject BuildPropertySchema(JsonElement defaults)
    {
        var properties = new JsonObject();
        foreach (var property in defaults.EnumerateObject()) properties[property.Name] = new JsonObject { ["default"] = JsonNode.Parse(property.Value.GetRawText()) };
        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
    }

    private static JsonDocument LoadDocument()
    {
        var stream = typeof(RuntimeCapabilityContract).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded runtime capability contract is missing: {ResourceName}");
        return JsonDocument.Parse(stream);
    }

    private static string ReadString(string propertyName)
    {
        return Document.Value.RootElement.GetProperty(propertyName).GetString()
            ?? throw new InvalidOperationException($"Runtime capability contract property is null: {propertyName}");
    }

    private static IReadOnlySet<string> ReadSet(string propertyName)
    {
        return Document.Value.RootElement.GetProperty(propertyName)
            .EnumerateArray()
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadActionManifests()
    {
        var property = Document.Value.RootElement.GetProperty("actionManifests");
        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Runtime action manifests must be an object.");
        }

        var manifests = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var item in property.EnumerateObject())
        {
            var manifest = item.Value;
            if (manifest.ValueKind != JsonValueKind.Object ||
                !manifest.TryGetProperty("inputSchema", out var inputSchema) || inputSchema.ValueKind != JsonValueKind.Object ||
                !manifest.TryGetProperty("outputSchema", out var outputSchema) || outputSchema.ValueKind != JsonValueKind.Object ||
                !manifest.TryGetProperty("permissions", out var permissions) || permissions.ValueKind != JsonValueKind.Array ||
                !manifest.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array ||
                !manifest.TryGetProperty("cancelable", out var cancelable) || cancelable.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !manifest.TryGetProperty("errorPolicy", out var errorPolicy) || errorPolicy.ValueKind != JsonValueKind.String ||
                !manifest.TryGetProperty("sideEffect", out var sideEffect) || sideEffect.ValueKind != JsonValueKind.String ||
                !manifest.TryGetProperty("timeoutMs", out var timeoutMs) || timeoutMs.ValueKind != JsonValueKind.Number ||
                timeoutMs.GetInt32() <= 0)
            {
                throw new InvalidOperationException($"Runtime action manifest is invalid: {item.Name}.");
            }

            manifests.Add(item.Name, manifest.Clone());
        }

        var actionTypes = ReadSet("actions");
        if (!actionTypes.SetEquals(manifests.Keys))
        {
            throw new InvalidOperationException("Runtime action manifests must exactly match actions.");
        }

        return manifests;
    }

    private static IReadOnlyDictionary<string, JsonElement> ReadComponentCapabilities()
    {
        var property = Document.Value.RootElement.GetProperty("componentCapabilities");
        if (property.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Runtime component capabilities must be an array.");

        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var capability in property.EnumerateArray())
        {
            if (capability.ValueKind != JsonValueKind.Object ||
                capability.GetProperty("id").GetString() is not { Length: > 0 } ||
                capability.GetProperty("types").ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException("Runtime component capability profile is invalid.");
            }

            foreach (var typeNode in capability.GetProperty("types").EnumerateArray())
            {
                var type = typeNode.GetString();
                if (string.IsNullOrWhiteSpace(type) || !result.TryAdd(type, capability.Clone()))
                {
                    throw new InvalidOperationException($"Runtime component capability type is duplicated or invalid: {type}.");
                }
            }
        }

        if (!ComponentTypes.SetEquals(result.Keys)) throw new InvalidOperationException("Runtime component capabilities must exactly match components.");
        if (!InspectorComponentTypes.SetEquals(ComponentTypes)) throw new InvalidOperationException("Runtime inspector component types must exactly match components.");
        return result;
    }

    private static JsonNode CloneNode(JsonElement element) => JsonNode.Parse(element.GetRawText())!;
}
