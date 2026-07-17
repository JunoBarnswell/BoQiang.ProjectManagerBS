using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.System.Users;

namespace AsterERP.Api.Application.System.Printing;

public sealed class SystemUserDetailPrintDataProvider(ISystemUserService systemUserService) : IPrintDataProvider
{
    public string Key => "system.user.detail";

    public async Task<JsonObject> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var detail = await systemUserService.GetDetailAsync(id, cancellationToken);
        return JsonSerializer.SerializeToNode(detail)?.AsObject() ?? new JsonObject();
    }
}
