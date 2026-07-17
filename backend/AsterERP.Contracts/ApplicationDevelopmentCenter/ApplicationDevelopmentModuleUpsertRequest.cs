namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentModuleUpsertRequest
{
    public string ModuleCode { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public string? ParentModuleId { get; set; }

    public string? Remark { get; set; }

    public int SortOrder { get; set; }

    public string VersionId { get; set; } = string.Empty;
}
