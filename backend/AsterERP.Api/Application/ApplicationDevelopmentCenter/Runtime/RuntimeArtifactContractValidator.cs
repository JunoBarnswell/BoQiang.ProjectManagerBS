using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;

/// <summary>
/// Validates the publish envelope against the single embedded Runtime Capability
/// Contract without owning a second component, action, or converter registry.
/// </summary>
public static class RuntimeArtifactContractValidator
{
    public static void Validate(JsonObject artifact)
    {
        if (!string.Equals(ReadString(artifact, "compilerVersion"), RuntimeCapabilityContract.CompilerRevision, StringComparison.Ordinal))
        {
            throw Invalid("Runtime artifact compilerVersion is unsupported.");
        }

        if (!string.Equals(ReadString(artifact, "migrationRevision"), RuntimeCapabilityContract.MigrationRevision, StringComparison.Ordinal))
        {
            throw Invalid("Runtime artifact migrationRevision must be latest.");
        }

        var document = artifact["document"] as JsonObject ?? throw Invalid("Runtime artifact document is required.");
        var manifestTypes = artifact["manifestTypes"] as JsonArray ?? throw Invalid("Runtime artifact manifestTypes is required.");
        var manifest = artifact["manifest"] as JsonArray ?? throw Invalid("Runtime artifact manifest is required.");
        ValidateDocument(document, manifestTypes, manifest);
    }

    private static void ValidateDocument(JsonObject document, JsonArray manifestTypes, JsonArray manifest)
    {
        var elementTypes = new HashSet<string>(StringComparer.Ordinal);
        if (document["elements"] is not JsonObject elements || elements.Count == 0)
        {
            throw Invalid("Runtime artifact elements are required.");
        }

        foreach (var (elementId, elementNode) in elements)
        {
            if (elementNode is not JsonObject element)
            {
                throw Invalid($"Runtime artifact element is invalid: {elementId}.");
            }

            var type = ReadString(element, "type");
            if (!RuntimeCapabilityContract.ComponentTypes.Contains(type))
            {
                throw Invalid($"Runtime artifact component type is not registered: {type}.");
            }

            elementTypes.Add(type);
            ValidateBindings(element, $"elements.{elementId}");
            ValidateActionArray(element["events"], $"elements.{elementId}.events");
        }

        ValidateManifest(elementTypes, manifestTypes, manifest);
        ValidateActionArray(document["actions"], "actions");
        ValidateDocumentBindingCollections(document);
    }

    /// <summary>
    /// Validates executable binding surfaces without treating document definitions as
    /// binding expressions. In particular, variables retain their formal source/path
    /// descriptor so the runtime can expose their origin; that descriptor is not a
    /// retired expression payload.
    /// </summary>
    private static void ValidateDocumentBindingCollections(JsonObject document)
    {
        foreach (var name in new[] { "apiBindings", "dataSources", "pageMicroflows", "pageParameters", "workflowBindings" })
        {
            if (document[name] is JsonNode value)
            {
                ValidateBindings(value, $"document.{name}");
            }
        }
    }

    private static void ValidateManifest(HashSet<string> elementTypes, JsonArray manifestTypes, JsonArray manifest)
    {
        var declaredTypes = manifestTypes.Select((node, index) => ReadString(node, $"manifestTypes[{index}]")).ToHashSet(StringComparer.Ordinal);
        if (declaredTypes.Count != manifestTypes.Count || !declaredTypes.SetEquals(elementTypes))
        {
            throw Invalid("Runtime artifact manifestTypes must exactly match registered element component types.");
        }

        var declarationTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (index, node) in manifest.Select((item, index) => (index, item)))
        {
            if (node is not JsonObject declaration)
            {
                throw Invalid($"Runtime artifact manifest declaration is invalid: {index}.");
            }

            var type = ReadString(declaration, "type");
            if (!RuntimeCapabilityContract.ComponentTypes.Contains(type) || !declarationTypes.Add(type))
            {
                throw Invalid($"Runtime artifact manifest component type is not registered or duplicated: {type}.");
            }

            if (declaration["renderer"] is not JsonObject renderer ||
                !string.Equals(ReadString(renderer, "runtime"), RuntimeCapabilityContract.Renderer, StringComparison.Ordinal) ||
                !string.Equals(ReadString(renderer, "preview"), RuntimeCapabilityContract.Renderer, StringComparison.Ordinal))
            {
                throw Invalid($"Runtime artifact renderer contract is invalid for component: {type}.");
            }

            var canonicalDeclaration = RuntimeCapabilityContract.BuildArtifactManifest(type);
            var actualJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(declaration.ToJsonString());
            var canonicalJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(canonicalDeclaration.ToJsonString());
            if (!string.Equals(actualJson, canonicalJson, StringComparison.Ordinal))
            {
                throw Invalid($"Runtime artifact manifest capability drift detected for component: {type}.");
            }
        }

