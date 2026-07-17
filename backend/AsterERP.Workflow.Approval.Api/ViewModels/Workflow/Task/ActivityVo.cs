namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

public class ActivityVo
{
    public string Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Documentation { get; set; }
    public string Description { get; set; }
    public string Name { get; set; }
    public string Approver { get; set; }
    public string Type { get; set; }
    public string NodeType { get; set; }
    public string Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Duration { get; set; }
    public string ApproverNo { get; set; }
    public string ProceInsId { get; set; }
    public string ProceDefId { get; set; }
    public string TaskDefKey { get; set; }
}
