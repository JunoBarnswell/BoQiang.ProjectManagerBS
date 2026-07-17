namespace AsterERP.Workflow.Approval.Api.ViewModels.BpmnDesigner.Variable;

public class VariableVo
{
    public string Name { get; set; }
    public string Code { get; set; }
    public string Prefix { get; set; }
    public string Function { get; set; }
    public string Remark { get; set; }

    public VariableVo() { }

    public VariableVo(string prefix, string name, string code)
    {
        Prefix = prefix;
        Name = name;
        Code = code;
    }

    public VariableVo(string prefix, string name, string code, string remark)
    {
        Prefix = prefix;
        Name = name;
        Code = code;
        Remark = remark;
    }
}
