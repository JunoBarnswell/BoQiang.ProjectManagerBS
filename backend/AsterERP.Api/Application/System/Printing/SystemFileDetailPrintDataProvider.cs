using System.Text.Json;
using System.Text.Json.Nodes;
using AsterERP.Api.Application.System.Files;

namespace AsterERP.Api.Application.System.Printing;

public sealed class SystemFileDetailPrintDataProvider(IFileAppService fileAppService) : IPrintDataProvider
{
    public string Key => "system.file.detail";

    public async Task<JsonObject> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        var detail = await fileAppService.GetDetailAsync(id, cancellationToken);
        return JsonSerializer.SerializeToNode(detail)?.AsObject() ?? new JsonObject();
    }
}
