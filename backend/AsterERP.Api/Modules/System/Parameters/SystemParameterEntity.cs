using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Parameters;

[SugarTable("system_parameters")]
public sealed class SystemParameterEntity : EntityBase
{
    public string ParamName { get; set; } = string.Empty;

    public string ParamKey { get; set; } = string.Empty;

    public string ParamValue { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public bool IsEnabled { get; set; } = true;
}
