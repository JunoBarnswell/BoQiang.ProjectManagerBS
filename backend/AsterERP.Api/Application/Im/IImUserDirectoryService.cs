using AsterERP.Contracts.Im;

namespace AsterERP.Api.Application.Im;

public interface IImUserDirectoryService
{
    Task<ImDirectoryResponse> GetDirectoryAsync(string? keyword, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImUserSearchItemResponse>> SearchAsync(ImUserSearchQuery query, CancellationToken cancellationToken = default);
}
