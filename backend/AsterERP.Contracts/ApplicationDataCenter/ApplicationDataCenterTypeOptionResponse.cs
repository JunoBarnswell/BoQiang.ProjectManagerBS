namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterTypeOptionResponse(
    string ModuleKey,
    string Type,
    string Title,
    string Description,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> TestActions);
