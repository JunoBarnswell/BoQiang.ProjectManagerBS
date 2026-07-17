using System.Text.Json;

namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed record ApplicationDesignerRuntimeArtifactDto(
    string DocumentId,
    int Revision,
    string ArtifactHash,
    string Signature,
    string CompilerVersion,
    IReadOnlyList<string> ManifestTypes,
    JsonElement IntegrityPayload,
    JsonElement Elements,
    JsonElement Actions,
    JsonElement Bindings,
    JsonElement PageMicroflows,
    JsonElement PageParameters,
    JsonElement Permissions,
    JsonElement Variables,
    JsonElement RuntimeContext);
