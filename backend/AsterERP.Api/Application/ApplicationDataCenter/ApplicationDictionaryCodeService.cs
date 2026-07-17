using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Modules.System.CodeRules;
using AsterERP.Api.Modules.System.Dicts;
using AsterERP.Contracts.ApplicationDataCenter;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDictionaryCodeService(
    IRepository<ApplicationDataCenterDictionaryEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService)
    : ApplicationDataCenterObjectService<ApplicationDataCenterDictionaryEntity>(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.DictionaryCode;

    public override async Task<ApplicationDataCenterOperationResponse> PublishAsync(
        string id,
        ApplicationDataCenterPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = WorkspaceResolver.Resolve();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await EnsureEntityAsync(id, cancellationToken);
        if (entity.ObjectType == ApplicationDictionaryCodeObjectType.DictionaryType)
        {
            await UpsertDictTypeAsync(db, workspace, entity, cancellationToken);
        }
        else if (entity.ObjectType == ApplicationDictionaryCodeObjectType.CodeRule)
        {
            await UpsertCodeRuleAsync(db, workspace, entity, cancellationToken);
        }

        entity.Status = ApplicationDataCenterObjectStatus.Published;
        entity.VersionNo += 1;
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        var summary = await ReferenceService.RecalculateReferencesAsync(ModuleKey, entity.Id, cancellationToken);
        var detail = await MapDetailAsync(await EnsureEntityAsync(id, cancellationToken), cancellationToken);
        return new ApplicationDataCenterOperationResponse(detail, summary, detail.NextActions);
    }

    public override async Task<ApplicationDataCenterPreviewResponse> PreviewAsync(
        string id,
        ApplicationDataCenterPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await EnsureEntityAsync(id, cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        if (entity.ObjectType == ApplicationDictionaryCodeObjectType.CodeRule)
        {
            var sample = BuildCodeRulePreview(config);
            return new ApplicationDataCenterPreviewResponse(
                [new Dictionary<string, object?> { ["preview"] = sample }],
                [new ApplicationDataCenterPreviewFieldResponse("preview", "预览编码", "Text", false, false, 1)],
                "编码规则预览成功");
        }

        var items = ReadItems(config);
        return new ApplicationDataCenterPreviewResponse(
            items.Select((item, index) => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["label"] = item.Label,
                ["value"] = item.Value,
                ["sortOrder"] = index + 1
            }).ToArray(),
            [
                new ApplicationDataCenterPreviewFieldResponse("label", "名称", "Text", false, false, 1),
                new ApplicationDataCenterPreviewFieldResponse("value", "值", "Text", false, false, 2),
                new ApplicationDataCenterPreviewFieldResponse("sortOrder", "排序", "Number", false, false, 3)
            ],
            "字典项预览成功");
    }

    private static async Task UpsertDictTypeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDataCenterDictionaryEntity entity,
        CancellationToken cancellationToken)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var dictCode = ReadString(config, "dictCode") ?? entity.ObjectCode;
        var dictName = ReadString(config, "dictName") ?? entity.ObjectName;
        var dictType = (await db.Queryable<SystemDictTypeEntity>()
            .Where(item => item.DictCode == dictCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (dictType is null)
        {
            dictType = new SystemDictTypeEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                DictCode = dictCode,
                DictName = dictName,
                IsEnabled = true,
                CreatedBy = workspace.UserId,
                CreatedTime = now
            };
            await db.Insertable(dictType).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            dictType.DictName = dictName;
            dictType.IsEnabled = true;
            dictType.UpdatedBy = workspace.UserId;
            dictType.UpdatedTime = now;
            await db.Updateable(dictType).ExecuteCommandAsync(cancellationToken);
        }

        var oldItems = await db.Queryable<SystemDictItemEntity>()
            .Where(item => item.DictTypeId == dictType.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var item in oldItems)
        {
            item.IsDeleted = true;
            item.DeletedBy = workspace.UserId;
            item.DeletedTime = now;
        }

        if (oldItems.Count > 0)
        {
            await db.Updateable(oldItems).ExecuteCommandAsync(cancellationToken);
        }

        var inserts = ReadItems(config)
            .Select((item, index) => new SystemDictItemEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                DictTypeId = dictType.Id,
                ItemLabel = item.Label,
                ItemValue = item.Value,
                SortOrder = index + 1,
                IsEnabled = true,
                CreatedBy = workspace.UserId,
                CreatedTime = now
            })
            .ToArray();
        if (inserts.Length > 0)
        {
            await db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        }

        entity.RuntimeObjectId = dictType.Id;
        entity.RuntimeObjectCode = dictType.DictCode;
    }

    private static async Task UpsertCodeRuleAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        ApplicationDataCenterDictionaryEntity entity,
        CancellationToken cancellationToken)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var ruleCode = ReadString(config, "ruleCode") ?? entity.ObjectCode;
        var rule = (await db.Queryable<SystemCodeRuleEntity>()
            .Where(item => item.RuleCode == ruleCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        var now = DateTime.UtcNow;
        if (rule is null)
        {
            rule = new SystemCodeRuleEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                RuleCode = ruleCode,
                RuleName = ReadString(config, "ruleName") ?? entity.ObjectName,
                ResetPolicy = ReadString(config, "resetPolicy") ?? "Daily",
                IsEnabled = true,
                CreatedBy = workspace.UserId,
                CreatedTime = now
            };
            await db.Insertable(rule).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            rule.RuleName = ReadString(config, "ruleName") ?? entity.ObjectName;
            rule.ResetPolicy = ReadString(config, "resetPolicy") ?? "Daily";
            rule.IsEnabled = true;
            rule.UpdatedBy = workspace.UserId;
            rule.UpdatedTime = now;
            await db.Updateable(rule).ExecuteCommandAsync(cancellationToken);
        }

        entity.RuntimeObjectId = rule.Id;
        entity.RuntimeObjectCode = rule.RuleCode;
    }

    private static string BuildCodeRulePreview(IReadOnlyDictionary<string, object?> config)
    {
        var pattern = ReadString(config, "pattern") ?? $"{ReadString(config, "prefix") ?? "NO"}-{{yyyyMMdd}}-{{0001}}";
        return pattern
            .Replace("{yyyyMMdd}", DateTime.UtcNow.ToString("yyyyMMdd"), StringComparison.Ordinal)
            .Replace("{yyyyMM}", DateTime.UtcNow.ToString("yyyyMM"), StringComparison.Ordinal)
            .Replace("{0001}", "0001", StringComparison.Ordinal);
    }

    private static IReadOnlyList<(string Label, string Value)> ReadItems(IReadOnlyDictionary<string, object?> config)
    {
        if (!config.TryGetValue("items", out var itemsValue) || itemsValue is null)
        {
            return [];
        }

        var itemsJson = itemsValue.ToString();
        if (string.IsNullOrWhiteSpace(itemsJson))
        {
            return [];
        }

        var items = ApplicationDataCenterJson.Deserialize<List<Dictionary<string, object?>>>(itemsJson);
        return (items ?? [])
            .Select(item => (
                ReadString(item, "label") ?? ReadString(item, "itemLabel") ?? string.Empty,
                ReadString(item, "value") ?? ReadString(item, "itemValue") ?? string.Empty))
            .Where(item => !string.IsNullOrWhiteSpace(item.Item1) && !string.IsNullOrWhiteSpace(item.Item2))
            .ToArray();
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;
}
