using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Api.ViewModels.Pager;

public class ParamVo<T>
{
    public T Entity { get; set; }
    public Query Query { get; set; }
}
