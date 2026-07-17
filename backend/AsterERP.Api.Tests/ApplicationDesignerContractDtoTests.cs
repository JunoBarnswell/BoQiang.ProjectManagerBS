using System.Text.Json;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDesignerContractDtoTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    [Fact]
    public void Document_dto_serializes_the_latest_document_shape()
    {
        var dto = new ApplicationDesignerDocumentDto(
            "document-1", 7, "sha256:document", Json("{}"), Json("[]"), Json("{}"), Json("[]"),
            Json("[]"), Json("[]"), Json("[]"), Json("[]"), Json("[]"), Json("{}"), Json("{}"),
            Json("[]"), Json("{}"), Json("{}"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, JsonOptions));
        var properties = json.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(
            ["documentId", "revision", "documentHash", "metadata", "pages", "elements", "actions", "apiBindings", "dataSources", "modals", "pageMicroflows", "pageParameters", "permissions", "styleTokens", "variables", "workflowBindings", "runtimeContext"],
            properties);
        Assert.False(json.RootElement.TryGetProperty("bindings", out _));
    }

    [Fact]
    public void Editor_session_dto_preserves_nullable_selection_and_transaction_fields()
    {
        var dto = new ApplicationDesignerEditorSessionDto(
            "session-1", "document-1", null, ["root"], null, Json("{}"), Json("{}"), null);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, JsonOptions));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("primaryNodeId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("anchorNodeId").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("transactionId").ValueKind);
    }

    [Fact]
    public void Runtime_artifact_dto_serializes_required_integrity_and_projection_fields()
    {
        var dto = new ApplicationDesignerRuntimeArtifactDto(
            "document-1", 7, "sha256:artifact", "signature", "runtime-1", ["layout.page"],
            Json("{}"), Json("{}"), Json("[]"), Json("[]"), Json("[]"), Json("[]"), Json("{}"), Json("[]"), Json("{}"));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, JsonOptions));

        Assert.Equal("sha256:artifact", json.RootElement.GetProperty("artifactHash").GetString());
        Assert.Equal("signature", json.RootElement.GetProperty("signature").GetString());
        Assert.True(json.RootElement.TryGetProperty("integrityPayload", out _));
        Assert.True(json.RootElement.TryGetProperty("pageMicroflows", out _));
        Assert.True(json.RootElement.TryGetProperty("pageParameters", out _));
        Assert.True(json.RootElement.TryGetProperty("permissions", out _));
    }

    [Fact]
    public void Runtime_artifact_dto_rejects_missing_or_null_signature()
    {
        var missingSignature = """{"documentId":"document-1","revision":1,"artifactHash":"sha256:artifact","compilerVersion":"runtime-1","manifestTypes":[],"integrityPayload":{},"elements":{},"actions":[],"bindings":[],"pageMicroflows":[],"pageParameters":[],"permissions":{},"variables":[],"runtimeContext":{}}""";
        var nullSignature = missingSignature.Replace("\"compilerVersion\"", "\"signature\":null,\"compilerVersion\"", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ApplicationDesignerRuntimeArtifactDto>(missingSignature, JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ApplicationDesignerRuntimeArtifactDto>(nullSignature, JsonOptions));
    }

    private static JsonElement Json(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}
