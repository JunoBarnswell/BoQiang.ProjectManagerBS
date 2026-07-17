using System.Text.Json;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationMicroflowDefinitionReader
{
    public static ApplicationMicroflowDefinition Read(string configJson)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<ApplicationMicroflowDefinition>(
                configJson,
                ApplicationDataCenterJson.Options);
            if (definition is null)
            {
                throw new ValidationException("微流定义不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            return definition;
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"微流定义不是合法 JSON: {ex.Message}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }
}
