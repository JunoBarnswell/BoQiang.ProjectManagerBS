using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Dicts;

[SugarTable("system_dict_types")]
public sealed class SystemDictTypeEntity : EntityBase
{
    public string DictName { get; set; } = string.Empty;

    public string DictCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
