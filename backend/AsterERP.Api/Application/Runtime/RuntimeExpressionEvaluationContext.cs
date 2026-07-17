namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeExpressionEvaluationContext
{
    public RuntimeExpressionEvaluationContext(IReadOnlyDictionary<string, object?> sources)
    {
        Sources = sources;
    }

    public IReadOnlyDictionary<string, object?> Sources { get; }
}
