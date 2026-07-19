using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementSyncJournalWriter(IWorkspaceDatabaseAccessor databaseAccessor) : IProjectManagementSyncJournalWriter
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwordhash", "token", "accesstoken", "refreshtoken", "secret", "privatekey", "authorization", "cookie"
    };
    private static readonly HashSet<string> BinaryFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "contentbytes", "filebytes", "binary", "base64", "rawcontent", "filecontent", "blob"
    };

    public async Task AppendAsync(ProjectManagementSyncJournalEvent entry, CancellationToken cancellationToken = default)
    {
        Validate(entry);
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            var db = databaseAccessor.GetCurrentDb();
            var payload = BuildPayload(entry);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var current = await db.Queryable<ProjectManagementSyncJournalEntity>()
                    .Where(item => item.TenantId == entry.TenantId && item.AppCode == entry.AppCode)
                    .OrderBy(item => item.SequenceNo, OrderByType.Desc)
                    .Select(item => item.SequenceNo)
                    .Take(1)
                    .ToListAsync(cancellationToken);
                var nextSequence = current.FirstOrDefault() + 1;
                try
                {
                    await db.Insertable(new ProjectManagementSyncJournalEntity
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        TenantId = entry.TenantId.Trim(),
                        AppCode = entry.AppCode.Trim().ToUpperInvariant(),
                        SequenceNo = nextSequence,
                        AggregateType = entry.AggregateType.Trim(),
                        AggregateId = entry.AggregateId.Trim(),
                        ProjectId = Normalize(entry.ProjectId),
                        Operation = entry.Operation.Trim(),
                        VersionNo = entry.VersionNo,
                        PayloadJson = payload,
                        ActorUserId = entry.ActorUserId.Trim(),
                        DeviceId = Normalize(entry.DeviceId),
                        TraceId = string.IsNullOrWhiteSpace(entry.TraceId) ? Guid.NewGuid().ToString("N") : entry.TraceId.Trim(),
                        CreatedBy = entry.ActorUserId.Trim(),
                        CreatedTime = DateTime.UtcNow
                    }).ExecuteCommandAsync(cancellationToken);
                    return;
                }
                catch (Exception) when (attempt < 2)
                {
                    // The unique (TenantId, AppCode, SequenceNo) index arbitrates writers from different processes.
                    // Re-read the watermark and retry instead of losing a committed business change.
                }
            }
            throw new ValidationException("同步变更序列号分配失败，请稍后重试");
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    public static (string Source, IReadOnlyList<ProjectManagementSyncFieldChange> FieldChanges) ReadMetadata(string payloadJson)
    {
        try
        {
            var root = JsonNode.Parse(payloadJson) as JsonObject;
            var metadata = root?["__sync"] as JsonObject;
            var source = metadata?["source"]?.GetValue<string>() ?? "User";
            var changes = metadata?["fieldChanges"]?.Deserialize<List<ProjectManagementSyncFieldChange>>(JsonOptions)
                ?? [];
            return (source, changes);
        }
        catch (JsonException)
        {
            return ("User", []);
        }
    }

    private static string BuildPayload(ProjectManagementSyncJournalEvent entry)
    {
        var sanitized = Sanitize(JsonNode.Parse(entry.PayloadJson) ?? throw new ValidationException("同步 Journal 载荷无效"));
        if (sanitized is not JsonObject root)
            throw new ValidationException("同步 Journal 载荷必须是 JSON 对象");

        var changes = entry.FieldChanges?.Count > 0
            ? entry.FieldChanges
            : Diff(entry.PreviousPayloadJson, sanitized);
        root["__sync"] = JsonSerializer.SerializeToNode(new
        {
            source = string.IsNullOrWhiteSpace(entry.Source) ? "User" : entry.Source.Trim(),
            redacted = true,
            fieldChanges = changes
        }, JsonOptions);
        return root.ToJsonString(JsonOptions);
    }

    private static IReadOnlyList<ProjectManagementSyncFieldChange> Diff(string? previousPayloadJson, JsonNode current)
    {
        if (string.IsNullOrWhiteSpace(previousPayloadJson)) return [];
        var previous = JsonNode.Parse(previousPayloadJson) as JsonObject;
        var currentObject = current as JsonObject;
        if (previous is null || currentObject is null) return [];
        var fields = previous.Select(item => item.Key)
            .Concat(currentObject.Select(item => item.Key))
            .Where(item => !string.Equals(item, "__sync", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .Take(100);
        var changes = new List<ProjectManagementSyncFieldChange>();
        foreach (var field in fields)
        {
            var before = previous[field]?.ToJsonString(JsonOptions);
            var after = currentObject[field]?.ToJsonString(JsonOptions);
            if (!string.Equals(before, after, StringComparison.Ordinal))
                changes.Add(new ProjectManagementSyncFieldChange(field, before, after));
        }
        return changes;
    }

    private static JsonNode Sanitize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (SensitiveFields.Contains(property.Key))
                {
                    obj[property.Key] = "[REDACTED]";
                }
                else if (BinaryFields.Contains(property.Key))
                {
                    obj.Remove(property.Key);
                }
                else if (property.Value is not null)
                {
                    obj[property.Key] = Sanitize(property.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
                if (array[index] is not null) array[index] = Sanitize(array[index]!);
        }
        return node;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void Validate(ProjectManagementSyncJournalEvent entry)
    {
        if (string.IsNullOrWhiteSpace(entry.TenantId)) throw new ValidationException("同步 Journal 租户不能为空");
        if (string.IsNullOrWhiteSpace(entry.AppCode)) throw new ValidationException("同步 Journal 应用不能为空");
        if (string.IsNullOrWhiteSpace(entry.AggregateType)) throw new ValidationException("同步 Journal 实体类型不能为空");
        if (string.IsNullOrWhiteSpace(entry.AggregateId)) throw new ValidationException("同步 Journal 稳定 ID 不能为空");
        if (string.IsNullOrWhiteSpace(entry.Operation)) throw new ValidationException("同步 Journal 操作不能为空");
        if (entry.VersionNo < 0) throw new ValidationException("同步 Journal 版本不能为负数");
        if (entry.TenantId.Length > 120 || entry.AppCode.Length > 120 || entry.AggregateType.Length > 120 || entry.AggregateId.Length > 120)
            throw new ValidationException("同步 Journal 标识长度无效");
    }
}
