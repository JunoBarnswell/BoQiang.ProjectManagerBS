using AsterERP.Contracts.System.QueryViews;

namespace AsterERP.Api.Application.System.QueryViews;

public interface IQueryViewDesignerService
{
    Task<IReadOnlyList<QueryViewDesignerResponse>> GetListAsync(CancellationToken cancellationToken = default);

    Task<QueryViewDesignerResponse> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<QueryViewDesignerResponse> CreateAsync(QueryViewDesignerSaveRequest request, CancellationToken cancellationToken = default);

    Task<QueryViewDesignerResponse> UpdateAsync(string id, QueryViewDesignerSaveRequest request, CancellationToken cancellationToken = default);

    Task<QueryViewPlanPreviewResponse> PreviewPlanAsync(string id, CancellationToken cancellationToken = default);

    Task<QueryViewDataPreviewResponse> PreviewDataAsync(string id, CancellationToken cancellationToken = default);

    Task<QueryViewPublishResponse> PublishAsync(string id, QueryViewPublishRequest request, CancellationToken cancellationToken = default);

    Task<QueryViewPublishResponse> RollbackAsync(string id, QueryViewRollbackRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QueryViewPublishLogResponse>> GetPublishLogsAsync(string? viewId, CancellationToken cancellationToken = default);
}
