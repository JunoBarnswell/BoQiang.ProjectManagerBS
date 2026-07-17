namespace AsterERP.Workflow.Approval.Core.Expressions;

public interface IExpressionService
{
    string? GetStrValue(string processInstanceId, string exp);
    object? GetValue(string processInstanceId, string exp);
    T? GetValue<T>(string processInstanceId, string exp);
    Dictionary<string, string> GetStrValues(string processInstanceId, List<string> exps);
    bool GetBooleanValue(Dictionary<string, object> parameters, string exp, bool flag);
    string? GetStrValue(Dictionary<string, object> parameters, string exp);
    T? GetValue<T>(Dictionary<string, object> parameters, string exp);
}
