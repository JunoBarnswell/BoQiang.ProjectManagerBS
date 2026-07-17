using System.Numerics;
using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_privilege_acl")]
public class Acl : BaseModel
{
    public const int AclYes = 1;
    public const int AclNo = 0;
    public const int AclNeutral = -1;

    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? ReleaseId { get; set; }

    public string? ModuleId { get; set; }

    public string? ModuleSn { get; set; }

    public string? AclState { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<AppPrivilegeValue>? Values { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public string? UserNo { get; set; }

    public void SetPermission(int permission, bool yes)
    {
        var aclState = ParseBigInteger(AclState) ?? BigInteger.Zero;
        var temp = BigInteger.One << permission;
        aclState = yes ? aclState | temp : aclState ^ temp;
        AclState = aclState.ToString();
    }

    public int GetPermission(int permission)
    {
        var aclState = ParseBigInteger(AclState) ?? BigInteger.Zero;
        var temp = BigInteger.One << permission;
        temp &= aclState;
        if (temp != BigInteger.Zero) return AclYes;
        return AclNo;
    }

    public void SetAclState(BigInteger? aclState)
    {
        AclState = aclState?.ToString();
    }

    public BigInteger GetAclStateValue()
    {
        return ParseBigInteger(AclState) ?? BigInteger.Zero;
    }

    private static BigInteger? ParseBigInteger(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return BigInteger.TryParse(value, out var parsed) ? parsed : null;
    }
}
