using AsterERP.Shared;

namespace AsterERP.Contracts.System.InfrastructureSettings;

public sealed class MessageSendLogQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? Channel { get; set; }

    public string? Provider { get; set; }

    public string? Result { get; set; }

    public string? TraceId { get; set; }

    public List<GridFilter> Filters { get; set; } = [];

    public List<GridSort> Sorts { get; set; } = [];
}
