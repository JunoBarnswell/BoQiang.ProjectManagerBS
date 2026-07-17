namespace AsterERP.Contracts.Ai;

public sealed class AiWorkbenchOverviewDto
{
    public int TodayConversationCount { get; set; }

    public int ActiveConversationCount { get; set; }

    public int TodayRunCount { get; set; }

    public decimal TodaySuccessRate { get; set; }

    public int TodayTotalTokens { get; set; }

    public int EnabledAgentCount { get; set; }

    public int EnabledModelCount { get; set; }

    public int EnabledToolCount { get; set; }

    public IReadOnlyList<AiConversationDto> RecentConversations { get; set; } = [];
}
