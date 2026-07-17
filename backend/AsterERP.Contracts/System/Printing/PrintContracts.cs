using AsterERP.Contracts.System.QueryViews;

namespace AsterERP.Contracts.System.Printing;

public sealed record PrintVariableNodeResponse(
    string Id,
    string Label,
    bool IsArray,
    IReadOnlyList<PrintVariableNodeResponse> Children);

public sealed record PrintTargetOptionResponse(
    string MenuCode,
    string MenuName,
    string? RoutePath,
    string DefaultTitle,
    bool SupportsAssets,
    IReadOnlyList<string> SupportedScenes);

public sealed record PrintTargetDetailResponse(
    string MenuCode,
    string MenuName,
    string? RoutePath,
    string DefaultTitle,
    bool SupportsAssets,
    IReadOnlyList<string> SupportedScenes,
    string ActiveScene,
    string? ListViewCode,
    string? DetailProviderKey,
    object? TestData,
    IReadOnlyList<PrintVariableNodeResponse> AvailableVariables);

public sealed record PrintTemplateListItemResponse(
    string Id,
    string Name,
    string MenuCode,
    string MenuName,
    string? RoutePath,
    string Scene,
    string TemplateCode,
    string Status,
    bool IsDefault,
    long UpdatedAt,
    object? Ext,
    object? Permissions,
    string? Remark);

public sealed record PrintTemplateDetailResponse(
    string Id,
    string Name,
    string MenuCode,
    string MenuName,
    string? RoutePath,
    string Scene,
    string TemplateCode,
    string Status,
    bool IsDefault,
    long UpdatedAt,
    object? Data,
    object? Ext,
    object? Permissions,
    string? Remark);

public sealed record PrintTemplateUpsertRequest(
    string? Id,
    string Name,
    string? MenuCode,
    string? Scene,
    string? TemplateCode,
    object? Data,
    long? UpdatedAt,
    object? Ext,
    object? Permissions,
    string? Remark);

public sealed record PrintCustomElementListItemResponse(
    string Id,
    string Name,
    long UpdatedAt,
    object? Ext,
    object? Permissions);

public sealed record PrintCustomElementDetailResponse(
    string Id,
    string Name,
    object? Element,
    long UpdatedAt,
    object? Ext,
    object? Permissions);

public sealed record PrintCustomElementUpsertRequest(
    string? Id,
    string Name,
    object? Element,
    long? UpdatedAt,
    object? Ext,
    object? Permissions);

public sealed record PrintTemplateResolveRequest(
    string MenuCode,
    string Scene,
    string? Mode,
    string? TemplateId,
    string? DetailId,
    int PageIndex,
    int PageSize,
    IReadOnlyList<string> SelectedIds,
    IReadOnlyList<QueryViewQueryCondition> Conditions,
    IReadOnlyList<QueryViewQuerySort> Sorts);

public sealed record PrintTemplateResolveResponse(
    string TemplateId,
    string TemplateName,
    string TemplateCode,
    string Scene,
    string SuggestedFileName,
    object? Data,
    object? TestData,
    object? Variables,
    bool SupportsAssets,
    IReadOnlyList<PrintVariableNodeResponse> AvailableVariables);
