using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

public class PositionSeq : BaseModel
{
    public string Id { get; set; }
    public string Code { get; set; }
    public string Name { get; set; }
    public string Pid { get; set; }
    public int? Status { get; set; }
    public int? OrderNo { get; set; }
    public string Note { get; set; }
}
