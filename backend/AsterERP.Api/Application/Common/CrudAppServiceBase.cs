using System.Linq.Expressions;
using AsterERP.Domain.Common;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.UnitOfWork;

namespace AsterERP.Api.Application.Common;

/// <summary>
/// Generic base class that wires <see cref="IRepository{TEntity}"/> to the standard CRUD
/// workflow (page, create, update, delete).  Subclasses provide entity-specific hooks:
/// <see cref="BuildKeywordPredicate"/>, <see cref="MapToListItem"/>, <see cref="ApplyToEntity"/>,
/// and optionally <see cref="ValidateAsync"/>.
/// </summary>
/// <typeparam name="TEntity">Entity type — must inherit <see cref="EntityBase"/>.</typeparam>
/// <typeparam name="TListDto">DTO returned from list / create / update operations.</typeparam>
/// <typeparam name="TUpsertRequest">Shared request DTO used by both create and update.</typeparam>
public abstract class CrudAppServiceBase<TEntity, TListDto, TUpsertRequest>
    : ICrudAppService<TListDto, TUpsertRequest>
    where TEntity : EntityBase, new()
{
    /// <summary>The underlying repository; accessible to subclasses for extra queries.</summary>
    protected readonly IRepository<TEntity> Repository;

    /// <summary>Unit-of-work used to wrap write operations in a transaction when needed.</summary>
    protected readonly IUnitOfWork UnitOfWork;

    protected CrudAppServiceBase(IRepository<TEntity> repository, IUnitOfWork unitOfWork)
    {
        Repository = repository;
        UnitOfWork = unitOfWork;
    }

    // ──────────────────────────── Public CRUD ────────────────────────────

    /// <inheritdoc/>
    public async Task<GridPageResult<TListDto>> GetPageAsync(
        GridQuery gridQuery,
        CancellationToken cancellationToken = default)
    {
        var predicate = BuildKeywordPredicate(gridQuery.Keyword?.Trim());
        var page = await Repository.GridPageAsync(gridQuery, predicate, cancellationToken: cancellationToken);

        return new GridPageResult<TListDto>
        {
            Total = page.Total,
            Summary = page.Summary,
            Items = page.Items.Select(MapToListItem).ToList()
        };
    }

    /// <inheritdoc/>
    public async Task<TListDto> CreateAsync(
        TUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, null, cancellationToken);
        var entity = new TEntity();
        ApplyToEntity(entity, request);
        await Repository.InsertAsync(entity, cancellationToken);
        return MapToListItem(entity);
    }

    /// <inheritdoc/>
    public async Task<TListDto> UpdateAsync(
        string id,
        TUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await RequireEntityAsync(id, cancellationToken);
        await ValidateAsync(request, id, cancellationToken);
        ApplyToEntity(entity, request);
        await Repository.UpdateAsync(entity, cancellationToken);
        return MapToListItem(entity);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await RequireEntityAsync(id, cancellationToken);
        await Repository.DeleteAsync(id, cancellationToken);
    }

    // ──────────────────────────── Abstract Hooks ────────────────────────────

    /// <summary>
    /// Builds a keyword search predicate applied alongside <see cref="GridQuery.Filters"/>.
    /// Return <c>null</c> when the keyword is empty or not applicable.
    /// </summary>
    protected abstract Expression<Func<TEntity, bool>>? BuildKeywordPredicate(string? keyword);

    /// <summary>Projects an entity to the list DTO.</summary>
    protected abstract TListDto MapToListItem(TEntity entity);

    /// <summary>
    /// Applies the upsert request fields onto the entity.
    /// Called after <see cref="ValidateAsync"/> for both create and update paths.
    /// </summary>
    protected abstract void ApplyToEntity(TEntity entity, TUpsertRequest request);

    // ──────────────────────────── Virtual Hooks ────────────────────────────

    /// <summary>
    /// Performs validation before create / update.
    /// <paramref name="existingId"/> is <c>null</c> for create, non-null for update.
    /// Throw <see cref="ValidationException"/> or <see cref="BusinessException"/> on failure.
    /// </summary>
    protected virtual Task ValidateAsync(
        TUpsertRequest request,
        string? existingId,
        CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Human-readable "not found" message used in <see cref="NotFoundException"/>.</summary>
    protected virtual string GetNotFoundMessage() => "Record not found";

    /// <summary>Error code used in <see cref="NotFoundException"/>.</summary>
    protected virtual int GetNotFoundErrorCode() => ErrorCodes.InternalError;

    // ──────────────────────────── Private Helpers ────────────────────────────

    private async Task<TEntity> RequireEntityAsync(string id, CancellationToken cancellationToken)
    {
        var entity = await Repository.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted, cancellationToken);
        return entity ?? throw new NotFoundException(GetNotFoundMessage(), GetNotFoundErrorCode());
    }
}
