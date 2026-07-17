namespace AsterERP.Api.Application.Platform.Applications;

public interface IPlatformApplicationWorkspaceProvisioningService
{
    Task ProvisionCurrentTenantAsync(
        string appCode,
        string appName,
        CancellationToken cancellationToken = default);
}
