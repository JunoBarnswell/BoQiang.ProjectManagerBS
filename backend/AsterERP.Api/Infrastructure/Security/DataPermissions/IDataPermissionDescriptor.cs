using System.Linq.Expressions;

namespace AsterERP.Api.Infrastructure.Security.DataPermissions;

public interface IDataPermissionDescriptor<TEntity>
{
    Task<Expression<Func<TEntity, bool>>?> BuildAsync(CancellationToken cancellationToken = default);
}
