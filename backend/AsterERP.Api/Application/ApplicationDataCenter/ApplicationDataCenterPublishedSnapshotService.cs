using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterPublishedSnapshotService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver)
{
    public async Task<ApplicationDataCenterPublishedSnapshot> CreateAsync(
        ApplicationDataCenterObjectEntity entity,
        IReadOnlyDictionary<string, object?> binding,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var snapshot = new ApplicationDataCenterPublishedSnapshot
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ModuleKey = entity.ModuleKey,
            ObjectId = entity.Id,
            ObjectCode = entity.ObjectCode,
            ObjectType = entity.ObjectType,
            VersionNo = entity.VersionNo,
            ConfigJson = entity.ConfigJson,
            BindingJson = JsonSerializer.Serialize(binding, ApplicationDataCenterJson.Options),
            PublishedAt = DateTime.UtcNow,
            PublishedBy = workspace.UserId,
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Insertable(snapshot).ExecuteCommandAsync(cancellationToken);
        return snapshot;
    }

    public async Task<ApplicationDataCenterPublishedSnapshot> GetLatestAsync(
        string moduleKey,
        string objectId,
        CancellationToken cancellationToken)
    {
        _ = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var snapshot = await db.Queryable<ApplicationDataCenterPublishedSnapshot>()
            .Where(item => item.ModuleKey == moduleKey && item.ObjectId == objectId)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .Take(1)
            .FirstAsync(cancellationToken);
        return snapshot ?? throw new ValidationException("运行时对象尚未发布", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    public static IReadOnlyDictionary<string, object?> ReadBinding(string json) =>
        ApplicationDataCenterJson.DeserializeDictionary(json);
}
