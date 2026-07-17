namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseStudioQuery
{
    public string? ResourceType { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? Status { get; set; }

    public string? WorkspaceId { get; set; }

    public string? Category { get; set; }
}

public sealed class FlowiseOverviewDto
{
    public long ChatflowCount { get; set; }

    public long AgentflowCount { get; set; }

    public long ExecutionCount { get; set; }

    public long DocumentStoreCount { get; set; }

    public long EvaluationCount { get; set; }

    public long WorkspaceCount { get; set; }

    public FlowiseExecutionDto? LatestExecution { get; set; }
}

public sealed class FlowiseResourceTypeDto
{
    public string ResourceType { get; set; } = string.Empty;

    public string RouteSegment { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ViewPermission { get; set; } = string.Empty;

    public string EditPermission { get; set; } = string.Empty;

    public bool SupportsCanvas { get; set; }

    public bool SupportsSecret { get; set; }

    public bool SupportsRun { get; set; }
}

public sealed class FlowiseWorkspaceDto
{
    public string Id { get; set; } = string.Empty;

    public string WorkspaceKey { get; set; } = string.Empty;

    public string WorkspaceName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseWorkspaceUpsertRequest
{
    public string WorkspaceKey { get; set; } = string.Empty;

    public string WorkspaceName { get; set; } = string.Empty;

    public string? Status { get; set; }

    public string? Description { get; set; }
}

public sealed class FlowiseSharedWorkspaceDto
{
    public string WorkspaceId { get; set; } = string.Empty;

    public string WorkspaceName { get; set; } = string.Empty;

    public bool Shared { get; set; }
}

public sealed class FlowiseDatasetCsvImportDto
{
    public string DatasetId { get; set; } = string.Empty;

    public int ImportedRows { get; set; }

    public bool FirstRowHeaders { get; set; }
}

public sealed class FlowiseShareWorkspacesRequest
{
    public string ItemType { get; set; } = string.Empty;

    public IReadOnlyList<string> WorkspaceIds { get; set; } = [];
}

public sealed class FlowiseResourceDto
{
    public string Id { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string ResourceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? WorkspaceName { get; set; }

    public string? Category { get; set; }

    public string Status { get; set; } = string.Empty;

    public string DefinitionJson { get; set; } = "{}";

    public string MetadataJson { get; set; } = "{}";

    public string? SecretMask { get; set; }

    public string? OneTimeSecret { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class FlowiseResourceUpsertRequest
{
    public string ResourceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? Category { get; set; }

    public string? Status { get; set; }

    public string? DefinitionJson { get; set; }

    public string? MetadataJson { get; set; }

    public string? SecretValue { get; set; }
}

public sealed class FlowiseCanvasDto
{
    public string Id { get; set; } = string.Empty;

    public string ResourceId { get; set; } = string.Empty;

    public string FlowType { get; set; } = string.Empty;

    public string FlowData { get; set; } = "{}";

    public FlowiseCanvasValidationResultDto? Validation { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class FlowiseCanvasUpsertRequest
{
    public string ResourceId { get; set; } = string.Empty;

    public string? FlowType { get; set; }

    public string FlowData { get; set; } = "{}";
}

public sealed class FlowiseNodeCatalogItemDto
{
    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public int Version { get; set; } = 1;

    public IReadOnlyList<string> Tags { get; set; } = [];

    public IReadOnlyList<FlowiseNodeAnchorDto> InputAnchors { get; set; } = [];

    public IReadOnlyList<FlowiseNodeAnchorDto> OutputAnchors { get; set; } = [];

    public IReadOnlyList<FlowiseNodeInputParamDto> InputParams { get; set; } = [];
}

public sealed class FlowiseExecutionDto
{
    public string Id { get; set; } = string.Empty;

    public string ResourceId { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string FlowType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string InputJson { get; set; } = "{}";

    public string OutputJson { get; set; } = "{}";

    public string SourceDocumentsJson { get; set; } = "[]";

    public string? ActionJson { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public int DurationMs { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseExecutionStartRequest
{
    public string ResourceId { get; set; } = string.Empty;

    public string? InputJson { get; set; }

    public string? Question { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? ChatId { get; set; }

    public string? SessionId { get; set; }

    public IReadOnlyList<FlowiseChatHistoryMessageDto> ChatHistory { get; set; } = [];

    public Dictionary<string, object?>? Form { get; set; }

    public Dictionary<string, object?>? Webhook { get; set; }

    public FlowiseHumanInputResumeRequest? HumanInput { get; set; }
}

public sealed class FlowiseHumanInputResumeRequest
{
    public string ActionId { get; set; } = string.Empty;

    public string Choice { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public string PreviousExecutionId { get; set; } = string.Empty;

    public string PreviousExecutionDataJson { get; set; } = "[]";

    public string PreviousActionJson { get; set; } = "{}";
}

public sealed class FlowiseNodeAnchorDto
{
    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class FlowiseNodeInputParamDto
{
    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Type { get; set; } = "string";

    public bool Optional { get; set; }

    public bool AdditionalParams { get; set; }

    public bool AcceptVariable { get; set; }

    public string? Description { get; set; }

    public string? Placeholder { get; set; }

    public string DefaultJson { get; set; } = "null";

    public IReadOnlyList<FlowiseNodeOptionDto> Options { get; set; } = [];

    public IReadOnlyDictionary<string, object?>? Show { get; set; }

    public IReadOnlyDictionary<string, object?>? Hide { get; set; }
}

public sealed class FlowiseNodeOptionDto
{
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class FlowiseCanvasValidationIssueDto
{
    public string Code { get; set; } = string.Empty;

    public string Severity { get; set; } = "error";

    public string Message { get; set; } = string.Empty;

    public string? NodeId { get; set; }

    public string? EdgeId { get; set; }
}

public sealed class FlowiseCanvasValidationResultDto
{
    public bool Valid { get; set; }

    public IReadOnlyList<FlowiseCanvasValidationIssueDto> Issues { get; set; } = [];
}

public sealed class FlowiseImportRequest
{
    public string ResourceType { get; set; } = string.Empty;

    public string? Mode { get; set; }

    public IReadOnlyList<FlowiseResourceUpsertRequest> Resources { get; set; } = [];
}

public sealed class FlowiseImportResultDto
{
    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }
}

public sealed class FlowiseExportDto
{
    public string ResourceType { get; set; } = string.Empty;

    public IReadOnlyList<FlowiseResourceDto> Resources { get; set; } = [];

    public DateTime ExportedAt { get; set; }
}

public sealed class FlowiseAccountSettingsDto
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string PreferencesJson { get; set; } = "{}";
}

public sealed class FlowiseLoginActivityDto
{
    public string Id { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseAuditLogDto
{
    public string Id { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public string DetailJson { get; set; } = "{}";

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseRoleDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Description { get; set; }

    public IReadOnlyList<string> Permissions { get; set; } = [];
}

public sealed class FlowiseUserDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public IReadOnlyList<string> WorkspaceIds { get; set; } = [];
}

public sealed class FlowiseSsoConfigDto
{
    public string Id { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string SettingsJson { get; set; } = "{}";
}
