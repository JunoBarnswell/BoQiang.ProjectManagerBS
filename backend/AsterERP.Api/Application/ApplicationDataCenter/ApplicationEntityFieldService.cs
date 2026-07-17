using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationEntityFieldService(
    IRepository<ApplicationDataEntityDefinitionEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService)
    : ApplicationDataCenterObjectService<ApplicationDataEntityDefinitionEntity>(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.EntityField;

    protected override void ApplySpecificFields(
        ApplicationDataEntityDefinitionEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        bool isCreate)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        entity.ModelId = ReadString(config, "modelId") ?? entity.ModelId;
        entity.SourceTable = ReadString(config, "sourceTable") ?? ReadString(config, "tableName");
        entity.KeyField = ReadString(config, "keyField") ?? "id";
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;
}
