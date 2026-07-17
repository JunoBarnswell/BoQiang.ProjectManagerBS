namespace AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

public class FlowNodeVo
{
    public string NodeId { get; set; }
    public string NodeName { get; set; }
    public string UserCode { get; set; }
    public string UserName { get; set; }
    public DateTime? EndTime { get; set; }

    public FlowNodeVo() { }

    public FlowNodeVo(string nodeId, string nodeName, string userCode, DateTime? endTime)
    {
        NodeId = nodeId;
        NodeName = nodeName;
        UserCode = userCode;
        EndTime = endTime;
    }
}
