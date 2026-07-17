namespace AsterERP.Contracts.System.QueryViews;

public sealed record QueryViewTableResourceResponse(
    string Id,
    string TableCode,
    string TableName,
    string TableComment,
    string SchemaName,
    string ModuleCode,
    bool IsEnabled,
    IReadOnlyList<QueryViewColumnResourceResponse> Columns);

public sealed record QueryViewColumnResourceResponse(
    string Id,
    string TableResourceId,
    string ColumnCode,
    string ColumnName,
    string ColumnComment,
    string DataType,
    bool IsPrimaryKey,
    bool IsNullable,
    bool IsEnabled,
    int SortOrder);

public sealed record QueryViewResourceSyncResponse(
    int TableCount,
    int ColumnCount,
    DateTime SyncedTime);

public sealed record QueryViewTableNodeRequest(
    string NodeId,
    string TableResourceId,
    string Alias,
    bool IsMain);

public sealed record QueryViewRelationRequest(
    string LeftNodeId,
    string LeftColumnResourceId,
    string RightNodeId,
    string RightColumnResourceId,
    string JoinType);

public sealed record QueryViewProjectionRequest(
    string ColumnResourceId,
    string FieldCode,
    string DisplayName,
    string FieldAlias,
    string DataType,
    int Width,
    string Align,
    bool IsVisible,
    bool IsQueryable,
    bool IsSortable,
    bool IsExportable,
    bool IsFrozen,
    string? DictType,
    string? MaskRule,
    string? PermissionCode);

public sealed record QueryViewConditionDefinitionRequest(
    string FieldCode,
    string ControlType,
    string Operator,
    bool IsDefault,
    string? DefaultValue);

public sealed record QueryViewSortDefinitionRequest(
    string FieldCode,
    string Direction,
    int SortOrder);

public sealed record QueryViewDesignerSaveRequest(
    string ViewName,
    string ViewCode,
    string ModuleCode,
    string? MenuCode,
    string ViewType,
    bool IsDefault,
    bool IsEnabled,
    int DefaultPageSize,
    int MaxPageSize,
    string? Remark,
    IReadOnlyList<QueryViewTableNodeRequest> Tables,
    IReadOnlyList<QueryViewRelationRequest> Relations,
    IReadOnlyList<QueryViewProjectionRequest> Projections,
    IReadOnlyList<QueryViewConditionDefinitionRequest> Conditions,
    IReadOnlyList<QueryViewSortDefinitionRequest> Sorts);

public sealed record QueryViewDesignerResponse(
    string Id,
    string ViewName,
    string ViewCode,
    string ModuleCode,
    string? MenuCode,
    string ViewType,
    bool IsDefault,
    bool IsEnabled,
    int VersionNo,
    int DefaultPageSize,
    int MaxPageSize,
    string Status,
    string? Remark,
    IReadOnlyList<QueryViewTableNodeRequest> Tables,
    IReadOnlyList<QueryViewRelationRequest> Relations,
    IReadOnlyList<QueryViewProjectionRequest> Projections,
    IReadOnlyList<QueryViewConditionDefinitionRequest> Conditions,
    IReadOnlyList<QueryViewSortDefinitionRequest> Sorts);

public sealed record QueryViewPublishRequest(string? Remark);

public sealed record QueryViewRollbackRequest(int TargetVersion);

public sealed record QueryViewPublishResponse(
    string ViewId,
    string ViewCode,
    int VersionNo,
    string StableViewName,
    string VersionViewName,
    string Status);

public sealed record QueryViewPlanPreviewResponse(string RuntimeMode, string ProviderCode);

public sealed record QueryViewDataPreviewResponse(
    IReadOnlyList<Dictionary<string, object?>> Rows,
    int Count);

public sealed record QueryViewRuntimeDefinitionResponse(
    string ViewCode,
    string ViewName,
    string ViewType,
    int DefaultPageSize,
    int MaxPageSize,
    IReadOnlyList<QueryViewRuntimeFieldResponse> Fields,
    IReadOnlyList<QueryViewConditionDefinitionRequest> Conditions,
    IReadOnlyList<QueryViewSortDefinitionRequest> Sorts);

public sealed record QueryViewRuntimeFieldResponse(
    string FieldCode,
    string DisplayName,
    string DataType,
    int Width,
    string Align,
    bool IsVisible,
    bool IsQueryable,
    bool IsSortable,
    bool IsExportable,
    bool IsFrozen,
    string? DictType,
    string? MaskRule,
    string? PermissionCode);

public sealed record QueryViewQueryCondition(
    string Field,
    string Operator,
    object? Value,
    object? ValueTo);

public sealed record QueryViewQuerySort(string Field, string Direction);

public sealed record QueryViewQueryRequest(
    int PageIndex,
    int PageSize,
    IReadOnlyList<QueryViewQueryCondition> Conditions,
    IReadOnlyList<QueryViewQuerySort> Sorts);

public sealed record QueryViewQueryResponse(
    int PageIndex,
    int PageSize,
    long Total,
    IReadOnlyList<Dictionary<string, object?>> Rows);

public sealed record QueryViewExportRequest(
    string ExportMode,
    string FileType,
    IReadOnlyList<string> Columns,
    IReadOnlyList<string> SelectedRowIds,
    IReadOnlyList<QueryViewQueryCondition> Conditions,
    IReadOnlyList<QueryViewQuerySort> Sorts);

public sealed record QueryViewExportResponse(
    string TaskNo,
    string Status,
    string FileName,
    string ContentType,
    string? Base64Content,
    long TotalCount);

public sealed record QueryViewPublishLogResponse(
    string Id,
    string ViewId,
    int VersionNo,
    string StableViewName,
    string VersionViewName,
    string Action,
    string PublishStatus,
    string? ErrorMessage,
    string? Remark,
    string PublishedBy,
    DateTime PublishedTime);

public sealed record QueryViewExportTaskResponse(
    string Id,
    string TaskNo,
    string ViewCode,
    string ExportName,
    string Status,
    string? FileUrl,
    long TotalCount,
    DateTime CreatedTime,
    DateTime? FinishedTime,
    string? ErrorMessage);
