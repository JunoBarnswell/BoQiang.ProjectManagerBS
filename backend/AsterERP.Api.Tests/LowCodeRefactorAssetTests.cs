using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class LowCodeRefactorAssetTests
{
    [Fact]
    public void Fixture_manifest_is_parseable_complete_and_validates_against_latest_backend_contract()
    {
        var root = RepositoryRoot();
        var manifest = Read(root, "docs/low-code-refactor/fixtures/manifest.json");
        var fixtures = manifest["fixtures"]!.AsArray();
        var validator = new ApplicationDevelopmentSchemaValidator();
        var fixtureIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var manifestNode in fixtures)
        {
            var entry = manifestNode!.AsObject();
            var fixtureId = Required(entry, "fixtureId");
            Assert.True(fixtureIds.Add(fixtureId), $"duplicate fixture id: {fixtureId}");
            var relativePath = Required(entry, "file");
            var fixture = Read(root, Path.Combine("docs/low-code-refactor/fixtures", relativePath));
            Assert.Equal(fixtureId, Required(fixture, "fixtureId"));

            var document = fixture["document"]!.AsObject();
            validator.ValidateDraft(document.ToJsonString());
            Assert.Equal(entry["fixtureNodeCount"]?.GetValue<int>() ?? entry["nodeCount"]!.GetValue<int>(), document["elements"]!.AsObject().Count);
            Assert.Equal(entry["fixtureActionCount"]?.GetValue<int>() ?? entry["actionCount"]?.GetValue<int>() ?? 0, CountEventsWithSteps(document));
            Assert.Equal(entry["fixtureBindingCount"]?.GetValue<int>() ?? entry["bindingCount"]?.GetValue<int>() ?? 0, CountBindings(document));
            Assert.Matches("^[0-9a-f]{64}$", Required(entry, "sourcePageFingerprint"));
            Assert.Matches("^[0-9a-f]{64}$", Required(entry, "sourceLayoutDraftSha256"));
            Assert.DoesNotContain("tenant-a", fixture.ToJsonString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", fixture.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(manifest["source"]!["pageCount"]!.GetValue<int>(), fixtures.Count);
    }

    [Fact]
    public void Golden_cases_reference_existing_fixtures_and_define_machine_checkable_expectations()
    {
        var root = RepositoryRoot();
        var manifest = Read(root, "docs/low-code-refactor/fixtures/manifest.json");
        var fixtureIds = manifest["fixtures"]!.AsArray().Select(node => Required(node!.AsObject(), "fixtureId")).ToHashSet(StringComparer.Ordinal);
        var golden = Read(root, "docs/low-code-refactor/golden-cases.json");

        foreach (var node in golden["cases"]!.AsArray())
        {
            var goldenCase = node!.AsObject();
            var fixture = Required(goldenCase, "fixture");
            if (!fixtureIds.Contains(fixture))
            {
                Assert.Equal("generated", Required(goldenCase, "fixtureKind"));
                Assert.NotEmpty(goldenCase["generation"]!.AsObject());
            }
            Assert.False(string.Equals("placeholder", Required(goldenCase, "action"), StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(goldenCase["expected"]!.AsObject());
        }

        var gc002 = golden["cases"]!.AsArray().Single(node => Required(node!.AsObject(), "id") == "GC-002")!.AsObject();
        Assert.Contains("form", gc002["expected"]!["bindingSources"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Contains("dataset", gc002["expected"]!["bindingSources"]!.AsArray().Select(item => item!.GetValue<string>()));
    }

    [Fact]
    public void Capability_and_provider_matrices_have_complete_fail_closed_dimensions()
    {
        var root = RepositoryRoot();
        var components = Read(root, "docs/low-code-refactor/component-capability-matrix.json");
        var dimensions = components["requiredDimensions"]!.AsArray().Select(item => item!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        var componentTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in components["components"]!.AsArray())
        {
            var component = node!.AsObject();
            Assert.True(componentTypes.Add(Required(component, "type")));
            foreach (var dimension in dimensions)
                Assert.True(component.ContainsKey(dimension), $"missing capability '{dimension}' for {Required(component, "type")}");
            Assert.Contains(Required(component, "security"), new[] { "S0", "S1", "S2", "S3" });
        }

        var providers = Read(root, "docs/low-code-refactor/database-provider-capability-matrix.json")["providers"]!.AsArray();
        Assert.Equal(new[] { "SqlServer", "MySql", "PostgreSql", "Sqlite" }, providers.Select(node => Required(node!.AsObject(), "provider")));
        foreach (var node in providers)
        {
            var provider = node!.AsObject();
            foreach (var capability in new[] { "catalog", "transactions", "ddlPlan", "typedEdit", "queryCancel", "viewReplace" })
                Assert.Equal("required", Required(provider, capability), ignoreCase: true);
            Assert.Equal("provider", Required(provider, "identifierQuote"), ignoreCase: true);
        }
    }

    [Fact]
    public void Performance_and_migration_assets_fail_closed_without_fabricating_runtime_evidence()
    {
        var root = RepositoryRoot();
        var baseline = Read(root, "docs/low-code-refactor/performance-baseline.json");
        Assert.Equal(5, baseline["measurementPolicy"]!["runs"]!.GetValue<int>());
        foreach (var node in baseline["scenarios"]!.AsArray())
        {
            var scenario = node!.AsObject();
            var status = Required(scenario, "status");
            Assert.Contains(status, new[] { "PendingExecution", "Measured", "Pass", "Fail", "Blocked" });
            if (status == "Blocked")
                Assert.False(string.IsNullOrWhiteSpace(Required(scenario, "blockedReason")));
            Assert.False(string.IsNullOrWhiteSpace(Required(scenario, "evidencePath")));
        }

        var evidenceSchema = Read(root, "docs/low-code-refactor/migration-evidence.schema.json");
        Assert.Equal("astererp.low-code.migration-evidence.v1", Required(evidenceSchema, "format"));
        foreach (var field in new[] { "backupSha256", "previousArtifactId", "publishedArtifactId", "healthCheckId" })
            Assert.Contains(field, evidenceSchema["required"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Contains("blockedReason", evidenceSchema["blockedRequires"]!.AsArray().Select(item => item!.GetValue<string>()));
    }

    private static int CountEventsWithSteps(JsonObject document) => document["elements"]!.AsObject().Select(item => item.Value)
        .OfType<JsonObject>()
        .SelectMany(element => element["events"]?.AsArray() ?? [])
        .OfType<JsonObject>()
        .Count(@event => @event["steps"] is JsonArray steps && steps.Count > 0);

    private static int CountBindings(JsonObject document) => document["elements"]!.AsObject().Select(item => item.Value)
        .OfType<JsonObject>()
        .SelectMany(element => element["bindings"]?.AsObject() ?? [])
        .Count();

    private static JsonObject Read(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        Assert.True(File.Exists(path), $"missing asset: {path}");
        var json = File.ReadAllText(path, Encoding.UTF8);
        using var parsed = JsonDocument.Parse(json);
        using var hash = SHA256.Create();
        Assert.NotEmpty(hash.ComputeHash(Encoding.UTF8.GetBytes(json)));
        return JsonNode.Parse(json)!.AsObject();
    }

    private static string Required(JsonObject value, string propertyName)
    {
        var property = value[propertyName];
        Assert.NotNull(property);
        var text = property!.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(text), $"required asset field is empty: {propertyName}");
        return text;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
