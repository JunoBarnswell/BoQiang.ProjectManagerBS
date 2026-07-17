using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public interface IApplicationMicroflowRuntimeService
{
    Task<ApplicationMicroflowExecuteResponse> ExecuteAsync(
        string flowCode,
        ApplicationMicroflowExecuteRequest request,
        CancellationToken cancellationToken = default);

    Task<ApplicationMicroflowExecuteResponse> ExecuteDefinitionAsync(
        string flowCode,
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowExecuteRequest request,
        CancellationToken cancellationToken = default);
}
