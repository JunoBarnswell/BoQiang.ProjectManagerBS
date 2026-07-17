using AsterERP.Api.Modules.System.Parameters;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace AsterERP.Api.Infrastructure.Abp.Settings;

public sealed class SystemParameterSettingStore(ISqlSugarClient db) : ISettingStore, ITransientDependency
{
    private const string GlobalProviderName = "G";

    public async Task<string?> GetOrNullAsync(string name, string? providerName, string? providerKey)
    {
        if (!CanReadProvider(providerName, providerKey))
        {
            return null;
        }

        var items = await db.Queryable<SystemParameterEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && item.ParamKey == name)
            .Take(1)
            .ToListAsync();

        return items.FirstOrDefault()?.ParamValue;
    }

    public async Task<List<SettingValue>> GetAllAsync(string[] names, string? providerName, string? providerKey)
    {
        if (!CanReadProvider(providerName, providerKey) || names.Length == 0)
        {
            return [];
        }

        var uniqueNames = names
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = await db.Queryable<SystemParameterEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && uniqueNames.Contains(item.ParamKey))
            .ToListAsync();

        return items
            .Select(item => new SettingValue(item.ParamKey, item.ParamValue))
            .ToList();
    }

    private static bool CanReadProvider(string? providerName, string? providerKey) =>
        string.IsNullOrWhiteSpace(providerKey) &&
        !string.IsNullOrWhiteSpace(providerName) &&
        (providerName.Equals(GlobalProviderName, StringComparison.OrdinalIgnoreCase) ||
         providerName.Equals("Global", StringComparison.OrdinalIgnoreCase));
}
