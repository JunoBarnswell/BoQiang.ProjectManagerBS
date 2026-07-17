using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Organizations;

[SugarTable("system_departments")]
public sealed class SystemDepartmentEntity : EntityBase
{
    public string DeptCode { get; set; } = string.Empty;

    public string DeptName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ParentId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ManagerName { get; set; }

    [SugarColumn(IsNullable = true, Length = 1000)]
    public string? LeaderUserIdsJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PhoneNumber { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = "Enabled";
}