        if (!declarationTypes.SetEquals(declaredTypes))
        {
            throw Invalid("Runtime artifact manifest declarations must exactly match manifestTypes.");
        }
    }

    private static void ValidateActionArray(JsonNode? node, string path)
    {
        if (node is null) return;
        if (node is not JsonArray actions) throw Invalid($"Runtime action collection is invalid: {path}.");

        foreach (var (actionNode, index) in actions.Select((item, index) => (item, index)))
        {
            if (actionNode is not JsonObject action || action["steps"] is not JsonArray steps)
            {
                throw Invalid($"Runtime action must contain steps: {path}[{index}].");
            }

            foreach (var (stepNode, stepIndex) in steps.Select((item, stepIndex) => (item, stepIndex)))
            {
                if (stepNode is not JsonObject step)
                {
                    throw Invalid($"Runtime action step is invalid: {path}[{index}].steps[{stepIndex}].");
                }

                var actionType = ReadString(step, "type");
                if (!RuntimeCapabilityContract.ActionTypes.Contains(actionType) ||
                    !RuntimeCapabilityContract.ActionManifests.ContainsKey(actionType))
                {
                    throw Invalid($"Runtime action type is not registered: {actionType}.");
                }

                ValidateBindings(step, $"{path}[{index}].steps[{stepIndex}]");
            }
        }
    }

    private static void ValidateBindings(JsonNode node, string path)
    {
        if (node is JsonArray array)
        {
            foreach (var (item, index) in array.Select((value, index) => (value, index)))
            {
                if (item is not null) ValidateBindings(item, $"{path}[{index}]");
            }
            return;
        }

        if (node is not JsonObject obj) return;
        var isMicroflowExpression = path.EndsWith(".sourceExpression", StringComparison.Ordinal) ||
            path.EndsWith(".valueExpression", StringComparison.Ordinal);
        if (!isMicroflowExpression && (obj.ContainsKey("source") || obj.ContainsKey("path")))
        {
            throw Invalid($"Runtime binding uses retired source/path fields: {path}.");
        }
        if (path.Contains(".bindings.props", StringComparison.Ordinal))
        {
            throw Invalid($"Runtime property binding must be stored in props/layout/style: {path}.");
        }
        if (obj["kind"] is JsonValue kind && kind.TryGetValue<string>(out var kindName) &&
            string.Equals(kindName, "expression", StringComparison.OrdinalIgnoreCase) &&
            (obj["graph"] is not JsonObject || obj["expectedType"] is not JsonValue expectedType || !expectedType.TryGetValue<string>(out _)))
        {
            throw Invalid($"Runtime ExpressionValue requires graph and expectedType: {path}.");
        }
        if (obj.TryGetPropertyValue("resourceId", out var resourceId) && string.IsNullOrWhiteSpace(ReadString(resourceId, $"{path}.resourceId")))
        {
            throw Invalid($"Runtime binding resourceId is required: {path}.");
        }

        if (obj.TryGetPropertyValue("conversionPipeline", out var pipelineNode))
        {
            if (pipelineNode is not JsonArray pipeline) throw Invalid($"Runtime conversionPipeline is invalid: {path}.");
            foreach (var (stepNode, index) in pipeline.Select((item, index) => (item, index)))
            {
                if (stepNode is not JsonObject step ||
                    string.IsNullOrWhiteSpace(ReadString(step, "from")) || string.IsNullOrWhiteSpace(ReadString(step, "to")))
                {
                    throw Invalid($"Runtime converter step is invalid: {path}.conversionPipeline[{index}].");
                }

                var converterName = ReadString(step, "name");
                if (!RuntimeCapabilityContract.ConverterNames.Contains(converterName))
                {
                    throw Invalid($"Runtime converter is not registered: {converterName}.");
                }
            }
        }

        foreach (var (name, child) in obj)
        {
            if (child is not null) ValidateBindings(child, $"{path}.{name}");
        }
    }

    private static string ReadString(JsonNode? node, string path)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)) return text.Trim();
        throw Invalid($"Runtime artifact requires a non-empty string: {path}.");
    }

    private static string ReadString(JsonObject obj, string propertyName) => ReadString(obj[propertyName], propertyName);

    private static ValidationException Invalid(string message) => new(message, ErrorCodes.DesignerSchemaInvalid);
}
