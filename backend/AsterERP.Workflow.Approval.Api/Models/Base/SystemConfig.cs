using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Base;

[SqlSugar.SugarTable("tbl_base_system_config")]
public class SystemConfig : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? ConfigName { get; set; }

    public string? ConfigSn { get; set; }

    public string? ConfigKey { get; set; }

    public string? ConfigValue { get; set; }

    public int ConfigOrder { get; set; }

    public string? Remark { get; set; }

    public byte[]? Image { get; set; }
}
