using AsterERP.Api.Application.AsterScene;
using AsterERP.Api.Infrastructure.Security.DataPermissions;
using AsterERP.Api.Modules.AsterScene;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AsterERP.Api.Infrastructure.Abp.AsterScene;

public sealed class AsterErpAsterSceneModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<AsterSceneWorkspaceContext>();
        context.Services.AddScoped<AsterSceneDocumentService>();
        context.Services.AddScoped<AsterSceneAssetService>();
        context.Services.AddScoped<AsterScenePublishService>();
        context.Services.AddScoped<AsterScenePublicService>();
        context.Services.AddScoped<AsterSceneCommerceGovernanceService>();
        context.Services.AddScoped<AsterSceneSchemaMigrator>();
        context.Services.AddScoped<AsterSceneSeedService>();
        context.Services.AddHostedService<AsterSceneJobWorker>();
    }

    public static void RegisterDataFilters(IDataPermissionFilterRegistry registry)
    {
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneProjectEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneDocumentEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneAssetEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneAssetVersionEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneUploadSessionEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneUploadChunkEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneJobEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterScenePublishVersionEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterScenePublicWorkEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneCreatorProfileEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneCommunityReactionEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneRemixEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneSubscriptionEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneUsageLedgerEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneModerationCaseEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneAiCreditLedgerEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneSupportTicketEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneSupportCommentEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneModerationDecisionEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneModerationEvidenceEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneAppealEntity));
        registry.RegisterAsterSceneWorkspaceFilter(typeof(AsterSceneAppealDecisionEntity));
    }
}
