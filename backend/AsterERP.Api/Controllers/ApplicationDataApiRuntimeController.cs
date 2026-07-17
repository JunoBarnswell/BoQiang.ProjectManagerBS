using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data")]
public sealed class ApplicationDataApiRuntimeController(ApplicationDataApiRuntimeService runtimeService) : BaseApiController
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [Route("{**path}")]
    public async Task<IActionResult> HandleAsync(string path, CancellationToken cancellationToken)
    {
        var body = await ReadBodyAsync(cancellationToken);
        var query = Request.Query.ToDictionary(item => item.Key, item => (string?)item.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var result = await runtimeService.ExecuteAsync(path, Request.Method, query, body, cancellationToken);
        return ApiOk(result);
    }

    private async Task<JsonElement?> ReadBodyAsync(CancellationToken cancellationToken)
    {
        if (Request.ContentLength is null or 0)
        {
            return null;
        }

        using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        return document.RootElement.Clone();
    }
}
