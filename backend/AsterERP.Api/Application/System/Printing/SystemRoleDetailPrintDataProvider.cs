using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.System.Roles;

namespace AsterERP.Api.Application.System.Printing;

public sealed class SystemRoleDetailPrintDataProvider(ISystemRoleService systemRoleService) : IPrintDataProvider
{
    public string Key => "system.role.detail";

    public async Task<JsonObject> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var detail = await systemRoleService.GetDetailAsync(id, cancellationToken);
        return JsonSerializer.SerializeToNode(detail)?.AsObject() ?? new JsonObject();
    }
}
