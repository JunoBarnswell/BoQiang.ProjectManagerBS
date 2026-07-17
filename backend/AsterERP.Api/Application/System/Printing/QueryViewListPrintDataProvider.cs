using AsterERP.Api.Application.System.QueryViews;
using AsterERP.Contracts.System.Printing;
using AsterERP.Contracts.System.QueryViews;
using System.Text.Json.Nodes;

namespace AsterERP.Api.Application.System.Printing;

public sealed class QueryViewListPrintDataProvider(IQueryViewRuntimeService queryViewRuntimeService)
{
    public async Task<(JsonArray Rows, long Total)> QueryAsync(
        string viewCode,
        PrintTemplateResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var mode = string.IsNullOrWhiteSpace(request.Mode) ? "currentPage" : request.Mode.Trim().ToLowerInvariant();
        var pageIndex = mode == "allfiltered" ? 1 : Math.Max(1, request.PageIndex);
        var pageSize = mode == "allfiltered"
            ? Math.Clamp(request.PageSize <= 0 ? 500 : request.PageSize, 1, 500)
            : Math.Clamp(request.PageSize <= 0 ? 20 : request.PageSize, 1, 200);

        var response = await queryViewRuntimeService.QueryAsync(
            viewCode,
            new QueryViewQueryRequest(
                pageIndex,
                pageSize,
                request.Conditions ?? [],
                request.Sorts ?? []),
            cancellationToken);

        var rows = response.Rows;
        if (mode == "selected")
        {
            var selectedIds = request.SelectedIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            rows = rows
                .Where(row => row.TryGetValue("id", out var idValue) && selectedIds.Contains(idValue?.ToString() ?? string.Empty))
                .ToList();
        }

        return (PrintJsonNodeMapper.ToArrayNode(rows), response.Total);
    }
}
