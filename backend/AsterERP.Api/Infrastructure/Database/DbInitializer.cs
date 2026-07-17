namespace AsterERP.Api.Infrastructure.Database;

public sealed class DbInitializer(
    DbMigrationService dbMigrationService,
    DbSeedService dbSeedService,
    IConfiguration configuration,
    ILogger<DbInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dataDirectory = Path.Combine(Environment.CurrentDirectory, "data");
        Directory.CreateDirectory(dataDirectory);

        if (configuration.GetValue<bool>("Database:SkipInitialize"))
        {
            logger.LogInformation("Database initialization skipped by Database:SkipInitialize configuration");
            return;
        }

        logger.LogInformation("Initializing database and seed data");

        await dbMigrationService.MigrateAsync(cancellationToken);
        if (configuration.GetValue<bool>("Database:SkipSeed"))
        {
            logger.LogInformation("Database seed skipped by Database:SkipSeed configuration");
            return;
        }

        await dbSeedService.SeedAsync(cancellationToken);
    }
}
