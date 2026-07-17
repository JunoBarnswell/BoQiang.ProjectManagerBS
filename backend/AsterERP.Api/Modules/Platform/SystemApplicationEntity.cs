using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Platform;

[SugarTable("system_applications")]
public sealed class SystemApplicationEntity : EntityBase
{
    public string AppCode { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string AppType { get; set; } = "Business";

    [SugarColumn(IsNullable = true)]
    public string? Icon { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DefaultRoutePath { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? AdminDefaultRoutePath { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RuntimeDefaultRoutePath { get; set; }

    public string Status { get; set; } = "Enabled";

    [SugarColumn(IsNullable = true)]
    public string? Version { get; set; }
}
