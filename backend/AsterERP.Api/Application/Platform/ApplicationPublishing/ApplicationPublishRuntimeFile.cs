namespace AsterERP.Api.Application.Platform.ApplicationPublishing;

public sealed record ApplicationPublishRuntimeFile(
    string AppCode,
    string TenantId,
    string BackendHost,
    int BackendPort,
    string BackendUrls,
    string FrontendBasePath,
    string FrontendApiBaseUrl,
    string FrontendOutputPath);
