using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Org;

public class PositionInfo : BaseModel
{
    public string Id { get; set; }
    public string Code { get; set; }
    public string PositionSeqId { get; set; }
    public string PositionSeqCode { get; set; }
    public string Name { get; set; }
    public int OrderNo { get; set; }
    public int Status { get; set; }
    public string SuperiorCode { get; set; }
    public DateTime? StartDate { get; set; }
    public string SuperiorName { get; set; }
    public string PositionSeqName { get; set; }
    public string CompanyName { get; set; }
    public string DeptName { get; set; }
}
