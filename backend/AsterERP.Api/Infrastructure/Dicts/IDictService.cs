using AsterERP.Shared;

namespace AsterERP.Api.Infrastructure.Dicts;

public interface IDictService
{
    Task<IReadOnlyList<OptionItem>> GetOptionsAsync(string dictCode, CancellationToken cancellationToken = default);
}
