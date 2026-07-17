using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterSqlScriptExecutionRequest
{
    public string? ContextDataSourceId { get; init; }

    public RuntimeExpressionEvaluationContext ExpressionContext { get; init; } = new(new Dictionary<string, object?>());

    public int? PageIndex { get; init; }

    public int? PageSize { get; init; }

    public ApplicationMicroflowSqlScriptDefinition SqlScript { get; init; } = new();

    public string? SourceId { get; init; }

    public string SourceKind { get; init; } = "SqlScript";

    public string? SourceName { get; init; }

    public string? TraceId { get; init; }
}
