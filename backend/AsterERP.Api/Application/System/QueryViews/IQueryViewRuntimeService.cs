using AsterERP.Contracts.System.QueryViews;

namespace AsterERP.Api.Application.System.QueryViews;

public interface IQueryViewRuntimeService
{
    Task<QueryViewRuntimeDefinitionResponse> GetDefinitionAsync(string viewCode, CancellationToken cancellationToken = default);

    Task<QueryViewQueryResponse> QueryAsync(string viewCode, QueryViewQueryRequest request, CancellationToken cancellationToken = default);
}
