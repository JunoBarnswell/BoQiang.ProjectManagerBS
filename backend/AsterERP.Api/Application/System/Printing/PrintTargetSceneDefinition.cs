using System.Text.Json.Nodes;

namespace AsterERP.Api.Application.System.Printing;

public sealed record PrintTargetSceneDefinition(
    string Scene,
    string? ListViewCode,
    string? DetailProviderKey,
    Func<JsonObject> CreateTestData);
