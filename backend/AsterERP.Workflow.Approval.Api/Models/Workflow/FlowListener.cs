using AsterERP.Workflow.Tools.Common;

namespace AsterERP.Workflow.Approval.Api.Models.Workflow;

[SqlSugar.SugarTable("tbl_flow_listener")]
public class FlowListener : BaseModel
{
    public const string TypeClass = "class";
    public const string TypeExpression = "expression";
    public const string TypeDelegateexpression = "delegateExpression";
    public const string ListenerTypeTask = "taskListener";
    public const string ListenerTypeExecution = "executionListener";

    public string? Id { get; set; }

    public string? Type { get; set; }

    public string? Name { get; set; }

    public string? ListenerType { get; set; }

    public string? Value { get; set; }

    public string? Remark { get; set; }

    public int? OrderNo { get; set; }

    [SqlSugar.SugarColumn(IsIgnore = true)]
    public List<FlowListenerParam>? FlowListenerParamList { get; set; }
}
