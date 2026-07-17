using AsterERP.Shared;

namespace AsterERP.Contracts.Logs;

public sealed class OperationLogQueryRequest
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public string? User { get; set; }

    public string? ModuleName { get; set; }

    public string? RequestPath { get; set; }

    public string? RequestMethod { get; set; }

    public bool? IsSuccess { get; set; }

    public string? TraceId { get; set; }

    public List<GridFilter> Filters { get; set; } = [];

    public List<GridSort> Sorts { get; set; } = [];
}
