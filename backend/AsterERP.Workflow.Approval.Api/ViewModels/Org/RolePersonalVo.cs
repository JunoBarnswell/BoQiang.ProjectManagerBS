using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.ViewModels.Org;

public class RolePersonalVo : BaseModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string PersonalId { get; set; }
    public string RoleId { get; set; }
    public string RoleName { get; set; }
    public string DeptName { get; set; }
    public string DeptId { get; set; }
    public string CompanyId { get; set; }
    public string CompanyName { get; set; }
    public string DeptIds { get; set; }
    public string DeptNames { get; set; }
}
