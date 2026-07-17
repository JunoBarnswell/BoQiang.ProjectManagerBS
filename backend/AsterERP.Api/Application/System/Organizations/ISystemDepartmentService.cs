using AsterERP.Shared;
using AsterERP.Contracts.System.Organizations;

namespace AsterERP.Api.Application.System.Organizations;

public interface ISystemDepartmentService
{
    Task<GridPageResult<DepartmentListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<DepartmentListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DepartmentTreeNodeResponse>> GetTreeAsync(CancellationToken cancellationToken = default);

    Task<DepartmentListItemResponse> CreateAsync(DepartmentUpsertRequest request, CancellationToken cancellationToken = default);

    Task<DepartmentListItemResponse> UpdateAsync(string id, DepartmentUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);
}
