namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public interface IDataPermissionFilterRegistrar
{
    Task<IDataPermissionFilterScope> RegisterAsync(CancellationToken cancellationToken = default);
}
