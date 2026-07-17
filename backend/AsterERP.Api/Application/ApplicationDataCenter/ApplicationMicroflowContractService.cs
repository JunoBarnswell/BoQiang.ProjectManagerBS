using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowContractService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationMicroflowOutputSchemaSynchronizer outputSchemaSynchronizer)
{
    private const int MaxBatchSize = 100;

    public async Task<RuntimeMicroflowContractResponse> GetAsync(
        string flowCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeFlowCode(flowCode);
        var contracts = await GetManyAsync([normalizedCode], cancellationToken);
        return contracts.First();
    }

    public async Task<IReadOnlyList<RuntimeMicroflowContractResponse>> GetManyAsync(
        IEnumerable<string> flowCodes,
        CancellationToken cancellationToken = default)
    {
        var normalizedCodes = flowCodes
            .Select(NormalizeOptionalFlowCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxBatchSize + 1)
            .ToArray();
        if (normalizedCodes.Length == 0)
        {
            return [];
        }

        if (normalizedCodes.Length > MaxBatchSize)
        {
            throw new ValidationException($"单次最多读取 {MaxBatchSize} 个微流契约", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entities = await db.Queryable<ApplicationMicroflowEntity>()
            .Where(item =>
                item.ModuleKey == ApplicationDataCenterModuleKey.Microflow &&
                item.Status == ApplicationDataCenterObjectStatus.Published &&
                !item.IsDeleted &&
                normalizedCodes.Contains(item.ObjectCode))
            .ToListAsync(cancellationToken);

        var byCode = entities.ToDictionary(item => item.ObjectCode, StringComparer.OrdinalIgnoreCase);
        var missing = normalizedCodes.Where(code => !byCode.ContainsKey(code)).ToArray();
        if (missing.Length > 0)
        {
            throw new NotFoundException($"微流不存在或未发布: {string.Join(", ", missing)}", ErrorCodes.ApplicationDataCenterObjectNotFound);
        }

        return normalizedCodes
            .Select(code => MapContract(byCode[code]))
            .ToArray();
    }

    private RuntimeMicroflowContractResponse MapContract(ApplicationMicroflowEntity entity)
    {
        var definition = outputSchemaSynchronizer.Synchronize(ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson));
        return new RuntimeMicroflowContractResponse(
            entity.ObjectCode,
            entity.ObjectName,
            definition.Inputs,
            definition.Outputs,
            entity.VersionNo);
    }

    private static string NormalizeFlowCode(string flowCode)
    {
        var normalized = NormalizeOptionalFlowCode(flowCode);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException("微流编码不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized;
    }

    private static string NormalizeOptionalFlowCode(string? flowCode) => flowCode?.Trim() ?? string.Empty;
}
