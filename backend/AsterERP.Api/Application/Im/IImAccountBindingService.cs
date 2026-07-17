using AsterERP.Contracts.Im;

namespace AsterERP.Api.Application.Im;

public interface IImAccountBindingService
{
    Task<ImAccountBindingResponse> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task<ImAccountBindingResponse> EnsureForUserAsync(string tenantId, string userId, CancellationToken cancellationToken = default);
}
