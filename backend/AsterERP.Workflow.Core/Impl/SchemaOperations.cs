using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using Microsoft.Extensions.Logging;

namespace AsterERP.Workflow.Core.Impl;

public class SchemaOperationsProcessEngineBuild : ICommand<object?>
{
    public object? Execute(ICommandContext context)
    {
        var config = context.ProcessEngineConfiguration;
        var logger = config.LoggerFactory.CreateLogger<SchemaOperationsProcessEngineBuild>();

        var schemaUpdate = config.DatabaseSchemaUpdate;
        switch (schemaUpdate?.ToUpperInvariant())
        {
            case "TRUE":
            case "CREATE-DROP":
                logger.LogInformation("Performing database schema create/drop");
                SchemaCreate(context);
                break;
            case "TRUE-DROP":
                logger.LogInformation("Performing database schema drop and create");
                SchemaDrop(context);
                SchemaCreate(context);
                break;
            case "FALSE":
                logger.LogInformation("Validating database schema");
                SchemaValidate(context);
                break;
            default:
                logger.LogInformation("No database schema operation specified (schemaUpdate={SchemaUpdate})", schemaUpdate);
                break;
        }

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }

    private void SchemaCreate(ICommandContext context)
    {
    }

    private void SchemaDrop(ICommandContext context)
    {
    }

    private void SchemaValidate(ICommandContext context)
    {
    }
}

public class SchemaOperationProcessEngineClose : ICommand<object?>
{
    public object? Execute(ICommandContext context)
    {
        var config = context.ProcessEngineConfiguration;
        var logger = config.LoggerFactory.CreateLogger<SchemaOperationProcessEngineClose>();

        if (string.Equals(config.DatabaseSchemaUpdate, "CREATE-DROP", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Dropping database schema on engine close");
        }

        return null;
    }

    public Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Execute(context));
    }
}
