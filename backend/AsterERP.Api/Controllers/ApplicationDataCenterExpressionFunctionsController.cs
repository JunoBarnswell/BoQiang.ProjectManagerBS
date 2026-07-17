using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/expression-functions")]
public sealed class ApplicationDataCenterExpressionFunctionsController(RuntimeExpressionFunctionCatalog catalog) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterMicroflowView)]
    public IActionResult GetCatalog([FromQuery] string? scope) => ApiOk(catalog.GetCatalog(scope));
}
