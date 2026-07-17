using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

public class JobGrade : BaseModel
{
    public string Id { get; set; }
    public string Code { get; set; }
    public string TypeId { get; set; }
    public string TypeCode { get; set; }
    public string Name { get; set; }
    public int OrderNo { get; set; }
}
