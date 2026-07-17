using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime;

public interface IRuntimeGridViewService
{
    Task<RuntimeGridViewResponse> GetAsync(string pageCode, string? previewPageId = null, CancellationToken cancellationToken = default);

    Task<RuntimeGridViewResponse> SaveUserViewAsync(string pageCode, RuntimeGridViewSaveRequest request, CancellationToken cancellationToken = default);

    Task<RuntimeGridViewResponse> SaveTenantDefaultAsync(string pageCode, RuntimeGridViewSaveRequest request, CancellationToken cancellationToken = default);

    Task<RuntimeGridViewResponse> ResetUserViewAsync(string pageCode, CancellationToken cancellationToken = default);
}
