using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDesignerCanonicalHashFixtureTests
{
    [Fact]
    public void Designer_document_hash_matches_shared_fixture_and_excludes_only_top_level_hash()
    {
        var fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "docs/low-code-refactor/fixtures/designer-document-hash-fixture.json")))!.AsObject();
        var document = fixture["document"]!.AsObject();
        var expectedHash = fixture["expectedHash"]!.GetValue<string>();

        Assert.StartsWith("sha256:", expectedHash, StringComparison.Ordinal);
        Assert.Equal(expectedHash, ApplicationDesignerCanonicalJson.ComputeDocumentHash(document.ToJsonString()));

        var normalized = JsonNode.Parse(ApplicationDesignerCanonicalJson.NormalizeDocument(document.ToJsonString()))!.AsObject();
        Assert.False(normalized.ContainsKey("documentHash"));
        Assert.Equal("sha256:preserve-nested-value", normalized["elements"]!["root"]!["props"]!["value"]!["documentHash"]!.GetValue<string>());

        document["documentHash"] = "sha256:different";
        Assert.Equal(expectedHash, ApplicationDesignerCanonicalJson.ComputeDocumentHash(document.ToJsonString()));

        document["elements"]!["root"]!["props"]!["value"]!["documentHash"] = "sha256:changed-nested-value";
        Assert.NotEqual(expectedHash, ApplicationDesignerCanonicalJson.ComputeDocumentHash(document.ToJsonString()));
    }

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
