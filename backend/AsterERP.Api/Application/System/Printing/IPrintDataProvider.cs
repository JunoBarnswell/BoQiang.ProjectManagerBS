using System.Text.Json.Nodes;

namespace AsterERP.Api.Application.System.Printing;

public interface IPrintDataProvider
{
    string Key { get; }

    Task<JsonObject> GetDetailAsync(string id, CancellationToken cancellationToken = default);
}
