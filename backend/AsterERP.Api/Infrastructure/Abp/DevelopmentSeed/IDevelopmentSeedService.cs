namespace AsterERP.Api.Infrastructure.Abp.DevelopmentSeed;

public interface IDevelopmentSeedService
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
