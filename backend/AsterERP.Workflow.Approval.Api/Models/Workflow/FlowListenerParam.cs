namespace AsterERP.Workflow.Approval.Api.Models.Workflow;

[SqlSugar.SugarTable("tbl_flow_listener_param")]
public class FlowListenerParam
{
    public const string TypeClass = "string";
    public const string TypeExpression = "expression";

    public string? Id { get; set; }
    public string? ListenerId { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public string? Value { get; set; }
}
