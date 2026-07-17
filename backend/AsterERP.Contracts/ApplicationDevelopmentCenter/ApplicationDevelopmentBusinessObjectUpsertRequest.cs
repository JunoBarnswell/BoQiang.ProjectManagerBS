namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentBusinessObjectUpsertRequest
{
    public string VersionId { get; set; } = string.Empty;

    public string? ModuleId { get; set; }

    public string? PageId { get; set; }

    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string? MenuCode { get; set; }

    public string? ParentPageId { get; set; }

    public string? DataSourceId { get; set; }

    public string? SourceTable { get; set; }

    public string ProviderKey { get; set; } = "application-data-center.sql-table";

    public string? KeyField { get; set; }

    public string? DocumentJson { get; set; }

    public string PermissionConfigJson { get; set; } = "{}";

    public List<ApplicationDevelopmentBusinessObjectFieldDto> Fields { get; set; } = [];

    public bool CreateWorkflowBinding { get; set; }

    public int SortOrder { get; set; }

    public string? Remark { get; set; }
}
