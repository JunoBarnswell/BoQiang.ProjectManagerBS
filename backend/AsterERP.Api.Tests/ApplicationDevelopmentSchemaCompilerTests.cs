using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Application.ApplicationDevelopmentCenter.Runtime;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Contracts.ApplicationDesigner;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentSchemaCompilerTests
{
    [Fact]
    public void CompileSchema_rejects_unknown_component_from_the_shared_runtime_capability_contract()
    {
        var draft = JsonNode.Parse(BuildDesignerDocumentDraftJson("unknown_component", "Unknown component"))!.AsObject();
        draft["elements"]!["unknown_component_root"]!["type"] = "unknown.component";

        Assert.Throws<ValidationException>(() => new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "unknown_component", "Unknown component", ApplicationDevelopmentPageTypes.Standard, [], draft.ToJsonString(), "{}"));
    }

    [Fact]
    public void CompileSchema_emits_independent_canonical_component_capabilities_and_preserves_data_table_editing()
    {
        var schema = JsonNode.Parse(new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "capability_page", "Capability page", ApplicationDevelopmentPageTypes.Standard, [], BuildDesignerDocumentDraftJson("capability_page", "Capability page"), "{}"))!.AsObject();

        foreach (var declaration in schema["manifest"]!.AsArray().OfType<JsonObject>())
        {
            Assert.NotEmpty(declaration["binding"]!["acceptedTypes"]!.AsArray());
            Assert.NotEmpty(declaration["events"]!.AsArray());
            Assert.NotEmpty(declaration["responsive"]!["supportedLayouts"]!.AsArray());
            Assert.True(declaration["defaults"]!["props"]!.AsObject().Count > 0);
            Assert.NotNull(declaration["security"]);
        }

        var table = schema["manifest"]!.AsArray().Single(item => item!["type"]!.GetValue<string>() == "report.dataTable");
        Assert.Equal(["blur", "enter", "escape"], table!["editing"]!["commitTriggers"]!.AsArray().Select(item => item!.GetValue<string>()));
    }

    [Fact]
    public void RuntimeArtifactValidator_rejects_manifest_capability_drift_before_publish()
    {
        var artifact = JsonNode.Parse(new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "drift_page", "Drift page", ApplicationDevelopmentPageTypes.Standard, [], BuildDesignerDocumentDraftJson("drift_page", "Drift page"), "{}"))!.AsObject();
        artifact["manifest"]![0]!["defaults"]!["props"]!["drifted"] = true;

        Assert.Throws<ValidationException>(() => RuntimeArtifactContractValidator.Validate(artifact));
    }

    [Fact]
    public void CompileSchema_rejects_unknown_action_from_the_shared_runtime_capability_contract()
    {
        var draft = JsonNode.Parse(BuildDesignerDocumentDraftJson("unknown_action", "Unknown action"))!.AsObject();
        draft["actions"] = new JsonArray(new JsonObject
        {
            ["id"] = "action-1",
            ["steps"] = new JsonArray(new JsonObject { ["id"] = "step-1", ["type"] = "unknown.action" })
        });

        Assert.Throws<ValidationException>(() => new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "unknown_action", "Unknown action", ApplicationDevelopmentPageTypes.Standard, [], draft.ToJsonString(), "{}"));
    }

    [Fact]
    public void CompileSchema_preserves_formal_open_modal_action()
    {
        var draft = JsonNode.Parse(BuildDesignerDocumentDraftJson("formal_modal", "Formal modal"))!.AsObject();
        draft["modals"] = new JsonArray(new JsonObject { ["id"] = "edit-modal" });
        draft["actions"] = new JsonArray(new JsonObject
        {
            ["id"] = "action-1",
            ["steps"] = new JsonArray(new JsonObject
            {
                ["id"] = "step-1",
                ["type"] = "openModal",
                ["config"] = new JsonObject { ["modalId"] = "edit-modal" }
            })
        });

        var schema = JsonNode.Parse(new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "formal_modal", "Formal modal", ApplicationDevelopmentPageTypes.Standard, [], draft.ToJsonString(), "{}"))!.AsObject();

        Assert.Equal("openModal", schema["document"]!["actions"]![0]!["steps"]![0]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void CompileSchema_rejects_unknown_converter_from_the_shared_runtime_capability_contract()
    {
        var draft = JsonNode.Parse(BuildDesignerDocumentDraftJson("unknown_converter", "Unknown converter"))!.AsObject();
        draft["elements"]!["unknown_converter_root"]!["props"]!["value"] = new JsonObject
        {
            ["resourceId"] = "runtime.value",
            ["conversionPipeline"] = new JsonArray(new JsonObject
            {
                ["from"] = "number",
                ["name"] = "unknownConverter",
                ["to"] = "string"
            })
        };

        Assert.Throws<ValidationException>(() => new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "unknown_converter", "Unknown converter", ApplicationDevelopmentPageTypes.Standard, [], draft.ToJsonString(), "{}"));
    }

    [Fact]
    public void CompileSchema_preserves_variable_source_descriptors_without_treating_them_as_bindings()
    {
        var draft = JsonNode.Parse(BuildDesignerDocumentDraftJson("variable_descriptor", "Variable descriptor"))!.AsObject();
        draft["variables"] = new JsonArray(new JsonObject
        {
            ["id"] = "currentUser",
            ["name"] = "Current user",
            ["source"] = "system",
            ["path"] = null,
            ["valueType"] = "json"
        });

        var schema = JsonNode.Parse(new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "variable_descriptor", "Variable descriptor", ApplicationDevelopmentPageTypes.Standard, [], draft.ToJsonString(), "{}"))!.AsObject();

        var variable = schema["document"]!["variables"]![0]!.AsObject();
        Assert.Equal("system", variable["source"]!.GetValue<string>());
        Assert.True(variable.ContainsKey("path"));
    }

    [Fact]
    public void CompileSchema_still_rejects_retired_source_path_expression_in_document_binding_collections()
    {
        var draft = JsonNode.Parse(BuildDesignerDocumentDraftJson("retired_binding", "Retired binding"))!.AsObject();
        draft["apiBindings"] = new JsonArray(new JsonObject
        {
            ["id"] = "orders",
            ["value"] = new JsonObject { ["source"] = "variables", ["path"] = "orders" }
        });

        Assert.Throws<ValidationException>(() => new ApplicationDevelopmentSchemaCompiler().CompileSchema(
            "retired_binding", "Retired binding", ApplicationDevelopmentPageTypes.Standard, [], draft.ToJsonString(), "{}"));
    }

    [Fact]
    public void CompileSchema_ReadsDesignerDataTableColumnsFromDesignerDocument()
    {
        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var draftJson = BuildDesignerDocumentDraftJson("order_page", "订单页面");

        var schemaJson = compiler.CompileSchema(
            "order_page",
            "订单页面",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            draftJson,
            "{}");

        var schema = JsonNode.Parse(schemaJson)!.AsObject();
        var columns = schema["grid"]!["columns"]!.AsArray();

        Assert.Equal("designerDocument", schema["renderer"]!.GetValue<string>());
        Assert.Equal(RuntimeCapabilityContract.CompilerRevision, schema["compilerVersion"]!.GetValue<string>());
        Assert.Equal("latest", schema["migrationRevision"]!.GetValue<string>());
        Assert.Equal("ComponentRuntimeHost", schema["manifest"]![0]!["renderer"]!["runtime"]!.GetValue<string>());
        Assert.Equal("component", schema["manifest"]![0]!["inspector"]!["sections"]![0]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(schema["signature"]!.GetValue<string>()));
        Assert.Equal(64, schema["signature"]!.GetValue<string>().Length);
        var manifestSignatureJson = ApplicationDesignerCanonicalJson.NormalizeRuntimeObject(new JsonObject
        {
            ["types"] = schema["manifestTypes"]!.DeepClone(),
            ["declarations"] = schema["manifest"]!.DeepClone()
        }.ToJsonString());
        var manifestHash = ApplicationDesignerCanonicalJson.ComputeHash(manifestSignatureJson);
        var expectedSignature = ApplicationDesignerCanonicalJson.ComputeSignature(
            schema["document"]!["documentId"]!.GetValue<string>(),
            schema["artifactHash"]!.GetValue<string>(),
            manifestHash,
            schema["compilerVersion"]!.GetValue<string>(),
            schema["revision"]!.GetValue<int>().ToString(global::System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(manifestHash, ApplicationDesignerCanonicalJson.ComputeHash(manifestSignatureJson));
        Assert.Equal(expectedSignature, schema["signature"]!.GetValue<string>());
        Assert.Equal("order_no", schema["grid"]!["keyField"]!.GetValue<string>());
        Assert.Equal(2, columns.Count);
        Assert.Equal("order_no", columns[0]!["key"]!.GetValue<string>());
        Assert.Equal("订单号", columns[0]!["title"]!.GetValue<string>());
        Assert.Equal("amount", columns[1]!["key"]!.GetValue<string>());
        Assert.Equal("120px", columns[1]!["width"]!.GetValue<string>());
    }

    [Fact]
    public void CompileSchema_PreservesDesignerDocumentRootChildren()
    {
        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var draftJson = BuildDesignerDocumentDraftJson("order_page", "订单页面");

        var schemaJson = compiler.CompileSchema(
            "order_page",
            "订单页面",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            draftJson,
            "{}");

        var schema = JsonNode.Parse(schemaJson)!.AsObject();
        var document = schema["document"]!.AsObject();
        var elements = document["elements"]!.AsObject();
        var rootId = document["pages"]!.AsArray()[0]!["rootElementId"]!.GetValue<string>();
        var rootChildren = ReadChildren(elements[rootId]!.AsObject());
        var elementTypes = elements.Select(item => item.Value!["type"]!.GetValue<string>()).ToArray();

        Assert.Equal("designerDocument", schema["renderer"]!.GetValue<string>());
        Assert.Empty(schema["sections"]!.AsArray());
        Assert.False(document.ContainsKey("tree"));
        Assert.False(document.ContainsKey("selectedElementId"));
        Assert.Equal(["order_page_toolbar", "order_page_table"], rootChildren);
        Assert.False(document["runtimeContext"]!.AsObject().ContainsKey("modelCode"));
        Assert.DoesNotContain("runtimeCrudPage", elementTypes);
        Assert.DoesNotContain("business.runtimeCrud", elementTypes);
    }

    [Fact]
    public void CompileSchema_AllowsBlankDesignerDocumentWithoutModelCode()
    {
        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var draftJson = """
        {
          "documentId": "blank_page",
          "revision": 1,
          "pages": [{ "id": "blank_page", "name": "空白页面", "rootElementId": "blank_page_root" }],
          "modals": [],
          "elements": {
            "blank_page_root": {
              "id": "blank_page_root",
              "type": "layout.page",
              "name": "空白页面",
              "children": [],
              "props": { "title": "空白页面" }
            }
          },
          "runtimeContext": { "pageCode": "blank_page", "pageName": "空白页面" }
        }
        """;

        var schemaJson = compiler.CompileSchema(
            "blank_page",
            "空白页面",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            draftJson,
            "{}");

        var schema = JsonNode.Parse(schemaJson)!.AsObject();
        var runtimeContext = schema["runtimeContext"]!.AsObject();
        var document = schema["document"]!.AsObject();

        Assert.Equal("designerDocument", schema["renderer"]!.GetValue<string>());
        Assert.False(runtimeContext.ContainsKey("modelCode"));
        Assert.False(document["runtimeContext"]!.AsObject().ContainsKey("modelCode"));
        Assert.Equal("id", schema["grid"]!["keyField"]!.GetValue<string>());
        Assert.Empty(schema["grid"]!["columns"]!.AsArray());
        Assert.Empty(schema["form"]!["fields"]!.AsArray());
    }

    [Fact]
    public void CompileSchema_UsesDefaultRuntimeMenuCodeWhenPermissionMenuCodeIsBlank()
    {
        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var draftJson = BuildDesignerDocumentDraftJson("order_page", "订单页面");

        var schemaJson = compiler.CompileSchema(
            "order_page",
            "订单页面",
            ApplicationDevelopmentPageTypes.Standard,
            [],
            draftJson,
            """{"menuCode":""}""");

        var schema = JsonNode.Parse(schemaJson)!.AsObject();
        var runtimeContext = schema["runtimeContext"]!.AsObject();
        var documentRuntimeContext = schema["document"]!["runtimeContext"]!.AsObject();

        Assert.Equal("order_page-menu", runtimeContext["menuCode"]!.GetValue<string>());
        Assert.Equal("order_page-menu", documentRuntimeContext["menuCode"]!.GetValue<string>());
    }

    [Fact]
    public void CompileSchema_WritesPageTypeAndParametersIntoRuntimeContext()
    {
        var compiler = new ApplicationDevelopmentSchemaCompiler();
        var draftJson = BuildDesignerDocumentDraftJson("edit_dialog", "编辑弹框");

        var schemaJson = compiler.CompileSchema(
            "edit_dialog",
            "编辑弹框",
            ApplicationDevelopmentPageTypes.Dialog,
            [
                new ApplicationDevelopmentPageParameterDto
                {
                    Code = "currentRow",
                    Direction = "input",
                    Name = "当前行",
                    Required = true,
                    ValueType = "json"
                }
            ],
            draftJson,
            "{}");

        var schema = JsonNode.Parse(schemaJson)!.AsObject();
        var runtimeContext = schema["runtimeContext"]!.AsObject();
        var document = schema["document"]!.AsObject();

        Assert.Equal(ApplicationDevelopmentPageTypes.Dialog, schema["pageType"]!.GetValue<string>());
        Assert.Equal(ApplicationDevelopmentPageTypes.Dialog, runtimeContext["pageType"]!.GetValue<string>());
        Assert.Equal(ApplicationDevelopmentPageTypes.Dialog, document["pageType"]!.GetValue<string>());
        Assert.Single(runtimeContext["pageParameters"]!.AsArray());
    }

    private static string BuildDesignerDocumentDraftJson(string pageCode, string pageName)
    {
        var rootId = $"{pageCode}_root";
        var toolbarId = $"{pageCode}_toolbar";
        var tableId = $"{pageCode}_table";
        var document = new JsonObject
        {
            ["documentId"] = pageCode,
            ["revision"] = 1,
            ["pages"] = new JsonArray(new JsonObject
            {
                ["id"] = pageCode,
                ["name"] = pageName,
                ["rootElementId"] = rootId
            }),
            ["modals"] = new JsonArray(),
            ["elements"] = new JsonObject
            {
                [rootId] = new JsonObject
                {
                    ["id"] = rootId,
                    ["type"] = "layout.page",
                    ["name"] = pageName,
                    ["children"] = new JsonArray(toolbarId, tableId),
                    ["props"] = new JsonObject { ["title"] = pageName }
                },
                [toolbarId] = new JsonObject
                {
                    ["id"] = toolbarId,
                    ["parentId"] = rootId,
                    ["type"] = "layout.row",
                    ["name"] = "工具条",
                    ["children"] = new JsonArray(),
                    ["props"] = new JsonObject()
                },
                [tableId] = new JsonObject
                {
                    ["id"] = tableId,
                    ["parentId"] = rootId,
                    ["type"] = "report.dataTable",
                    ["name"] = "订单表格",
                    ["children"] = new JsonArray(),
                    ["props"] = new JsonObject
                    {
                        ["columns"] = new JsonArray(
                            new JsonObject
                            {
                                ["fieldCode"] = "order_no",
                                ["fieldName"] = "订单号",
                                ["dataType"] = "text",
                                ["order"] = 1
                            },
                            new JsonObject
                            {
                                ["fieldCode"] = "amount",
                                ["fieldName"] = "金额",
                                ["dataType"] = "number",
                                ["order"] = 2
                            })
                    }
                }
            },
            ["runtimeContext"] = new JsonObject
            {
                ["pageCode"] = pageCode,
                ["pageName"] = pageName
            }
        };

        return document.ToJsonString();
    }

    private static string[] ReadChildren(JsonObject element) =>
        ReadChildren(element["children"]!.AsArray());

    private static string[] ReadChildren(JsonArray children) =>
        children.Select(child => child!.GetValue<string>()).ToArray();
}
