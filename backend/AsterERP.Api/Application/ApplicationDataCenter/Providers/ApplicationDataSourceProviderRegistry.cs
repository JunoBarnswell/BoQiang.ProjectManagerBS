using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed class ApplicationDataSourceProviderRegistry(IEnumerable<IApplicationDataSourceProvider> providers)
{
    private readonly IReadOnlyDictionary<string, IApplicationDataSourceProvider> providers =
        providers.ToDictionary(item => item.Type, StringComparer.OrdinalIgnoreCase);

    public IApplicationDataSourceProvider Resolve(string type)
    {
        if (string.Equals(type, ApplicationDataSourceType.ApplicationDatabase, StringComparison.OrdinalIgnoreCase))
        {
            type = ApplicationDataSourceType.Sqlite;
        }

        return providers.TryGetValue(type, out var provider)
            ? provider
            : throw new ValidationException("不支持的数据源 provider", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    public IReadOnlyList<ApplicationDataSourceProviderCapability> GetCapabilities() => providers.Values.Select(item => item.Capability).ToArray();
}
