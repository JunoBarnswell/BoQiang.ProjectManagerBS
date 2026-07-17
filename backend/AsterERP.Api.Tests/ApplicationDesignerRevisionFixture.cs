using System.Text.Json.Nodes;
using AsterERP.Api.Modules.ApplicationDevelopmentCenter;
using SqlSugar;

namespace AsterERP.Api.Tests;

internal static class ApplicationDesignerRevisionFixture
{
    public static async Task InsertDocumentAsync(
        ISqlSugarClient db,
        ApplicationDesignerDocumentEntity document,
        CancellationToken cancellationToken = default)
    {
        await db.Insertable(document).ExecuteCommandAsync(cancellationToken);
        await db.Insertable(CreateRevision(document)).ExecuteCommandAsync(cancellationToken);
    }

    public static async Task SynchronizeCurrentRevisionAsync(
        ISqlSugarClient db,
        ApplicationDesignerDocumentEntity document,
        CancellationToken cancellationToken = default)
    {
        await db.Updateable<ApplicationDesignerRevisionEntity>()
            .SetColumns(item => new ApplicationDesignerRevisionEntity
            {
                DocumentJson = document.DocumentJson,
                DocumentHash = document.DocumentHash,
                SourceHash = document.SourceHash,
                TargetHash = document.TargetHash
            })
            .Where(item => item.Id == document.CurrentRevisionId)
            .ExecuteCommandAsync(cancellationToken);
    }

    private static ApplicationDesignerRevisionEntity CreateRevision(ApplicationDesignerDocumentEntity document)
    {
        if (string.IsNullOrWhiteSpace(document.CurrentRevisionId))
        {
            throw new InvalidOperationException("The fixture requires a current revision id.");
        }

        var documentNode = JsonNode.Parse(document.DocumentJson)?.AsObject()
            ?? throw new InvalidOperationException("The fixture document must be a JSON object.");
        var revisionNumber = documentNode["revision"]?.GetValue<int>() ?? 0;
        if (revisionNumber < 1)
        {
            throw new InvalidOperationException("The fixture document must contain a positive revision.");
        }

        return new ApplicationDesignerRevisionEntity
        {
            Id = document.CurrentRevisionId,
            TenantId = document.TenantId,
            AppCode = document.AppCode,
            DocumentId = document.Id,
            RevisionNumber = revisionNumber,
            DocumentJson = document.DocumentJson,
            DocumentHash = document.DocumentHash,
            SourceHash = document.SourceHash,
            TargetHash = document.TargetHash,
            MigrationRevision = document.MigrationRevision,
            ChangeSetJson = "{}",
            DiagnosticsJson = "[]",
            CreatedBy = document.CreatedBy,
            CreatedTime = document.CreatedTime,
            IsDeleted = false
        };
    }
}
