using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementSyncJournalWriter(IWorkspaceDatabaseAccessor databaseAccessor) : IProjectManagementSyncJournalWriter
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    public async Task AppendAsync(ProjectManagementSyncJournalEvent entry, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            var db = databaseAccessor.GetProjectManagementDb();
            var current = await db.Queryable<ProjectManagementSyncJournalEntity>()
                .Where(item => item.TenantId == entry.TenantId && item.AppCode == entry.AppCode)
                .OrderBy(item => item.SequenceNo, OrderByType.Desc)
                .Select(item => item.SequenceNo)
                .Take(1)
                .ToListAsync(cancellationToken);
            var nextSequence = (current.FirstOrDefault()) + 1;
            await db.Insertable(new ProjectManagementSyncJournalEntity
            {
                TenantId = entry.TenantId,
                AppCode = entry.AppCode,
                SequenceNo = nextSequence,
                AggregateType = entry.AggregateType,
                AggregateId = entry.AggregateId,
                ProjectId = entry.ProjectId,
                Operation = entry.Operation,
                VersionNo = entry.VersionNo,
                PayloadJson = entry.PayloadJson,
                ActorUserId = entry.ActorUserId,
                DeviceId = entry.DeviceId,
                TraceId = entry.TraceId,
                CreatedBy = entry.ActorUserId,
                CreatedTime = DateTime.UtcNow
            }).ExecuteCommandAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);
}
