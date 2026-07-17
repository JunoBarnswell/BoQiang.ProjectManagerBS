namespace AsterERP.Contracts.Platform;

public sealed record ApplicationPublishRequest(
    string? Version,
    string TenantId,
    string? Remark,
    bool CleanOutput = false,
    bool IncludeFrontend = true,
    bool IncludeBackend = true,
    string? BackendHost = null,
    int? BackendPort = null,
    string? FrontendBasePath = null,
    string? FrontendApiBaseUrl = null);
