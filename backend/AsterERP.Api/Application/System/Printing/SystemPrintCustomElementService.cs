using AsterERP.Contracts.System.Printing;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.System.Printing;
using SqlSugar;

namespace AsterERP.Api.Application.System.Printing;

public sealed class SystemPrintCustomElementService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    PrintWorkspaceResolver workspaceResolver)
{
    public async Task<IReadOnlyList<PrintCustomElementListItemResponse>> GetListAsync(CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var items = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintCustomElementEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        return items
            .Select(item => new PrintCustomElementListItemResponse(
                item.Id,
                item.Name,
                PrintJsonNodeMapper.ToUnixMilliseconds(item.CreatedTime, item.UpdatedTime),
                PrintJsonNodeMapper.Deserialize(item.ExtJson),
                PrintJsonNodeMapper.Deserialize(item.PermissionsJson)))
            .ToList();
    }

    public async Task<PrintCustomElementDetailResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await GetRequiredAsync(id, cancellationToken);
        return new PrintCustomElementDetailResponse(
            item.Id,
            item.Name,
            PrintJsonNodeMapper.Deserialize(item.ElementJson),
            PrintJsonNodeMapper.ToUnixMilliseconds(item.CreatedTime, item.UpdatedTime),
            PrintJsonNodeMapper.Deserialize(item.ExtJson),
            PrintJsonNodeMapper.Deserialize(item.PermissionsJson));
    }

    public async Task<PrintCustomElementDetailResponse> UpsertAsync(
        PrintCustomElementUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var normalizedName = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new InvalidOperationException("自定义元素名称不能为空。");
        }

        var now = DateTime.UtcNow;
        var entity = string.IsNullOrWhiteSpace(request.Id)
            ? null
            : await databaseAccessor.GetCurrentDb().Queryable<SystemPrintCustomElementEntity>()
                .Where(item =>
                    item.Id == request.Id &&
                    !item.IsDeleted &&
                    item.TenantId == scope.TenantId &&
                    item.AppCode == scope.AppCode)
                .FirstAsync(cancellationToken);

        if (entity is null)
        {
            entity = new SystemPrintCustomElementEntity
            {
                TenantId = scope.TenantId,
                AppCode = scope.AppCode,
                Name = normalizedName,
                ElementJson = PrintJsonNodeMapper.Serialize(request.Element),
                ExtJson = PrintJsonNodeMapper.Serialize(request.Ext),
                PermissionsJson = PrintJsonNodeMapper.Serialize(request.Permissions),
                CreatedBy = scope.UserId,
                UpdatedBy = scope.UserId,
                UpdatedTime = now
            };
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity.Name = normalizedName;
            entity.ElementJson = PrintJsonNodeMapper.Serialize(request.Element);
            entity.ExtJson = PrintJsonNodeMapper.Serialize(request.Ext);
            entity.PermissionsJson = PrintJsonNodeMapper.Serialize(request.Permissions);
            entity.UpdatedBy = scope.UserId;
            entity.UpdatedTime = now;
            await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        return await GetByIdAsync(entity.Id, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var entity = await GetRequiredAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedBy = scope.UserId;
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = scope.UserId;
        entity.UpdatedTime = entity.DeletedTime;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<SystemPrintCustomElementEntity> GetRequiredAsync(string id, CancellationToken cancellationToken)
    {
        var scope = workspaceResolver.GetRequiredCurrent();
        var entity = await databaseAccessor.GetCurrentDb().Queryable<SystemPrintCustomElementEntity>()
            .Where(item =>
                item.Id == id &&
                !item.IsDeleted &&
                item.TenantId == scope.TenantId &&
                item.AppCode == scope.AppCode)
            .FirstAsync(cancellationToken);

        return entity ?? throw new KeyNotFoundException($"未找到自定义元素：{id}");
    }
}

