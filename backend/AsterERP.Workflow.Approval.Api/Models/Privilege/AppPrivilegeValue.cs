using System.Numerics;
using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_privilege_pvalue")]
public class AppPrivilegeValue : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public int? Position { get; set; }

    public string? Name { get; set; }

    public int? OrderNo { get; set; }

    public string? Remark { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public BigInteger? State { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? ModuleId { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public bool Flag { get; set; } = false;

    public AppPrivilegeValue() { }

    public AppPrivilegeValue(int? position, string? name, int? orderNo)
    {
        Position = position;
        Name = name;
        OrderNo = orderNo;
    }
}
