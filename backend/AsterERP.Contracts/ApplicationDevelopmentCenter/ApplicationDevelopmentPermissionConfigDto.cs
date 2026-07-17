namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPermissionConfigDto
{
    public bool AllowAdd { get; set; } = true;

    public bool AllowDelete { get; set; } = true;

    public bool AllowEdit { get; set; } = true;

    public bool AllowExport { get; set; } = true;

    public bool AllowImport { get; set; } = true;

    public string? MenuCode { get; set; }

    public string? MenuName { get; set; }

    public string? ParentMenuCode { get; set; }

    public List<string> RoleCodes { get; set; } = [];
}
