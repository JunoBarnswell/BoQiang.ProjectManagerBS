using AsterERP.Api.Application.Common;
using AsterERP.Shared;
using AsterERP.Contracts.System.Parameters;

namespace AsterERP.Api.Application.System.Parameters;

/// <summary>
/// Application service contract for system parameter CRUD operations.
/// Extends <see cref="ICrudAppService{TListDto,TUpsertRequest}"/> without additional members;
/// the interface exists to allow DI registration and easy mocking.
/// </summary>
public interface IParameterService
    : ICrudAppService<ParameterListItemResponse, ParameterUpsertRequest>
{
    Task<GridPageResult<ParameterListItemResponse>> GetPageAsync(
        GridQuery gridQuery,
        string? category,
        CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(
        IReadOnlyList<string> ids,
        string status,
        CancellationToken cancellationToken = default);
}
