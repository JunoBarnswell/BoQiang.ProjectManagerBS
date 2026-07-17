using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentSchemaCompiler(
    ApplicationDevelopmentSchemaValidator? schemaValidator = null)
{
    private readonly ApplicationDevelopmentSchemaValidator schemaValidator =
        schemaValidator ?? new ApplicationDevelopmentSchemaValidator();

    public string CompileSchema(
        string pageCode,
        string pageName,
        string pageType,
        IReadOnlyList<ApplicationDevelopmentPageParameterDto> pageParameters,
        string documentJson,
        string permissionConfigJson,
        IReadOnlyList<RuntimeDataFieldDefinition>? runtimeFields = null,
        string? keyField = null,
        string? modelCode = null,
        bool readOnly = false,
        bool createRuntimeCrudActions = false,
        bool createImportExport = false)
    {
        var document = schemaValidator.ValidateDraft(documentJson);

        var permissionConfig = ApplicationDataCenterJson.Deserialize<ApplicationDevelopmentPermissionConfigDto>(permissionConfigJson)
            ?? new ApplicationDevelopmentPermissionConfigDto();
        var runtimeContext = document["runtimeContext"] as JsonObject ?? new JsonObject();
        runtimeContext["pageCode"] = pageCode;
        runtimeContext["pageName"] = pageName;
        runtimeContext["pageType"] = NormalizePageType(pageType);
        runtimeContext["pageParameters"] = JsonNode.Parse(ApplicationDataCenterJson.Serialize(pageParameters));
        runtimeContext["permissionPrefix"] = PermissionCodes.BuildAppRuntimePagePermission(pageCode, "view")[..^":view".Length];
        runtimeContext["menuCode"] = ResolveRuntimeMenuCode(pageCode, permissionConfig);
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            runtimeContext["modelCode"] = modelCode;
        }
        document["runtimeContext"] = runtimeContext;
        document["pageType"] = NormalizePageType(pageType);
        document["pageParameters"] = JsonNode.Parse(ApplicationDataCenterJson.Serialize(pageParameters));
        ValidateDesignerDocument(document);
        document = ApplicationDevelopmentSchemaValidator.RemoveRuntimeEditorState(document);
        schemaValidator.ValidateRuntimeArtifact(document.ToJsonString(ApplicationDataCenterJson.Options));
        var columns = BuildRuntimeGridColumns(document, runtimeFields);
        var formFields = BuildRuntimeFormFields(runtimeFields, columns);
        var resolvedKeyField = !string.IsNullOrWhiteSpace(keyField)
            ? keyField
            : ResolveKeyField(runtimeFields, columns);

        var canonicalDocument = JsonNode.Parse(ApplicationDesignerCanonicalJson.NormalizeObject(document.ToJsonString(ApplicationDataCenterJson.Options)))!.AsObject();
        var documentId = canonicalDocument["documentId"]?.GetValue<string>() ?? pageCode;
        canonicalDocument["documentId"] = documentId;
        var revision = canonicalDocument["revision"]?.GetValue<int>() is int documentRevision && documentRevision > 0 ? documentRevision : 1;
        canonicalDocument["revision"] = revision;
        var artifactHash = ApplicationDesignerCanonicalJson.ComputeRuntimeArtifactHash(canonicalDocument);
        var compilerVersion = RuntimeCapabilityContract.CompilerRevision;
        var manifestTypes = BuildManifestTypes(canonicalDocument);
        var manifest = BuildManifest(canonicalDocument, manifestTypes);
        var manifestSignatureJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
        {
            ["types"] = manifestTypes.DeepClone(),
            ["declarations"] = manifest.DeepClone()
        }.ToJsonString());
        var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestSignatureJson);
        var signature = ApplicationDesignerCanonicalJson.ComputeSignature(
            documentId,
            artifactHash,
            manifestHash,
            compilerVersion,
            revision.ToString(global::System.Globalization.CultureInfo.InvariantCulture));

        var schema = new JsonObject
        {
            ["artifactHash"] = artifactHash,
            ["signature"] = signature,
            ["compilerVersion"] = compilerVersion,
            ["id"] = pageCode,
            ["manifestTypes"] = manifestTypes,
            ["manifest"] = manifest,
            ["migrationRevision"] = "latest",
            ["revision"] = revision,
            ["title"] = pageName,
            ["description"] = "由完整低代码元素设计器生成的页面。",
            ["pageType"] = NormalizePageType(pageType),
            ["renderer"] = "designerDocument",
            ["document"] = canonicalDocument,
            ["grid"] = new JsonObject
            {
                ["keyField"] = resolvedKeyField,
                ["columns"] = columns,
                ["masterDetail"] = new JsonObject
                {
                    ["enabled"] = false,
                    ["relations"] = new JsonArray(),
                    ["detailSections"] = new JsonArray()
                }
            },
            ["form"] = new JsonObject { ["fields"] = formFields },
            ["runtimeContext"] = runtimeContext.DeepClone(),
            ["relations"] = new JsonArray(),
            ["detail"] = new JsonObject(),
            ["sections"] = string.IsNullOrWhiteSpace(modelCode)
                ? new JsonArray()
                : new JsonArray(new JsonObject
                {
                    ["id"] = $"{pageCode}-runtime-crud",
                    ["componentKey"] = "runtimeCrudPage",
                    ["variant"] = "plain",
                    ["props"] = new JsonObject
                    {
                        ["modelCode"] = modelCode,
                        ["pageCode"] = pageCode,
                        ["pageName"] = pageName,
                        ["permissionCode"] = PermissionCodes.BuildAppRuntimePagePermission(pageCode, "view"),
                        ["addPermissionCode"] = readOnly ? null : PermissionCodes.BuildAppRuntimePagePermission(pageCode, "add"),
                        ["editPermissionCode"] = readOnly ? null : PermissionCodes.BuildAppRuntimePagePermission(pageCode, "edit"),
                        ["deletePermissionCode"] = readOnly ? null : PermissionCodes.BuildAppRuntimePagePermission(pageCode, "delete"),
                        ["importPermissionCode"] = readOnly ? null : PermissionCodes.BuildAppRuntimePagePermission(pageCode, "import"),
                        ["exportPermissionCode"] = PermissionCodes.BuildAppRuntimePagePermission(pageCode, "export"),
                        ["createRuntimeCrudActions"] = createRuntimeCrudActions && !readOnly,
                        ["createImportExport"] = createImportExport && !readOnly,
                        ["readOnly"] = readOnly,
                        ["keyField"] = resolvedKeyField
                    }
                })
        };
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            schema["modelCode"] = modelCode;
        }

        RuntimeArtifactContractValidator.Validate(schema);

        var schemaJson = schema.ToJsonString(ApplicationDataCenterJson.Options);
        if (Encoding.UTF8.GetByteCount(schemaJson) > ApplicationDevelopmentSchemaValidator.RuntimeMaximumBytes)
        {
            throw new ValidationException(
                $"运行时产物超过 {ApplicationDevelopmentSchemaValidator.RuntimeMaximumBytes / 1024} KiB 限制",
                ErrorCodes.SchemaOrPayloadTooLarge);
        }

        return schemaJson;
    }

    private static JsonArray BuildManifestTypes(JsonObject document)
    {
        var manifestTypes = new JsonArray();
        foreach (var type in document["elements"]?.AsObject()
                     .Select(item => item.Value?["type"]?.GetValue<string>())
                     .Where(type => !string.IsNullOrWhiteSpace(type))
                     .Distinct(StringComparer.Ordinal) ?? [])
        {
            manifestTypes.Add(type);
        }

        if (manifestTypes.Count == 0)
        {
            throw new ValidationException("Runtime Artifact manifestTypes cannot be empty", ErrorCodes.DesignerSchemaInvalid);
        }

        return manifestTypes;
    }

    private static JsonArray BuildManifest(JsonObject document, JsonArray manifestTypes)
    {
        var declarations = new JsonArray();
        foreach (var typeNode in manifestTypes)
        {
            var type = typeNode?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(type))
            {
                throw new ValidationException("Component manifest type is required", ErrorCodes.DesignerSchemaInvalid);
            }

            if (!RuntimeCapabilityContract.ComponentTypes.Contains(type))
            {
                throw new ValidationException($"Runtime artifact component type is not registered: {type}", ErrorCodes.DesignerSchemaInvalid);
            }

            declarations.Add(RuntimeCapabilityContract.BuildArtifactManifest(type));
        }

        return declarations;
    }

    private static string NormalizePageType(string? value)
    {
        var normalized = value?.Trim();
        return ApplicationDevelopmentPageTypes.IsValid(normalized)
            ? normalized!
            : ApplicationDevelopmentPageTypes.Standard;
    }

    private static void ValidateDesignerDocument(JsonObject document)
    {
        if (document["elements"] is not JsonObject elements)
        {
            return;
        }

        foreach (var element in elements)
        {
            if (element.Value is not JsonObject elementObject ||
                elementObject["type"]?.GetValue<string>() is not "report.dataTable" ||
                elementObject["props"] is not JsonObject props)
            {
                continue;
            }

            if (props["rowEditing"] is JsonObject rowEditing &&
                ReadJsonString(rowEditing, "mode") is "dialog" or "drawer")
            {
                throw new ValidationException("旧 rowEditing 弹框/抽屉业务编辑壳已下线，请改为操作列页面调用", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (props["columns"] is not JsonArray columns)
            {
                continue;
            }

            foreach (var column in columns.OfType<JsonObject>())
            {
                var fieldCode = ReadJsonString(column, "fieldCode") ?? ReadJsonString(column, "key");
                if (string.IsNullOrWhiteSpace(fieldCode))
                {
                    throw new ValidationException("表格列必须有稳定 fieldCode", ErrorCodes.ApplicationDataCenterInvalidConfig);
                }
            }
        }
    }

    private static JsonArray BuildRuntimeGridColumns(JsonObject document, IReadOnlyList<RuntimeDataFieldDefinition>? runtimeFields)
    {
        if (runtimeFields is { Count: > 0 })
        {
            return new JsonArray(runtimeFields
                .Where(field => field.Visible)
                .OrderBy(field => field.Order)
                .Select(field => (JsonNode)GridColumn(
                    field.FieldCode,
                    string.IsNullOrWhiteSpace(field.FieldName) ? field.FieldCode : field.FieldName,
                    string.IsNullOrWhiteSpace(field.Binding) ? field.FieldCode : field.Binding,
                    field.Width ?? ResolveWidth(field.DataType),
                    field.Fixed,
                    true,
                    field.Order,
                    field.Renderer,
                    field.Queryable ? field.FieldCode : null,
                    field.Sortable ? field.FieldCode : null))
                .ToArray());
        }

        return new JsonArray(ReadDesignerTableColumns(document)
            .Where(field => field.Visible)
            .Select((field, index) => (JsonNode)GridColumn(
                field.FieldCode,
                field.FieldName,
                field.FieldCode,
                string.IsNullOrWhiteSpace(field.Width) ? ResolveWidth(field.DataType) : field.Width!,
                field.Fixed,
                field.Visible,
                field.Order ?? index + 1,
                field.Format,
                field.FieldCode,
                field.FieldCode))
            .ToArray());
    }

    private static JsonArray BuildRuntimeFormFields(IReadOnlyList<RuntimeDataFieldDefinition>? runtimeFields, JsonArray columns)
    {
        if (runtimeFields is { Count: > 0 })
        {
            return new JsonArray(runtimeFields
                .OrderBy(field => field.Order)
                .Select(field => (JsonNode)new JsonObject
                {
                    ["fieldCode"] = field.FieldCode,
                    ["fieldName"] = string.IsNullOrWhiteSpace(field.FieldName) ? field.FieldCode : field.FieldName,
                    ["dataType"] = field.DataType,
                    ["binding"] = string.IsNullOrWhiteSpace(field.Binding) ? field.FieldCode : field.Binding,
                    ["writable"] = field.Writable,
                    ["visible"] = field.Visible,
                    ["order"] = field.Order
                })
                .ToArray());
        }

        return new JsonArray(columns
            .Select((column, index) => (JsonNode)new JsonObject
            {
                ["fieldCode"] = column?["key"]?.GetValue<string>() ?? $"field{index + 1}",
                ["fieldName"] = column?["title"]?.GetValue<string>() ?? column?["key"]?.GetValue<string>() ?? $"字段{index + 1}",
                ["dataType"] = "text",
                ["binding"] = column?["binding"]?.GetValue<string>() ?? column?["key"]?.GetValue<string>() ?? $"field{index + 1}",
                ["writable"] = true,
                ["visible"] = true,
                ["order"] = index + 1
            })
            .ToArray());
    }

    private static JsonObject GridColumn(
        string key,
        string title,
        string binding,
        string width,
        string? fixedValue,
        bool isVisible,
        int order,
        string? renderer,
        string? queryField,
        string? sortField) =>
        new()
        {
            ["key"] = key,
            ["title"] = title,
            ["binding"] = binding,
            ["width"] = width,
            ["fixed"] = fixedValue,
            ["isVisible"] = isVisible,
            ["order"] = order,
            ["renderer"] = renderer,
            ["queryField"] = queryField,
            ["sortField"] = sortField
        };

    private static string ResolveKeyField(IReadOnlyList<RuntimeDataFieldDefinition>? runtimeFields, JsonArray columns)
    {
        var idField = runtimeFields?.FirstOrDefault(field => field.FieldCode.Equals("id", StringComparison.OrdinalIgnoreCase));
        if (idField is not null)
        {
            return idField.FieldCode;
        }

        var firstRuntimeField = runtimeFields?.FirstOrDefault();
        if (firstRuntimeField is not null)
        {
            return firstRuntimeField.FieldCode;
        }

        return columns.Count > 0 && columns[0]?["key"] is JsonValue keyValue
            ? keyValue.GetValue<string>()
            : "id";
    }

    private static IReadOnlyList<DesignerColumn> ReadDesignerTableColumns(JsonObject document)
    {
        if (document["elements"] is not JsonObject elements)
        {
            return [];
        }

        foreach (var element in elements)
        {
            if (element.Value is not JsonObject elementObject ||
                elementObject["type"]?.GetValue<string>() is not "report.dataTable" ||
                elementObject["props"] is not JsonObject props ||
                props["columns"] is not JsonArray columns)
            {
                continue;
            }

            var result = columns
                .OfType<JsonObject>()
                .Select((column, index) => new DesignerColumn(
                    column["fieldCode"]?.GetValue<string>() ?? column["key"]?.GetValue<string>() ?? string.Empty,
                    column["fieldName"]?.GetValue<string>() ?? column["title"]?.GetValue<string>() ?? column["fieldCode"]?.GetValue<string>() ?? $"字段{index + 1}",
                    column["dataType"]?.GetValue<string>() ?? "text",
                    ReadJsonInt(column, "order"),
                    ReadJsonString(column, "width"),
                    ReadJsonString(column, "fixed"),
                    ReadJsonBool(column, "visible") ?? true,
                    ReadJsonString(column, "format")))
                .Where(column => !string.IsNullOrWhiteSpace(column.FieldCode))
                .ToArray();
            if (result.Length > 0)
            {
                return result;
            }
        }

        return [];
    }

    private static string ResolveWidth(string? dataType)
    {
        var normalized = dataType ?? string.Empty;
        if (normalized.Contains("date", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("time", StringComparison.OrdinalIgnoreCase))
        {
            return "160px";
        }

        return normalized.Contains("int", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("decimal", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("number", StringComparison.OrdinalIgnoreCase)
            ? "120px"
            : "180px";
    }

    private sealed record DesignerColumn(
        string FieldCode,
        string FieldName,
        string DataType,
        int? Order,
        string? Width,
        string? Fixed,
        bool Visible,
        string? Format);

    private static int? ReadJsonInt(JsonObject value, string key)
    {
        if (value[key] is not JsonValue jsonValue)
        {
            return null;
        }

        return jsonValue.TryGetValue<int>(out var number) ? number : null;
    }

    private static bool? ReadJsonBool(JsonObject value, string key)
    {
        if (value[key] is not JsonValue jsonValue)
        {
            return null;
        }

        return jsonValue.TryGetValue<bool>(out var flag) ? flag : null;
    }

    private static string? ReadJsonString(JsonObject value, string key)
    {
        if (value[key] is not JsonValue jsonValue)
        {
            return null;
        }

        return jsonValue.TryGetValue<string>(out var text) ? text : null;
    }

    private static string ResolveRuntimeMenuCode(string pageCode, ApplicationDevelopmentPermissionConfigDto permissionConfig)
    {
        var menuCode = permissionConfig.MenuCode?.Trim();
        return string.IsNullOrWhiteSpace(menuCode) ? $"{pageCode}-menu" : menuCode;
    }

}
