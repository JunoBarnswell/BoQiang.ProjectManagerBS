using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiWorkbenchService(ISqlSugarClient db, AiKernelFunctionCatalog toolCatalog)
{
    public async Task<AiWorkbenchOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var todayRunQuery = db.Queryable<AiChatRunEntity>()
            .Where(item => !item.IsDeleted && item.CreatedTime >= today && item.CreatedTime < tomorrow);
        var todayRunCount = await todayRunQuery.CountAsync(cancellationToken);
        var todaySuccessCount = await todayRunQuery.Where(item => item.Status == "Succeeded").CountAsync(cancellationToken);

        var recentConversations = await db.Queryable<AiConversationEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.LastMessageAt, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(8)
            .ToListAsync(cancellationToken);

        return new AiWorkbenchOverviewDto
        {
            TodayConversationCount = await db.Queryable<AiConversationEntity>()
                .Where(item => !item.IsDeleted && item.CreatedTime >= today && item.CreatedTime < tomorrow)
                .CountAsync(cancellationToken),
            ActiveConversationCount = await db.Queryable<AiConversationEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Active")
                .CountAsync(cancellationToken),
            TodayRunCount = todayRunCount,
            TodaySuccessRate = todayRunCount == 0 ? 0 : Math.Round(todaySuccessCount * 100m / todayRunCount, 2),
            TodayTotalTokens = await db.Queryable<AiUsageLogEntity>()
                .Where(item => !item.IsDeleted && item.RequestStartedAt >= today && item.RequestStartedAt < tomorrow)
                .SumAsync(item => item.TotalTokens),
            EnabledAgentCount = await db.Queryable<AiAgentProfileEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .CountAsync(cancellationToken),
            EnabledModelCount = await db.Queryable<AiModelConfigEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .CountAsync(cancellationToken),
            EnabledToolCount = toolCatalog.ListDefinitions().Count(item => item.IsEnabled),
            RecentConversations = recentConversations.Select(MapConversation).ToList()
        };
    }

    private static AiConversationDto MapConversation(AiConversationEntity entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        AppCode = entity.AppCode,
        OwnerUserId = entity.OwnerUserId,
        Title = entity.Title,
        Status = entity.Status,
        IsFavorite = entity.IsFavorite,
        Summary = entity.Summary,
        LastRunStatus = entity.LastRunStatus,
        LastMessageAt = entity.LastMessageAt,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };
}
