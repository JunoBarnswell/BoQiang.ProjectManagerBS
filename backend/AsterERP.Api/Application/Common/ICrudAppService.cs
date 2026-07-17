using AsterERP.Shared;

namespace AsterERP.Api.Application.Common;

/// <summary>
/// Defines the standard CRUD contract for a single-entity application service.
/// Consumed by <see cref="CrudEndpointExtensions"/> to register uniform Minimal API routes.
/// </summary>
/// <typeparam name="TListDto">DTO returned in list/detail responses.</typeparam>
/// <typeparam name="TUpsertRequest">Request DTO used for both create and update.</typeparam>
public interface ICrudAppService<TListDto, TUpsertRequest>
{
    /// <summary>Returns a paged, filtered, sorted result set.</summary>
    Task<GridPageResult<TListDto>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    /// <summary>Creates a new entity and returns the mapped DTO.</summary>
    Task<TListDto> CreateAsync(TUpsertRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing entity and returns the mapped DTO.</summary>
    Task<TListDto> UpdateAsync(string id, TUpsertRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes the entity with the given identifier.</summary>
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
