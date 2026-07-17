using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

/// <summary>
/// Validates the single latest Designer Document contract at persistence and runtime boundaries.
/// Numeric schema-version negotiation is intentionally not supported.
/// </summary>
public sealed class ApplicationDevelopmentSchemaValidator
{
    public const int DraftMaximumBytes = 2 * 1024 * 1024;
    // The runtime artifact contains the executable projection of the document and its integrity payload.
    // 2000-node documents measured at roughly 507 KiB; keep headroom for manifest and runtime metadata.
    public const int RuntimeMaximumBytes = 1024 * 1024;
    public const int MaximumElements = 2_000;
    public const int MaximumDepth = 64;
    public const int MaximumChildren = 1_000;
    public const int MaximumActions = 200;
    public const int MaximumStepsPerAction = 50;
    public const int MaximumValuePathSegments = 32;

    private static readonly HashSet<string> EditorStateProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "schemaVersion", "tree", "selectedElementId", "selectedNodeIds", "primaryNodeId", "anchorNodeId",
        "viewport", "history", "historyIndex", "dirty", "saving", "selection", "editorState", "runtimeEditorState",
        "panelState", "transactionId", "editorSession"
    };

    private static readonly HashSet<string> ValuePathProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "fieldPath", "valuePath", "variablePath", "outputPath", "sourcePath", "targetPath", "pathSegments"
    };

    private static readonly string[] LayoutPlacementPayloadNames = ["absolute", "flexItem", "gridItem", "constrained"];

    private static readonly HashSet<string> LayoutModes = new(StringComparer.Ordinal)
    {
        "free", "flex", "grid", "constraints"
    };

    public JsonObject ValidateDraft(string json) => Validate(json, DraftMaximumBytes, "draft");

    public JsonObject ValidateRuntimeArtifact(string json) => Validate(json, RuntimeMaximumBytes, "runtime artifact");

    public static JsonObject RemoveRuntimeEditorState(JsonObject document)
    {
        var clone = (JsonObject)document.DeepClone();
        foreach (var property in EditorStateProperties)
        {
            clone.Remove(property);
        }

        return clone;
    }

    private static JsonObject Validate(string json, int maximumBytes, string artifactName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw Invalid($"{artifactName} cannot be empty");
        }

        if (Encoding.UTF8.GetByteCount(json) > maximumBytes)
        {
            throw TooLarge($"{artifactName} exceeds {maximumBytes / 1024} KiB");
        }

        try
        {
            using var parsed = JsonDocument.Parse(json);
            RejectDuplicateProperties(parsed.RootElement, "$");
            var document = JsonNode.Parse(json) as JsonObject ?? throw Invalid($"{artifactName} must be a JSON object");
            ValidateShape(document);
            return document;
        }
        catch (JsonException exception)
        {
            throw Invalid($"{artifactName} is invalid JSON: {exception.Message}");
        }
    }

    private static void ValidateShape(JsonObject document)
    {
        RejectEditorState(document);
        RejectPrototypePollution(document);
        RequireDocumentIdentity(document);
        ValidateJsonGraph(document);
        ValidateElements(document);
        ValidateActions(document);
        ValidateValuePaths(document);
    }

    private static void RequireDocumentIdentity(JsonObject document)
    {
        if (string.IsNullOrWhiteSpace(ReadNullableString(document, "documentId")))
        {
            throw Invalid("Designer Document requires documentId");
        }

        if (document["revision"] is not JsonValue revision ||
            !revision.TryGetValue<int>(out var revisionNumber) || revisionNumber < 1)
        {
            throw Invalid("Designer Document requires a positive revision");
        }
    }

    private static void RejectEditorState(JsonObject document)
    {
        var found = document.Select(item => item.Key).FirstOrDefault(EditorStateProperties.Contains);
        if (found is not null)
        {
            throw Invalid($"Designer Document cannot contain editor-session property: {found}");
        }
    }

    private static void ValidateElements(JsonObject document)
    {
        if (document["elements"] is not JsonObject elements || elements.Count == 0)
        {
            throw Invalid("Designer Document requires a non-empty elements object");
        }

        if (elements.Count > MaximumElements)
        {
            throw TooLarge($"elements cannot exceed {MaximumElements}");
        }

        var parentByChild = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (elementId, node) in elements)
        {
            if (string.IsNullOrWhiteSpace(elementId) || node is not JsonObject element)
            {
                throw Invalid("elements must contain valid node objects");
            }

            if (element.ContainsKey("dataBinding"))
            {
                throw Invalid($"elements.{elementId}.dataBinding is not supported; use bindings.data");
            }

            ValidateLatestPropertyValues(element["props"], $"elements.{elementId}.props");
            ValidateLatestPropertyValues(element["layout"], $"elements.{elementId}.layout");
            ValidateLatestPropertyValues(element["style"], $"elements.{elementId}.style");
            ValidateLatestBindings(element["bindings"], $"elements.{elementId}.bindings");
            ValidateCanonicalLayoutProtocol(element["layout"], $"elements.{elementId}.layout");

            if (!string.Equals(ReadRequiredString(element, "id", $"elements.{elementId}"), elementId, StringComparison.Ordinal))
            {
                throw Invalid($"elements key and node id differ: {elementId}");
            }

            var parentId = ReadNullableString(element, "parentId");
            if (parentId is not null && !elements.ContainsKey(parentId))
            {
                throw Invalid($"node {elementId} references missing parentId {parentId}");
            }

            var children = ReadChildren(element, elementId);
            if (children.Count > MaximumChildren)
            {
                throw TooLarge($"node {elementId} exceeds {MaximumChildren} children");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var childId in children)
            {
                if (!seen.Add(childId) || !elements.ContainsKey(childId))
                {
                    throw Invalid($"node {elementId} contains duplicate or missing child {childId}");
                }

                if (parentByChild.TryGetValue(childId, out var existingParent) && existingParent != elementId)
                {
                    throw Invalid($"node {childId} has multiple parents");
                }

                parentByChild[childId] = elementId;
            }
        }

        if (document["pages"] is not JsonArray pages || pages.Count == 0)
        {
            throw Invalid("Designer Document requires at least one page");
        }

        var roots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pageNode in pages)
        {
            if (pageNode is not JsonObject page)
            {
                throw Invalid("pages must contain objects");
            }

            var rootId = ReadRequiredString(page, "rootElementId", "pages");
            if (!roots.Add(rootId) || !elements.ContainsKey(rootId))
            {
                throw Invalid($"invalid or duplicate page root: {rootId}");
            }
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rootId in roots)
        {
            VisitElement(rootId, elements, visited, new HashSet<string>(StringComparer.Ordinal), 1);
        }

        if (visited.Count != elements.Count)
        {
            throw Invalid("Designer Document contains unreachable nodes");
        }

        foreach (var (elementId, node) in elements)
        {
            var parentId = ReadNullableString((JsonObject)node!, "parentId");
            if (parentId is null && !roots.Contains(elementId))
            {
                throw Invalid($"non-root node {elementId} has no parent");
            }

            if (parentId is not null && (!parentByChild.TryGetValue(elementId, out var actual) || actual != parentId))
            {
                throw Invalid($"node {elementId} parentId and parent children disagree");
            }
        }
    }

    private static void ValidateLatestBindings(JsonNode? node, string path)
    {
        if (node is not JsonObject bindings)
        {
            return;
        }

        foreach (var (name, child) in bindings)
        {
            if (name.Equals("props", StringComparison.Ordinal) || name.StartsWith("props.", StringComparison.Ordinal))
            {
                throw Invalid($"{path}.{name} is a legacy property-binding location; use props/layout/style");
            }

            ValidateLatestPropertyValues(child, $"{path}.{name}");
        }
    }

    private static void ValidateLatestPropertyValues(JsonNode? node, string path)
    {
        if (node is JsonObject value)
        {
            var isMicroflowExpression = path.EndsWith(".sourceExpression", StringComparison.Ordinal) ||
                path.EndsWith(".valueExpression", StringComparison.Ordinal);
            if (!isMicroflowExpression && (value.ContainsKey("source") || value.ContainsKey("path")))
            {
                throw Invalid($"{path} uses legacy source/path binding fields; migrate it before latest Designer validation");
            }

            if (value["kind"] is JsonValue kind && kind.TryGetValue<string>(out var kindName) &&
                string.Equals(kindName, "expression", StringComparison.OrdinalIgnoreCase) &&
                (value["graph"] is not JsonObject || value["expectedType"] is not JsonValue expectedType || !expectedType.TryGetValue<string>(out _)))
            {
                throw Invalid($"{path} ExpressionValue requires graph and expectedType");
            }

            foreach (var (name, child) in value)
            {
                ValidateLatestPropertyValues(child, $"{path}.{name}");
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var (child, index) in array.Select((item, index) => (item, index)))
            {
                ValidateLatestPropertyValues(child, $"{path}[{index}]");
            }
        }
    }

    private static void ValidateCanonicalLayoutProtocol(JsonNode? layoutNode, string layoutPath)
    {
        // Documents without layout.protocol are existing legacy documents and remain valid.
        if (layoutNode is not JsonObject layout || !layout.ContainsKey("protocol"))
        {
            return;
        }

        const string protocolProperty = "protocol";
        var protocolPath = $"{layoutPath}.{protocolProperty}";
        if (layout[protocolProperty] is not JsonObject protocol)
        {
            throw LayoutInvalid(protocolPath, "LAYOUT_PROTOCOL_INVALID", "must be a JSON object");
        }

        var container = RequireLayoutObject(protocol, "container", protocolPath);
        var placement = RequireLayoutObject(protocol, "placement", protocolPath);
        var size = RequireLayoutObject(protocol, "size", protocolPath);
        var mode = ReadLayoutString(container, "mode", $"{protocolPath}.container.mode", "LAYOUT_CONTAINER_MODE_INVALID");

        if (!LayoutModes.Contains(mode))
        {
            throw LayoutInvalid($"{protocolPath}.container.mode", "LAYOUT_CONTAINER_MODE_INVALID", "must be free, flex, grid, or constraints");
        }

        ValidateLayoutContainer(container, mode, protocolPath);
        ValidateLayoutPlacement(placement, mode, protocolPath);
        ValidateLayoutSize(size, protocolPath);
    }

    private static void ValidateLayoutContainer(JsonObject container, string mode, string protocolPath)
    {
        var containerPath = $"{protocolPath}.container";
        switch (mode)
        {
            case "flex":
            {
                var flex = RequireLayoutObject(container, "flex", containerPath);
                RequireLayoutEnum(flex, "direction", ["row", "row-reverse", "column", "column-reverse"], $"{containerPath}.flex.direction");
                RequireLayoutEnum(flex, "wrap", ["nowrap", "wrap", "wrap-reverse"], $"{containerPath}.flex.wrap");
                RequireLayoutNumber(flex, "gap", $"{containerPath}.flex.gap", nonNegative: true);
                RequireLayoutEnum(flex, "alignItems", ["start", "center", "end", "stretch", "baseline"], $"{containerPath}.flex.alignItems");
                RequireLayoutEnum(flex, "justifyContent", ["start", "center", "end", "space-between", "space-around", "space-evenly"], $"{containerPath}.flex.justifyContent");
                break;
            }
            case "grid":
            {
                var grid = RequireLayoutObject(container, "grid", containerPath);
                RequireLayoutStringArray(grid, "columns", $"{containerPath}.grid.columns");
                RequireLayoutStringArray(grid, "rows", $"{containerPath}.grid.rows");
                RequireLayoutNumber(grid, "columnGap", $"{containerPath}.grid.columnGap", nonNegative: true);
                RequireLayoutNumber(grid, "rowGap", $"{containerPath}.grid.rowGap", nonNegative: true);
                RequireLayoutEnum(grid, "autoFlow", ["row", "column", "dense", "row-dense", "column-dense"], $"{containerPath}.grid.autoFlow");
                break;
            }
            case "constraints":
            {
                var constraints = RequireLayoutObject(container, "constraints", containerPath);
                var coordinateSpace = ReadLayoutString(constraints, "coordinateSpace", $"{containerPath}.constraints.coordinateSpace", "LAYOUT_CONSTRAINT_STRATEGY_REQUIRED");
                if (!string.Equals(coordinateSpace, "parent-padding-box", StringComparison.Ordinal))
                {
                    throw LayoutInvalid($"{containerPath}.constraints.coordinateSpace", "LAYOUT_CONSTRAINT_STRATEGY_REQUIRED", "must be parent-padding-box");
                }

                break;
            }
        }
    }

    private static void ValidateLayoutPlacement(JsonObject placement, string mode, string protocolPath)
    {
        var placementPath = $"{protocolPath}.placement";
        var kind = ReadLayoutString(placement, "kind", $"{placementPath}.kind", "LAYOUT_PLACEMENT_KIND_INVALID");
        var expectedKind = mode switch
        {
            "free" => "absolute",
            "flex" => "flex-item",
            "grid" => "grid-item",
            _ => "constrained"
        };

        if (!string.Equals(kind, expectedKind, StringComparison.Ordinal))
        {
            throw LayoutInvalid($"{placementPath}.kind", "LAYOUT_PLACEMENT_KIND_INVALID", $"must be {expectedKind} for {mode}");
        }

        var payloadCount = LayoutPlacementPayloadNames.Count(name => placement[name] is not null);
        if (payloadCount != 1)
        {
            throw LayoutInvalid(placementPath, "LAYOUT_PLACEMENT_PAYLOAD_CONFLICT", "exactly one placement payload is required");
        }

        var payloadName = kind switch
        {
            "absolute" => "absolute",
            "flex-item" => "flexItem",
            "grid-item" => "gridItem",
            _ => "constrained"
        };
        var payload = RequireLayoutObject(placement, payloadName, placementPath);
        switch (payloadName)
        {
            case "absolute":
                RequireLayoutNumber(payload, "x", $"{placementPath}.absolute.x");
                RequireLayoutNumber(payload, "y", $"{placementPath}.absolute.y");
                OptionalLayoutNumber(payload, "zIndex", $"{placementPath}.absolute.zIndex");
                break;
            case "flexItem":
                RequireLayoutNumber(payload, "order", $"{placementPath}.flexItem.order");
                RequireLayoutNumber(payload, "grow", $"{placementPath}.flexItem.grow", nonNegative: true);
                RequireLayoutNumber(payload, "shrink", $"{placementPath}.flexItem.shrink", nonNegative: true);
                RequireLayoutDimension(payload, "basis", $"{placementPath}.flexItem.basis");
                OptionalLayoutEnum(payload, "alignSelf", ["auto", "start", "center", "end", "stretch", "baseline"], $"{placementPath}.flexItem.alignSelf");
                break;
            case "gridItem":
                RequireLayoutGridLine(payload, "rowStart", $"{placementPath}.gridItem.rowStart");
                RequireLayoutPositiveInteger(payload, "rowSpan", $"{placementPath}.gridItem.rowSpan");
                RequireLayoutGridLine(payload, "columnStart", $"{placementPath}.gridItem.columnStart");
                RequireLayoutPositiveInteger(payload, "columnSpan", $"{placementPath}.gridItem.columnSpan");
                OptionalLayoutEnum(payload, "alignSelf", ["auto", "start", "center", "end", "stretch"], $"{placementPath}.gridItem.alignSelf");
                OptionalLayoutEnum(payload, "justifySelf", ["auto", "start", "center", "end", "stretch"], $"{placementPath}.gridItem.justifySelf");
                break;
            case "constrained":
                foreach (var property in new[] { "left", "right", "top", "bottom", "centerX", "centerY" })
                {
                    OptionalLayoutNumber(payload, property, $"{placementPath}.constrained.{property}");
                }

                OptionalLayoutBoolean(payload, "stretchX", $"{placementPath}.constrained.stretchX");
                OptionalLayoutBoolean(payload, "stretchY", $"{placementPath}.constrained.stretchY");
                break;
        }
    }

    private static void ValidateLayoutSize(JsonObject size, string protocolPath)
    {
        var sizePath = $"{protocolPath}.size";
        RequireLayoutDimension(size, "width", $"{sizePath}.width");
        RequireLayoutDimension(size, "height", $"{sizePath}.height");
        foreach (var property in new[] { "minWidth", "maxWidth", "minHeight", "maxHeight" })
        {
            OptionalLayoutDimension(size, property, $"{sizePath}.{property}");
        }

        if (size.ContainsKey("aspectRatio"))
        {
            var aspectRatio = RequireLayoutNumber(size, "aspectRatio", $"{sizePath}.aspectRatio");
            if (aspectRatio <= 0)
            {
                throw LayoutInvalid($"{sizePath}.aspectRatio", "LAYOUT_ASPECT_RATIO_INVALID", "must be greater than zero");
            }
        }

        if (TryReadLayoutNumber(size["minWidth"], out var minWidth) && TryReadLayoutNumber(size["maxWidth"], out var maxWidth) && minWidth > maxWidth)
        {
            throw LayoutInvalid($"{sizePath}.width", "LAYOUT_SIZE_RANGE_INVALID", "minWidth cannot exceed maxWidth");
        }

        if (TryReadLayoutNumber(size["minHeight"], out var minHeight) && TryReadLayoutNumber(size["maxHeight"], out var maxHeight) && minHeight > maxHeight)
        {
            throw LayoutInvalid($"{sizePath}.height", "LAYOUT_SIZE_RANGE_INVALID", "minHeight cannot exceed maxHeight");
        }
    }

    private static JsonObject RequireLayoutObject(JsonObject parent, string property, string parentPath) =>
        parent[property] is JsonObject value
            ? value
            : throw LayoutInvalid($"{parentPath}.{property}", "LAYOUT_PROTOCOL_INVALID", "must be a JSON object");

    private static string ReadLayoutString(JsonObject parent, string property, string path, string code) =>
        parent[property] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw LayoutInvalid(path, code, "must be a non-empty string");

    private static void RequireLayoutEnum(JsonObject parent, string property, string[] allowed, string path) =>
        RequireLayoutEnumValue(parent, property, allowed, path, required: true);

    private static void OptionalLayoutEnum(JsonObject parent, string property, string[] allowed, string path)
    {
        if (parent.ContainsKey(property))
        {
            RequireLayoutEnumValue(parent, property, allowed, path, required: true);
        }
    }

    private static void RequireLayoutEnumValue(JsonObject parent, string property, string[] allowed, string path, bool required)
    {
        if (parent[property] is JsonValue value && value.TryGetValue<string>(out var text) && allowed.Contains(text, StringComparer.Ordinal))
        {
            return;
        }

        throw LayoutInvalid(path, "LAYOUT_PROTOCOL_INVALID", required ? $"must be one of {string.Join(", ", allowed)}" : "is invalid");
    }

    private static void RequireLayoutStringArray(JsonObject parent, string property, string path)
    {
        if (parent[property] is JsonArray array && array.Count > 0 && array.All(item => item is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)))
        {
            return;
        }

        throw LayoutInvalid(path, "LAYOUT_CONTAINER_PAYLOAD_INVALID", "must be a non-empty string array");
    }

    private static double RequireLayoutNumber(JsonObject parent, string property, string path, bool nonNegative = false)
    {
        if (!TryReadLayoutNumber(parent[property], out var number) || (nonNegative && number < 0))
        {
            throw LayoutInvalid(path, "LAYOUT_PROTOCOL_INVALID", nonNegative ? "must be a finite non-negative number" : "must be a finite number");
        }

        return number;
    }

    private static void OptionalLayoutNumber(JsonObject parent, string property, string path)
    {
        if (parent.ContainsKey(property))
        {
            RequireLayoutNumber(parent, property, path);
        }
    }

    private static void OptionalLayoutBoolean(JsonObject parent, string property, string path)
    {
        if (parent.ContainsKey(property) && (parent[property] is not JsonValue value || !value.TryGetValue<bool>(out _)))
        {
            throw LayoutInvalid(path, "LAYOUT_PROTOCOL_INVALID", "must be a boolean");
        }
    }

    private static void RequireLayoutPositiveInteger(JsonObject parent, string property, string path)
    {
        var number = RequireLayoutNumber(parent, property, path, nonNegative: true);
        if (number < 1 || number != Math.Truncate(number))
        {
            throw LayoutInvalid(path, "LAYOUT_GRID_SPAN_INVALID", "must be a positive integer");
        }
    }

    private static void RequireLayoutGridLine(JsonObject parent, string property, string path)
    {
        if (parent[property] is JsonValue value && value.TryGetValue<string>(out var text) && text == "auto")
        {
            return;
        }

        RequireLayoutPositiveInteger(parent, property, path);
    }

    private static void RequireLayoutDimension(JsonObject parent, string property, string path)
    {
        if (!IsLayoutDimension(parent[property]))
        {
            throw LayoutInvalid(path, "LAYOUT_DIMENSION_INVALID", "must be a non-negative number, percentage, pixel value, or supported intrinsic keyword");
        }
    }

    private static void OptionalLayoutDimension(JsonObject parent, string property, string path)
    {
        if (parent.ContainsKey(property))
        {
            RequireLayoutDimension(parent, property, path);
        }
    }

    private static bool IsLayoutDimension(JsonNode? value)
    {
        if (TryReadLayoutNumber(value, out var number))
        {
            return number >= 0;
        }

        if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var text))
        {
            return false;
        }

        if (text is "auto" or "min-content" or "max-content" or "fit-content")
        {
            return true;
        }

        if (text.EndsWith('%') || text.EndsWith("px", StringComparison.Ordinal))
        {
            var numericPart = text.EndsWith('%') ? text[..^1] : text[..^2];
            return decimal.TryParse(numericPart, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numeric) && numeric >= 0;
        }

        return false;
    }

    private static bool TryReadLayoutNumber(JsonNode? value, out double number)
    {
        if (value is JsonValue jsonValue && jsonValue.TryGetValue<double>(out number) && double.IsFinite(number))
        {
            return true;
        }

        number = default;
        return false;
    }

    private static ValidationException LayoutInvalid(string path, string code, string detail) =>
        Invalid($"[{code}] {path}: {detail}");

    private static void VisitElement(string elementId, JsonObject elements, HashSet<string> visited, HashSet<string> active, int depth)
    {
        if (depth > MaximumDepth)
        {
            throw TooLarge($"element tree exceeds depth {MaximumDepth}");
        }

        if (!active.Add(elementId))
        {
            throw Invalid($"element tree contains a cycle at {elementId}");
        }

        if (!visited.Add(elementId))
        {
            active.Remove(elementId);
            return;
        }

        foreach (var childId in ReadChildren((JsonObject)elements[elementId]!, elementId))
        {
            VisitElement(childId, elements, visited, active, depth + 1);
        }

        active.Remove(elementId);
    }

    private static IReadOnlyList<string> ReadChildren(JsonObject element, string elementId)
    {
        if (element["children"] is not JsonArray children)
        {
            throw Invalid($"node {elementId} requires children");
        }

        return children.Select((item, index) => item is JsonValue value &&
                value.TryGetValue<string>(out var childId) && !string.IsNullOrWhiteSpace(childId)
                ? childId
                : throw Invalid($"node {elementId} children[{index}] is invalid"))
            .ToArray();
    }

    private static void ValidateActions(JsonObject document)
    {
        if (document["actions"] is not JsonArray actions)
        {
            return;
        }

        if (actions.Count > MaximumActions)
        {
            throw TooLarge($"actions cannot exceed {MaximumActions}");
        }

        var actionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (index, actionNode) in actions.Select((node, index) => (index, node)))
        {
            if (actionNode is not JsonObject action)
            {
                throw Invalid($"actions[{index}] must be an object");
            }

            var actionId = ReadRequiredString(action, "id", $"actions[{index}]");
            if (!actionIds.Add(actionId))
            {
                throw Invalid($"duplicate action id: {actionId}");
            }

            if (action["steps"] is not JsonArray steps)
            {
                continue;
            }

            if (steps.Count > MaximumStepsPerAction)
            {
                throw TooLarge($"action {actionId} exceeds {MaximumStepsPerAction} steps");
            }

            var stepIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (stepIndex, stepNode) in steps.Select((node, stepIndex) => (stepIndex, node)))
            {
                if (stepNode is not JsonObject step)
                {
                    throw Invalid($"action {actionId} step {stepIndex} must be an object");
                }

                var stepId = ReadRequiredString(step, "id", $"actions[{index}].steps[{stepIndex}]");
                if (!stepIds.Add(stepId))
                {
                    throw Invalid($"duplicate step id: {stepId}");
                }
            }
        }
    }

    private static void ValidateJsonGraph(JsonNode node)
    {
        var visited = new HashSet<JsonNode>(ReferenceEqualityComparer.Instance);
        VisitJsonNode(node, visited, 1);
    }

    private static void VisitJsonNode(JsonNode? node, HashSet<JsonNode> visited, int depth)
    {
        if (node is null)
        {
            return;
        }

        if (depth > MaximumDepth)
        {
            throw TooLarge($"JSON exceeds depth {MaximumDepth}");
        }

        if (!visited.Add(node))
        {
            throw Invalid("Designer Document contains a cyclic JSON reference");
        }

        if (node is JsonObject obj)
        {
            foreach (var (name, child) in obj)
            {
                RejectPropertyName(name);
                VisitJsonNode(child, visited, depth + 1);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                VisitJsonNode(child, visited, depth + 1);
            }
        }
    }

    private static void ValidateValuePaths(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var (name, child) in obj)
            {
                if (ValuePathProperties.Contains(name))
                {
                    var segments = child switch
                    {
                        JsonValue value when value.TryGetValue<string>(out var text) => text.Split('.', StringSplitOptions.RemoveEmptyEntries),
                        JsonArray array => array.Select(item => item?.GetValue<string>() ?? string.Empty).ToArray(),
                        _ => Array.Empty<string>()
                    };

                    if (segments.Length > MaximumValuePathSegments)
                    {
                        throw TooLarge($"{name} exceeds {MaximumValuePathSegments} path segments");
                    }
                }

                ValidateValuePaths(child!);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                ValidateValuePaths(child!);
            }
        }
    }

    private static void RejectPrototypePollution(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var (name, child) in obj)
            {
                RejectPropertyName(name);
                RejectPrototypePollution(child!);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                RejectPrototypePollution(child!);
            }
        }
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw Invalid($"duplicate property: {path}.{property.Name}");
                }

                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
            {
                RejectDuplicateProperties(child, $"{path}[{index++}]");
            }
        }
    }

    private static void RejectPropertyName(string name)
    {
        if (name is "__proto__" or "prototype" or "constructor")
        {
            throw Invalid($"forbidden property name: {name}");
        }
    }

    private static string ReadRequiredString(JsonObject value, string propertyName, string path)
    {
        var text = ReadNullableString(value, propertyName);
        return string.IsNullOrWhiteSpace(text) ? throw Invalid($"{path}.{propertyName} is required") : text;
    }

    private static string? ReadNullableString(JsonObject value, string propertyName) =>
        value[propertyName] is JsonValue jsonValue &&
        jsonValue.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    private static ValidationException Invalid(string message) => new(message, ErrorCodes.DesignerSchemaInvalid);

    private static ValidationException TooLarge(string message) => new(message, ErrorCodes.SchemaOrPayloadTooLarge);
}
