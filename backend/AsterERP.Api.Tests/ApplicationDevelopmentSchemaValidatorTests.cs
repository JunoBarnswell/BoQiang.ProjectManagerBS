using System.Text;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDevelopmentSchemaValidatorTests
{
    private readonly ApplicationDevelopmentSchemaValidator validator = new();

    [Fact]
    public void ValidateDraft_RejectsEditorState()
    {
        var document = ValidDocument();
        document["tree"] = new JsonObject { ["root"] = new JsonArray() };
        document["selectedElementId"] = "root";

        Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));
    }

    [Fact]
    public void ValidateDraft_RejectsTopLevelEditorSession()
    {
        var document = ValidDocument();
        document["editorSession"] = new JsonObject { ["selectedNodeIds"] = new JsonArray("root") };

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public void ValidateDraft_RejectsLegacyNodeDataBinding()
    {
        var document = ValidDocument();
        document["elements"]!.AsObject()["root"]!["dataBinding"] = new JsonObject
        {
            ["source"] = "page",
            ["path"] = "orders"
        };

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public void ValidateDraft_RejectsLegacyPropertyBindingAndSourcePathValues()
    {
        var document = ValidDocument();
        var root = document["elements"]!.AsObject()["root"]!.AsObject();
        root["bindings"] = new JsonObject { ["props"] = new JsonObject { ["title"] = new JsonObject { ["source"] = "variables", ["path"] = "title" } } };

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public void ValidateDraft_AcceptsGraphExpressionOnlyInPropertySlots()
    {
        var document = ValidDocument();
        document["elements"]!.AsObject()["root"]!["props"] = new JsonObject
        {
            ["title"] = new JsonObject
            {
                ["kind"] = "expression",
                ["expectedType"] = "string",
                ["graph"] = new JsonObject { ["root"] = new JsonObject { ["kind"] = "constant", ["value"] = "Title", ["valueType"] = "string" } }
            }
        };

        validator.ValidateDraft(document.ToJsonString());
    }

    [Theory]
    [InlineData("free")]
    [InlineData("flex")]
    [InlineData("grid")]
    [InlineData("constraints")]
    public void ValidateDraft_AcceptsCanonicalLayoutProtocolForEveryMode(string mode)
    {
        var document = ValidDocument();
        document["elements"]!.AsObject()["root"]!.AsObject()["layout"] = new JsonObject
        {
            ["protocol"] = CanonicalLayoutProtocol(mode)
        };

        validator.ValidateDraft(document.ToJsonString());
    }

    [Fact]
    public void ValidateDraft_KeepsLegacyLayoutCompatibleWhenProtocolIsAbsent()
    {
        var document = ValidDocument();
        document["elements"]!.AsObject()["root"]!.AsObject()["layout"] = new JsonObject
        {
            ["display"] = "flex",
            ["layoutMode"] = "flex",
            ["width"] = "100%"
        };

        validator.ValidateDraft(document.ToJsonString());
    }

    [Fact]
    public void ValidateDraft_RejectsCanonicalLayoutProtocolWithStablePlacementCodeAndPath()
    {
        var document = ValidDocument();
        var protocol = CanonicalLayoutProtocol("free");
        protocol["placement"]!["kind"] = "grid-item";
        document["elements"]!.AsObject()["root"]!.AsObject()["layout"] = new JsonObject { ["protocol"] = protocol };

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Contains("[LAYOUT_PLACEMENT_KIND_INVALID] elements.root.layout.protocol.placement.kind", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateDraft_RejectsCanonicalLayoutProtocolWithStableDimensionCodeAndPath()
    {
        var document = ValidDocument();
        var protocol = CanonicalLayoutProtocol("free");
        protocol["size"]!["width"] = "-20px";
        document["elements"]!.AsObject()["root"]!.AsObject()["layout"] = new JsonObject { ["protocol"] = protocol };

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Contains("[LAYOUT_DIMENSION_INVALID] elements.root.layout.protocol.size.width", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateDraft_RejectsCanonicalGridSpanAndConstraintStrategy()
    {
        var gridDocument = ValidDocument();
        var gridProtocol = CanonicalLayoutProtocol("grid");
        gridProtocol["placement"]!["gridItem"]!["rowSpan"] = 0;
        gridDocument["elements"]!.AsObject()["root"]!.AsObject()["layout"] = new JsonObject { ["protocol"] = gridProtocol };

        var gridException = Assert.Throws<ValidationException>(() => validator.ValidateDraft(gridDocument.ToJsonString()));

        var constraintDocument = ValidDocument();
        var constraintProtocol = CanonicalLayoutProtocol("constraints");
        constraintProtocol["container"]!["constraints"]!["coordinateSpace"] = "viewport";
        constraintDocument["elements"]!.AsObject()["root"]!.AsObject()["layout"] = new JsonObject { ["protocol"] = constraintProtocol };

        var constraintException = Assert.Throws<ValidationException>(() => validator.ValidateDraft(constraintDocument.ToJsonString()));

        Assert.Contains("[LAYOUT_GRID_SPAN_INVALID] elements.root.layout.protocol.placement.gridItem.rowSpan", gridException.Message, StringComparison.Ordinal);
        Assert.Contains("[LAYOUT_CONSTRAINT_STRATEGY_REQUIRED] elements.root.layout.protocol.container.constraints.coordinateSpace", constraintException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateDraft_RejectsNumericSchemaVersion()
    {
        var document = ValidDocument();
        document["schemaVersion"] = 3;

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public void ValidateDraft_RejectsUnreachableAndCyclicElements()
    {
        var document = ValidDocument();
        var elements = document["elements"]!.AsObject();
        elements["orphan"] = Element("orphan", null, []);
        elements["root"]!["children"] = new JsonArray("child");
        elements["child"]!["children"] = new JsonArray("root");

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, exception.Code);
    }

    [Fact]
    public void ValidateRuntimeArtifact_RejectsEditorStateAndPrototypePollution()
    {
        var editorStateDocument = ValidDocument();
        editorStateDocument["viewport"] = new JsonObject();
        var editorStateException = Assert.Throws<ValidationException>(() =>
            validator.ValidateRuntimeArtifact(editorStateDocument.ToJsonString()));

        var pollutedDocument = ValidDocument();
        pollutedDocument["__proto__"] = new JsonObject();
        var pollutionException = Assert.Throws<ValidationException>(() =>
            validator.ValidateRuntimeArtifact(pollutedDocument.ToJsonString()));

        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, editorStateException.Code);
        Assert.Equal(ErrorCodes.DesignerSchemaInvalid, pollutionException.Code);
    }

    [Fact]
    public void ValidateDraft_RejectsOversizedPayloadWith413Semantic()
    {
        var document = ValidDocument();
        document["description"] = new string('x', ApplicationDevelopmentSchemaValidator.DraftMaximumBytes);

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Equal(ErrorCodes.SchemaOrPayloadTooLarge, exception.Code);
    }

    [Fact]
    public void ValidateRuntimeArtifact_AllowsTwoThousandNodeArtifactWithinOneMiB()
    {
        var document = LargeRuntimeDocument(2_000);
        var json = document.ToJsonString();

        Assert.True(Encoding.UTF8.GetByteCount(json) > 256 * 1024);
        Assert.True(Encoding.UTF8.GetByteCount(json) < ApplicationDevelopmentSchemaValidator.RuntimeMaximumBytes);

        validator.ValidateRuntimeArtifact(json);
    }

    [Fact]
    public void ValidateDraft_RejectsActionAndValuePathLimits()
    {
        var document = ValidDocument();
        document["actions"] = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "action-1",
                ["steps"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = "step-1",
                        ["valuePath"] = string.Join('.', Enumerable.Repeat("segment", 33))
                    }
                }
            }
        };

        var exception = Assert.Throws<ValidationException>(() => validator.ValidateDraft(document.ToJsonString()));

        Assert.Equal(ErrorCodes.SchemaOrPayloadTooLarge, exception.Code);
    }

    private static JsonObject ValidDocument() => new()
    {
        ["documentId"] = "document-1",
        ["revision"] = 1,
        ["pages"] = new JsonArray(new JsonObject
        {
            ["id"] = "page",
            ["rootElementId"] = "root"
        }),
        ["elements"] = new JsonObject
        {
            ["root"] = Element("root", null, ["child"]),
            ["child"] = Element("child", "root", [])
        }
    };

    private static JsonObject LargeRuntimeDocument(int nodeCount)
    {
        const int groupCount = 2;
        var elements = new JsonObject
        {
            ["root"] = Element("root", null, ["group-0", "group-1"]),
            ["group-0"] = Element("group-0", "root", []),
            ["group-1"] = Element("group-1", "root", [])
        };
        var leavesPerGroup = (nodeCount - 1 - groupCount) / groupCount;
        var remaining = nodeCount - 1 - groupCount - leavesPerGroup * groupCount;

        for (var groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            var groupId = $"group-{groupIndex}";
            var children = elements[groupId]!["children"]!.AsArray();
            var count = leavesPerGroup + (groupIndex == groupCount - 1 ? remaining : 0);
            for (var childIndex = 0; childIndex < count; childIndex++)
            {
                var id = $"node-{groupIndex}-{childIndex}";
                children.Add(id);
                elements[id] = new JsonObject
                {
                    ["id"] = id,
                    ["parentId"] = groupId,
                    ["children"] = new JsonArray(),
                    ["type"] = "text.paragraph",
                    ["props"] = new JsonObject { ["value"] = new string('x', 150) }
                };
            }
        }

        return new JsonObject
        {
            ["documentId"] = "runtime-2000",
            ["revision"] = 1,
            ["pages"] = new JsonArray(new JsonObject { ["id"] = "page", ["rootElementId"] = "root" }),
            ["elements"] = elements
        };
    }

    private static JsonObject Element(string id, string? parentId, string[] children) => new()
    {
        ["id"] = id,
        ["parentId"] = parentId,
        ["children"] = new JsonArray(children.Select(child => (JsonNode?)child).ToArray()),
        ["type"] = "layout.page"
    };

    private static JsonObject CanonicalLayoutProtocol(string mode) => mode switch
    {
        "free" => new JsonObject
        {
            ["container"] = new JsonObject { ["mode"] = "free" },
            ["placement"] = new JsonObject
            {
                ["kind"] = "absolute",
                ["absolute"] = new JsonObject { ["x"] = 12, ["y"] = 24 }
            },
            ["size"] = new JsonObject { ["width"] = 320, ["height"] = "240px" }
        },
        "flex" => new JsonObject
        {
            ["container"] = new JsonObject
            {
                ["mode"] = "flex",
                ["flex"] = new JsonObject
                {
                    ["direction"] = "row",
                    ["wrap"] = "nowrap",
                    ["gap"] = 8,
                    ["alignItems"] = "stretch",
                    ["justifyContent"] = "start"
                }
            },
            ["placement"] = new JsonObject
            {
                ["kind"] = "flex-item",
                ["flexItem"] = new JsonObject { ["order"] = 0, ["grow"] = 1, ["shrink"] = 1, ["basis"] = "auto" }
            },
            ["size"] = new JsonObject { ["width"] = "100%", ["height"] = "auto" }
        },
        "grid" => new JsonObject
        {
            ["container"] = new JsonObject
            {
                ["mode"] = "grid",
                ["grid"] = new JsonObject
                {
                    ["columns"] = new JsonArray("1fr", "2fr"),
                    ["rows"] = new JsonArray("auto"),
                    ["columnGap"] = 4,
                    ["rowGap"] = 4,
                    ["autoFlow"] = "row"
                }
            },
            ["placement"] = new JsonObject
            {
                ["kind"] = "grid-item",
                ["gridItem"] = new JsonObject { ["rowStart"] = 1, ["rowSpan"] = 1, ["columnStart"] = "auto", ["columnSpan"] = 1 }
            },
            ["size"] = new JsonObject { ["width"] = "min-content", ["height"] = 120 }
        },
        "constraints" => new JsonObject
        {
            ["container"] = new JsonObject
            {
                ["mode"] = "constraints",
                ["constraints"] = new JsonObject { ["coordinateSpace"] = "parent-padding-box" }
            },
            ["placement"] = new JsonObject
            {
                ["kind"] = "constrained",
                ["constrained"] = new JsonObject { ["left"] = 16, ["top"] = 16, ["stretchX"] = true }
            },
            ["size"] = new JsonObject { ["width"] = 240, ["height"] = 80 }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };
}
