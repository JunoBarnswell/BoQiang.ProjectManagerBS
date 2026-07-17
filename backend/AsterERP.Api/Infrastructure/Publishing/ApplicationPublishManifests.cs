namespace AsterERP.Api.Infrastructure.Publishing;

public sealed record ApplicationPublishDependencyFile(
    string Path,
    string ModuleKey,
    string Reason,
    string Sha256,
    long SizeBytes);

public sealed record ApplicationPublishClosureEdge(
    string SourceKind,
    string SourceValue,
    string ModuleKey,
    string Reason);

public sealed record ApplicationPublishUnresolvedDependency(
    string Kind,
    string Value,
    string Reason);

public sealed record ApplicationPublishLeakScanFinding(
    string FilePath,
    string ModuleKey,
    string MarkerKind,
    string Marker,
    string Encoding,
    long Offset);

public sealed record ApplicationPublishLeakScanReport(
    DateTime ScannedAt,
    int ScannedFileCount,
    int ForbiddenMarkerCount,
    IReadOnlyList<ApplicationPublishLeakScanFinding> Findings);

public sealed record ApplicationPublishRuntimeConfig(
    string BackendHost,
    int BackendPort,
    string BackendUrls,
    string FrontendBasePath,
    string FrontendApiBaseUrl,
    string FrontendOutputPath);

public sealed record ApplicationPublishDependencySnapshot(
    string AppCode,
    string? TenantId,
    string PublishMode,
    IReadOnlyList<object> Menus,
    IReadOnlyList<object> PageSchemas,
    IReadOnlyList<object> DataModels,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<string> BackendRoutes,
    IReadOnlyList<string> FrontendRoutes,
    IReadOnlyList<string> Providers,
    IReadOnlyList<string> ResolvedModules,
    IReadOnlyList<ApplicationPublishClosureEdge> ClosureEdges,
    IReadOnlyList<ApplicationPublishUnresolvedDependency> UnresolvedDependencies,
    string ModuleFileMapSha256);

public sealed record ApplicationPublishManifest(
    string AppCode,
    string TaskId,
    string? TenantId,
    string RuntimeIdentifier,
    bool SelfContained,
    DateTime CreatedAt,
    string SourcePath,
    string ReleasePath,
    ApplicationPublishRuntimeConfig RuntimeConfig,
    ApplicationPublishDependencySnapshot DependencySnapshot,
    IReadOnlyList<ApplicationPublishDependencyFile> IncludedFiles,
    IReadOnlyList<string> ExcludedPatterns,
    IReadOnlyList<string> BuildCommands,
    ApplicationPublishLeakScanReport LeakScan);

public sealed record ApplicationPublishSourceResult(
    string SourceRoot,
    string ManifestRoot,
    IReadOnlyList<ApplicationPublishDependencyFile> IncludedFiles);
