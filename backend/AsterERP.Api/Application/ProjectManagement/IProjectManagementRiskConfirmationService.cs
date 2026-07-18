namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementRiskConfirmationService
{
    Task EnsureConfirmedAsync(string currentPassword, bool confirmRisk, CancellationToken cancellationToken = default);
}
