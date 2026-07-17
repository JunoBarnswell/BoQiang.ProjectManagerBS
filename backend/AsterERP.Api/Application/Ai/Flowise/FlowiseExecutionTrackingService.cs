using System.Text.Json;
using AsterERP.Api.Modules.Ai.Flowise;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseExecutionTrackingService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext) : IFlowiseExecutionTrackingService
{
    public async Task<FlowiseScheduleTriggerLogEntity?> CreateScheduleTriggerLogAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseExecutionEntity execution,
        string inputJson,
        CancellationToken cancellationToken)
    {
        if (!IsScheduleExecution(inputJson))
        {
            return null;
        }

        var scheduleRecord = await db.Queryable<FlowiseScheduleRecordEntity>()
            .FirstAsync(
                item => !item.IsDeleted
                    && item.TriggerType == "AGENTFLOW"
                    && item.TargetId == chatflow.Id
                    && item.WorkspaceId == chatflow.WorkspaceId,
                cancellationToken);
        if (scheduleRecord is null)
        {
            return null;
        }

        var log = new FlowiseScheduleTriggerLogEntity
        {
            TenantId = execution.TenantId,
            AppCode = execution.AppCode,
            OwnerUserId = execution.OwnerUserId,
            WorkspaceId = execution.WorkspaceId,
            ScheduleRecordId = scheduleRecord.Id,
            TriggerType = scheduleRecord.TriggerType,
            TargetId = chatflow.Id,
            ExecutionId = execution.Id,
            Status = "RUNNING",
            ScheduledAt = ReadScheduledAt(inputJson) ?? DateTime.UtcNow
        };
        await db.Insertable(log).ExecuteCommandAsync(cancellationToken);
        return log;
    }

    public async Task CompleteScheduleTriggerLogAsync(
        FlowiseScheduleTriggerLogEntity? log,
        FlowiseExecutionEntity execution,
        CancellationToken cancellationToken)
    {
        if (log is null)
        {
            return;
        }

        log.ExecutionId = execution.Id;
        log.Status = execution.Status switch
        {
            "Completed" => "SUCCEEDED",
            "Cancelled" => "SKIPPED",
            _ => "FAILED"
        };
        log.Error = string.IsNullOrWhiteSpace(execution.ErrorMessage) ? null : execution.ErrorMessage;
        log.ElapsedTimeMs = execution.DurationMs;
        log.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(log).ExecuteCommandAsync(cancellationToken);

        if (log.Status != "SUCCEEDED")
        {
            return;
        }

        var record = await db.Queryable<FlowiseScheduleRecordEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == log.ScheduleRecordId, cancellationToken);
        if (record is null)
        {
            return;
        }

        record.LastRunAt = execution.StartedAt ?? log.ScheduledAt;
        record.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(record).ExecuteCommandAsync(cancellationToken);
    }

    public async Task WriteExecutionAuditAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseExecutionEntity execution,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        await db.Insertable(new FlowiseAuditLogEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            EventType = "execution.completed",
            ResourceType = chatflow.Type,
            ResourceId = chatflow.Id,
            DetailJson = JsonSerializer.Serialize(new { execution.TraceId, execution.Status, execution.ErrorCode })
        }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task WriteMcpExecutionAuditAsync(
        FlowiseChatFlowEntity chatflow,
        FlowiseExecutionEntity execution,
        CancellationToken cancellationToken)
    {
        await db.Insertable(new FlowiseAuditLogEntity
        {
            TenantId = chatflow.TenantId,
            AppCode = chatflow.AppCode,
            OwnerUserId = chatflow.OwnerUserId,
            WorkspaceId = chatflow.WorkspaceId,
            EventType = "mcp.execution.completed",
            ResourceType = chatflow.Type,
            ResourceId = chatflow.Id,
            DetailJson = JsonSerializer.Serialize(new { execution.TraceId, execution.Status, execution.ErrorCode })
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static bool IsScheduleExecution(string inputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(inputJson);
            return ReadString(document.RootElement, "chatType")?.Equals("schedule", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DateTime? ReadScheduledAt(string inputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(inputJson);
            var value = ReadString(document.RootElement, "scheduledAt");
            return DateTime.TryParse(value, out var scheduledAt) ? scheduledAt.ToUniversalTime() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
