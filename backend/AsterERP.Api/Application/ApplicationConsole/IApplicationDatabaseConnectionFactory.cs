using SqlSugar;

namespace AsterERP.Api.Application.ApplicationConsole;

public interface IApplicationDatabaseConnectionFactory
{
    ISqlSugarClient Create(ApplicationDatabaseBindingOptions options);

    Task ValidateAsync(ApplicationDatabaseBindingOptions options, CancellationToken cancellationToken = default);
}
