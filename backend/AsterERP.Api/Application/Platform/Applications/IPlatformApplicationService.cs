using AsterERP.Shared;
using AsterERP.Contracts.Platform;

namespace AsterERP.Api.Application.Platform.Applications;

public interface IPlatformApplicationService
{
    Task<GridPageResult<ApplicationListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<ApplicationListItemResponse> CreateAsync(ApplicationUpsertRequest request, CancellationToken cancellationToken = default);

    Task<ApplicationListItemResponse> UpdateAsync(string id, ApplicationUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);
}
