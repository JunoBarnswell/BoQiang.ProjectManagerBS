using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.Runtime;

public interface IRuntimePageSchemaService
{
    Task<RuntimePageSchemaResponse> GetPublishedPageAsync(
        string pageCode,
        string? previewPageId = null,
        CancellationToken cancellationToken = default);
}
