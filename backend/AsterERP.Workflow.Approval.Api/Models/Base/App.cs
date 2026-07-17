using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Base;

[SqlSugar.SugarTable("tbl_base_app")]
public class App : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Sn { get; set; }

    public string? SecretKey { get; set; }

    public int? Status { get; set; }

    public string? Url { get; set; }

    public string? IndexUrl { get; set; }

    public string? Image { get; set; }

    public string? Note { get; set; }

    public int? OrderNo { get; set; }

    public int PlatformEnabled { get; set; } = 0;
}
