using System.Text.Json;
using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class DesignerDocumentReferenceExtractorTests
{
    private readonly DesignerDocumentReferenceExtractor _extractor = new();

    [Fact]
    public void ExtractsOnlyDeclaredLatestContractReferenceFields()
    {
        using var document = JsonDocument.Parse("""{"metadata":{"note":"source-42"},"dataSources":[{"dataSourceId":"source-42"}],"elements":{"root":{"props":{"value":{"resourceId":"resource-42"}},"bindings":{"data":{"queryId":"query-42"}}}}}""");

        var references = _extractor.Extract(document.RootElement);

        using var metadataOnly = JsonDocument.Parse("""{"metadata":{"note":"source-42"}}""");
        Assert.Contains("source-42", references);
        Assert.Contains("resource-42", references);
        Assert.Contains("query-42", references);
        Assert.DoesNotContain("source-42", _extractor.Extract(metadataOnly.RootElement));
    }

    [Fact]
    public void IgnoresDisplayTextAndMalformedDocuments()
    {
        const string document = """{"elements":{"root":{"props":{"title":"source-42"}}}}""";
        Assert.False(_extractor.References(document, "source-42"));
        Assert.False(_extractor.References("{", "source-42"));
    }
}
