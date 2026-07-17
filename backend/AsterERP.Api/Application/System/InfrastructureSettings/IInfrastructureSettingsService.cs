using AsterERP.Contracts.System.InfrastructureSettings;
using AsterERP.Shared;

namespace AsterERP.Api.Application.System.InfrastructureSettings;

public interface IInfrastructureSettingsService
{
    Task<InfrastructureSettingsResponse> GetAsync(CancellationToken cancellationToken = default);

    Task<InfrastructureSettingsResponse> UpdateAsync(
        InfrastructureSettingsUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<InfrastructureTestResult> TestEmailAsync(
        InfrastructureEmailTestRequest request,
        CancellationToken cancellationToken = default);

    Task<InfrastructureTestResult> TestSmsAsync(
        InfrastructureSmsTestRequest request,
        CancellationToken cancellationToken = default);

    Task<InfrastructureTestResult> TestObjectStorageAsync(
        InfrastructureObjectStorageTestRequest request,
        CancellationToken cancellationToken = default);

    Task<GridPageResult<MessageSendLogResponse>> GetMessageLogsAsync(
        MessageSendLogQuery query,
        CancellationToken cancellationToken = default);
}
