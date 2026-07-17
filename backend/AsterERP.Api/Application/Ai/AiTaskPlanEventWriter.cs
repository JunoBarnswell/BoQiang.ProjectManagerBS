using System.Text.Json;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiTaskPlanEventWriter(ISqlSugarClient db, AiWorkspaceContext workspaceContext)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiTaskPlanEventDto> WriteAsync(
        AiTaskPlanEntity plan,
        string eventName,
        AiTaskPlanItemEntity? item = null,
        string? runId = null,
        string? fromStatus = null,
        string? toStatus = null,
        string? summary = null,
        object? payload = null,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var seq = await NextSeqAsync(plan.Id, cancellationToken);
        var entity = new AiTaskPlanEventEntity
        {
            TenantId = plan.TenantId,
            AppCode = plan.AppCode,
            OwnerUserId = plan.OwnerUserId,
            ConversationId = plan.ConversationId,
            PlanId = plan.Id,
            ItemId = item?.Id,
            RunId = runId ?? plan.RunId,
            Seq = seq,
            EventName = eventName,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Summary = summary,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, JsonOptions),
            TraceId = traceId,
            OperatorUserId = workspace.UserId
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync(plan, eventName, item, entity, cancellationToken);
        return AiTaskPlanMapper.MapEvent(entity);
    }

    private async Task<long> NextSeqAsync(string planId, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AiTaskPlanEventEntity>()
            .Where(item => !item.IsDeleted && item.PlanId == planId)
            .OrderBy(item => item.Seq, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken);
        return (rows.FirstOrDefault()?.Seq ?? 0) + 1;
    }

    private async Task WriteAuditAsync(
        AiTaskPlanEntity plan,
        string eventName,
        AiTaskPlanItemEntity? item,
        AiTaskPlanEventEntity eventEntity,
        CancellationToken cancellationToken)
    {
        var audit = new AiAuditEventEntity
        {
            TenantId = plan.TenantId,
            AppCode = plan.AppCode,
            EventType = eventName,
            ResourceType = item is null ? "AiTaskPlan" : "AiTaskPlanItem",
            ResourceId = item?.Id ?? plan.Id,
            UserId = eventEntity.OperatorUserId,
            TraceId = eventEntity.TraceId,
            DetailJson = JsonSerializer.Serialize(new
            {
                planId = plan.Id,
                itemId = item?.Id,
                eventSeq = eventEntity.Seq,
                eventName,
                eventEntity.FromStatus,
                eventEntity.ToStatus,
                eventEntity.Summary
            }, JsonOptions)
        };
        await db.Insertable(audit).ExecuteCommandAsync(cancellationToken);
    }
}
