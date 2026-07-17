using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Dicts;

[SugarTable("system_dict_items")]
public sealed class SystemDictItemEntity : EntityBase
{
    public string DictTypeId { get; set; } = string.Empty;

    public string ItemLabel { get; set; } = string.Empty;

    public string ItemValue { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; } = true;
}
