using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LowCodeRefactorAssetContractTests
{
    [Fact]
    public void Phase_baselines_contract_and_golden_assets_are_valid_and_resolve()
    {
        var root = FindRepositoryRoot();
        var phase0 = ReadObject(root, "docs/low-code-refactor/phase0-baseline.json");
        var phase1 = ReadObject(root, "docs/low-code-refactor/phase1-baseline.json");
        var contract = ReadObject(root, "docs/low-code-refactor/shared-designer-contract.json");
        var manifest = ReadObject(root, "docs/low-code-refactor/fixtures/manifest.json");
        var golden = ReadObject(root, "docs/low-code-refactor/golden-cases.json");

        Assert.Equal(0, phase0["phase"]!.GetValue<int>());
        Assert.Equal(1, phase1["phase"]!.GetValue<int>());
        Assert.Equal("latest-only", contract["semanticPolicy"]!.GetValue<string>());
        Assert.Equal("fixtures/manifest.json", phase1["fixtureManifest"]!.GetValue<string>());
        Assert.Equal("fixtures/manifest.json", golden["fixtureManifest"]!.GetValue<string>());

        var fixtureIds = manifest["fixtures"]!.AsArray()
            .Select(item => item!["fixtureId"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var fixtureFiles = manifest["fixtures"]!.AsArray();
        Assert.Equal(manifest["source"]!["pageCount"]!.GetValue<int>(), fixtureFiles.Count);

        foreach (var fixture in fixtureFiles)
        {
            var fixtureId = fixture!["fixtureId"]!.GetValue<string>();
            var relativePath = fixture["file"]!.GetValue<string>();
            var path = Path.Combine(root, "docs/low-code-refactor/fixtures", relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(path), $"Missing fixture {fixtureId}: {path}");

            var fixtureObject = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.Equal(fixtureId, fixtureObject["fixtureId"]!.GetValue<string>());
            var document = fixtureObject["document"]!.AsObject();
            Assert.Equal(fixtureObject["document"]!["documentId"]!.GetValue<string>(), document["documentId"]!.GetValue<string>());
            Assert.Equal(fixture["fixtureNodeCount"]?.GetValue<int>() ?? fixture["nodeCount"]?.GetValue<int>(), document["elements"]!.AsObject().Count);
            Assert.Equal(fixture["fixtureActionCount"]?.GetValue<int>() ?? fixture["actionCount"]?.GetValue<int>() ?? 0, CountActionEvents(document));
            Assert.Equal(fixture["bindingCount"]?.GetValue<int>() ?? fixture["fixtureBindingCount"]?.GetValue<int>() ?? 0, CountBindings(document));

            var canonical = ApplicationDesignerCanonicalJson.NormalizeObject(document.ToJsonString());
            Assert.Equal(64, ApplicationDesignerCanonicalJson.ComputeHash(canonical).Length);
            new ApplicationDevelopmentSchemaValidator().ValidateDraft(canonical);
        }

        foreach (var goldenCase in golden["cases"]!.AsArray())
        {
            var fixture = goldenCase!["fixture"]!.GetValue<string>();
            var isGenerated = phase1["generatedFixtures"]!.AsArray().Any(item => item!["fixtureId"]!.GetValue<string>() == fixture);
            Assert.True(fixtureIds.Contains(fixture) || isGenerated, $"Golden case references unknown fixture {fixture}");
        }
    }

    [Fact]
    public void Generated_tree_fixture_is_deterministic_bounded_and_valid()
    {
        var document = CreateGeneratedTree(1000);
        var canonical = ApplicationDesignerCanonicalJson.NormalizeObject(document.ToJsonString());
        var validator = new ApplicationDevelopmentSchemaValidator();

        validator.ValidateDraft(canonical);
        Assert.Equal(1000, document["elements"]!.AsObject().Count);
        Assert.Equal(
            ApplicationDesignerCanonicalJson.ComputeHash(canonical),
            ApplicationDesignerCanonicalJson.ComputeHash(ApplicationDesignerCanonicalJson.NormalizeObject(document.ToJsonString())));
    }

    [Fact]
    public void Generated_wide_table_fixture_is_deterministic_and_bounded()
    {
        var document = CreateGeneratedTable(100);
        new ApplicationDevelopmentSchemaValidator().ValidateDraft(document.ToJsonString());

        var columns = document["elements"]!["table"]!["props"]!["columns"]!.AsArray();
        Assert.Equal(100, columns.Count);
        Assert.Equal(100, columns.Select(item => item!["key"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
    }

    private static JsonObject CreateGeneratedTree(int count)
    {
        var elements = new JsonObject
        {
            ["root"] = Element("root", null, [])
        };
        for (var index = 1; index < count; index++)
        {
            var parent = index <= 2 ? "root" : $"node-{index / 2}";
            var id = $"node-{index}";
            elements[id] = Element(id, parent, []);
            elements[parent]!["children"]!.AsArray().Add(id);
        }

        var document = Document("generated-1000-node", elements);
        document["pages"]!.AsArray()[0]!["rootElementId"] = "root";
        return document;
    }

    private static JsonObject CreateGeneratedTable(int count)
    {
        var table = Element("table", "root", []);
        var columns = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            columns.Add(new JsonObject { ["key"] = $"column-{index:000}", ["valueType"] = "string" });
        }

        table["props"] = new JsonObject { ["columns"] = columns };
        var elements = new JsonObject { ["root"] = Element("root", null, ["table"]), ["table"] = table };
        return Document("generated-wide-table", elements);
    }

    private static JsonObject Document(string id, JsonObject elements) => new()
    {
        ["documentId"] = id,
        ["revision"] = 1,
        ["pages"] = new JsonArray(new JsonObject { ["id"] = id, ["rootElementId"] = "root" }),
        ["elements"] = elements,
        ["actions"] = new JsonArray(),
        ["variables"] = new JsonArray(),
        ["permissions"] = new JsonObject(),
        ["runtimeContext"] = new JsonObject(),
        ["metadata"] = new JsonObject()
    };

    private static JsonObject Element(string id, string? parentId, string[] children) => new()
    {
        ["id"] = id, ["type"] = "layout.container", ["parentId"] = parentId,
        ["children"] = new JsonArray(children.Select(child => (JsonNode?)child).ToArray()),
        ["props"] = new JsonObject(), ["layout"] = new JsonObject(), ["style"] = new JsonObject(),
        ["events"] = new JsonArray(), ["bindings"] = new JsonObject()
    };

    private static int CountBindings(JsonObject document) => document["elements"]!.AsObject().Sum(item =>
        item.Value!["bindings"] is JsonObject bindings
            ? bindings.Sum(binding => binding.Value is JsonObject reference && reference.ContainsKey("resourceId") && reference.ContainsKey("displayName") && reference.ContainsKey("valueType") && reference.ContainsKey("conversionPipeline") ? 1 : throw new Xunit.Sdk.XunitException($"Invalid binding on {item.Key}.{binding.Key}"))
            : 0);

    private static int CountActionEvents(JsonObject document) => document["elements"]!.AsObject().Sum(item =>
        item.Value!["events"] is JsonArray events ? events.Count(eventNode => eventNode?["steps"] is JsonArray) : 0);

    private static JsonObject ReadObject(string root, string relativePath) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar))))!.AsObject();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("AsterERP.sln was not found");
    }
}
