namespace AsterERP.Api.Application.System.Printing;

public sealed record PrintTargetDefinition(
    string MenuCode,
    string DefaultTitle,
    bool SupportsAssets,
    IReadOnlyList<PrintTargetSceneDefinition> Scenes);
