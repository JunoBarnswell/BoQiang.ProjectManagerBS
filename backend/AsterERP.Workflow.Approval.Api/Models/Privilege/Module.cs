using System.Numerics;
using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Privilege;

[SqlSugar.SugarTable("tbl_privilege_module")]
public class Module : BaseModel
{
    [SqlSugar.SugarColumn(IsPrimaryKey = true)]
    public string? Id { get; set; }

    public string? AppId { get; set; }

    public string? Name { get; set; }

    public string? Url { get; set; }

    public string? Sn { get; set; }

    public int? ShowStatus { get; set; }

    public int? Status { get; set; }

    public string? Image { get; set; }

    public int? OrderNo { get; set; }

    public string? Pid { get; set; }

    public string? State { get; set; }

    public string? Component { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<AppPrivilegeValue>? Pvs { get; set; }

    public void SetPermission(int permission, bool yes)
    {
        var state = ParseBigInteger(State) ?? BigInteger.Zero;
        var temp = BigInteger.One << permission;
        state = yes ? state | temp : state ^ temp;
        State = state.ToString();
    }

    public bool GetPermission(int permission)
    {
        var state = ParseBigInteger(State);
        if (state == null) return false;
        var temp = BigInteger.One << permission;
        temp &= state.Value;
        return temp != BigInteger.Zero;
    }

    public void SetState(BigInteger? state)
    {
        State = state?.ToString();
    }

    public BigInteger GetStateValue()
    {
        return ParseBigInteger(State) ?? BigInteger.Zero;
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
