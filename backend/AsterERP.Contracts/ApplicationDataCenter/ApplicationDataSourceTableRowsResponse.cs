namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowsResponse
{
    public IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> Fields { get; set; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; set; } = [];

    public IReadOnlyList<string> PrimaryKeys { get; set; } = [];

    public int Total { get; set; }

    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public bool Editable { get; set; }

    public string? EditDisabledReason { get; set; }

    public bool CanInsert { get; set; }

    public string? InsertDisabledReason { get; set; }

    public string ConcurrencyStrategy { get; set; } = "none";

    public string? ConcurrencyColumn { get; set; }

    public string? ConcurrencyDisabledReason { get; set; }
}
