using System.Security.Cryptography;
using System.Text;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowRevisionService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver)
{
    public async Task<ApplicationMicroflowRevisionEntity> CreateForCurrentAsync(
        ApplicationMicroflowEntity microflow,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var revisionNo = await GetNextRevisionNoAsync(db, microflow.Id, cancellationToken);
        var revision = new ApplicationMicroflowRevisionEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            MicroflowId = microflow.Id,
            RevisionNo = revisionNo,
            Status = "Draft",
            ConfigJson = microflow.ConfigJson,
            ContentHash = ComputeHash(microflow.ConfigJson),
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow,
            IsDeleted = false
        };
        await db.Insertable(revision).ExecuteCommandAsync(cancellationToken);
        return revision;
    }

    public async Task<IReadOnlyList<ApplicationMicroflowRevisionResponse>> ListAsync(
        ApplicationMicroflowEntity microflow,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var revisions = await db.Queryable<ApplicationMicroflowRevisionEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MicroflowId == microflow.Id && !item.IsDeleted)
            .OrderByDescending(item => item.RevisionNo)
            .ToListAsync(cancellationToken);
        if (revisions.Count == 0)
        {
            await CreateForCurrentAsync(microflow, cancellationToken);
            return await ListAsync(microflow, cancellationToken);
        }

        var currentRevisionId = revisions.FirstOrDefault(item => string.Equals(item.ContentHash, ComputeHash(microflow.ConfigJson), StringComparison.Ordinal))?.Id;
        return revisions.Select(item => new ApplicationMicroflowRevisionResponse(
            item.Id,
            item.RevisionNo,
            item.Status,
            item.ConfigJson,
            item.ValidationStatus,
            item.ValidationMessage,
            item.ValidatedAt,
            item.CreatedTime,
            item.PublishedAt,
            string.Equals(item.Id, currentRevisionId, StringComparison.Ordinal))).ToArray();
    }

    public async Task<ApplicationMicroflowRevisionEntity> RequireCurrentAsync(
        ApplicationMicroflowEntity microflow,
        string revisionId,
        CancellationToken cancellationToken)
    {
        var revision = await RequireAsync(microflow.Id, revisionId, cancellationToken);
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var latestRevisionId = await db.Queryable<ApplicationMicroflowRevisionEntity>()
            .Where(item => item.MicroflowId == microflow.Id && !item.IsDeleted)
            .OrderByDescending(item => item.RevisionNo)
            .Select(item => item.Id)
            .FirstAsync(cancellationToken);
        if (!string.Equals(revision.Id, latestRevisionId, StringComparison.Ordinal)
            || !string.Equals(revision.ContentHash, ComputeHash(microflow.ConfigJson), StringComparison.Ordinal))
        {
            throw new ValidationException("当前版本不是最新已保存草稿，请先保存或重新选择最新版本后再校验或发布。", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return revision;
    }

    public async Task<ApplicationMicroflowRevisionEntity> RequireAsync(string microflowId, string revisionId, CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var revision = await db.Queryable<ApplicationMicroflowRevisionEntity>()
            .Where(item => item.TenantId == workspace.TenantId && item.AppCode == workspace.AppCode && item.MicroflowId == microflowId && item.Id == revisionId && !item.IsDeleted)
            .FirstAsync(cancellationToken);
        return revision ?? throw new NotFoundException("微流版本不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    public async Task<ApplicationMicroflowRevisionResponse> RecordValidationAsync(
        ApplicationMicroflowRevisionEntity revision,
        bool success,
        string message,
        CancellationToken cancellationToken)
    {
        revision.ValidationStatus = success ? "Passed" : "Failed";
        revision.ValidationMessage = message;
        revision.ValidatedAt = DateTime.UtcNow;
        revision.UpdatedTime = DateTime.UtcNow;
        await (await databaseAccessor.RequireApplicationDbAsync(cancellationToken)).Updateable(revision).ExecuteCommandAsync(cancellationToken);
        return new ApplicationMicroflowRevisionResponse(revision.Id, revision.RevisionNo, revision.Status, revision.ConfigJson, revision.ValidationStatus, revision.ValidationMessage, revision.ValidatedAt, revision.CreatedTime, revision.PublishedAt, true);
    }

    public async Task MarkPublishedAsync(ApplicationMicroflowRevisionEntity revision, string snapshotId, CancellationToken cancellationToken)
    {
        revision.Status = "Published";
        revision.PublishedSnapshotId = snapshotId;
        revision.PublishedAt = DateTime.UtcNow;
        revision.UpdatedTime = DateTime.UtcNow;
        await (await databaseAccessor.RequireApplicationDbAsync(cancellationToken)).Updateable(revision).ExecuteCommandAsync(cancellationToken);
    }

    public static string ComputeHash(string configJson) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configJson)));

    private static async Task<int> GetNextRevisionNoAsync(ISqlSugarClient db, string microflowId, CancellationToken cancellationToken)
    {
        var latest = await db.Queryable<ApplicationMicroflowRevisionEntity>()
            .Where(item => item.MicroflowId == microflowId && !item.IsDeleted)
            .OrderByDescending(item => item.RevisionNo)
            .Select(item => (int?)item.RevisionNo)
            .FirstAsync(cancellationToken);
        return (latest ?? 0) + 1;
    }
}
