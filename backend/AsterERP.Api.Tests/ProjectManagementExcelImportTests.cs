using System.Security.Claims;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Abp.ProjectManagement;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementExcelImportTests
{
    [Fact]
    public async Task Template_contains_versioned_project_task_member_sheets()
    {
        var service = CreateService(CreateDatabase(), CreateUser());
        var file = await service.DownloadTemplateAsync();

        using var workbook = new XLWorkbook(new MemoryStream(file.Content));
        Assert.Equal("1.0", workbook.Worksheet("README").Cell(2, 2).GetString());
        Assert.Equal(ProjectManagementExcelImportTemplate.Columns[ProjectManagementExcelImportTemplate.ProjectsSheet].ToArray(), Header(workbook.Worksheet("Projects")));
        Assert.Equal(ProjectManagementExcelImportTemplate.Columns[ProjectManagementExcelImportTemplate.TasksSheet].ToArray(), Header(workbook.Worksheet("Tasks")));
        Assert.Equal(ProjectManagementExcelImportTemplate.Columns[ProjectManagementExcelImportTemplate.MembersSheet].ToArray(), Header(workbook.Worksheet("Members")));
    }

    [Fact]
    public async Task Preview_validates_rows_without_writing_business_data()
    {
        using var db = CreateDatabase();
        await new ProjectManagementSchemaMigrator().MigrateAsync(db, CancellationToken.None);
        await db.Insertable(new ProjectManagementProjectEntity
        {
            Id = "project-a", TenantId = "tenant-a", AppCode = "SYSTEM", ProjectCode = "P-A", ProjectName = "Existing", OwnerUserId = "operator"
        }).ExecuteCommandAsync();
        var user = CreateUser();
        var service = CreateService(db, user);
        var template = await service.DownloadTemplateAsync();
        using var workbook = new XLWorkbook(new MemoryStream(template.Content));
        var projects = workbook.Worksheet("Projects");
        projects.Cell(2, 1).Value = "project-a";
        projects.Cell(2, 2).Value = "P-A";
        projects.Cell(2, 3).Value = "Updated name";
        projects.Cell(3, 1).Value = "project-a";
        projects.Cell(3, 2).Value = "P-A-duplicate";
        projects.Cell(3, 3).Value = "Duplicate";
        var tasks = workbook.Worksheet("Tasks");
        tasks.Cell(2, 1).Value = "task-a";
        tasks.Cell(2, 2).Value = "project-a";
        tasks.Cell(2, 5).Value = "T-A";
        tasks.Cell(2, 6).FormulaA1 = "1+1";
        tasks.Cell(3, 1).Value = "task-b";
        tasks.Cell(3, 2).Value = "project-a";
        tasks.Cell(3, 5).Value = "T-B";
        tasks.Cell(3, 6).Value = "Task B";
        tasks.Cell(3, 4).Value = "task-c";
        tasks.Cell(4, 1).Value = "task-c";
        tasks.Cell(4, 2).Value = "project-a";
        tasks.Cell(4, 5).Value = "T-C";
        tasks.Cell(4, 6).Value = "Task C";
        tasks.Cell(4, 4).Value = "task-b";
        tasks.Cell(5, 1).Value = "task-invalid";
        tasks.Cell(5, 2).Value = "project-a";
        tasks.Cell(5, 5).Value = "T-I";
        tasks.Cell(5, 6).Value = "Invalid";
        tasks.Cell(5, 15).Value = "not-a-number";
        var members = workbook.Worksheet("Members");
        members.Cell(2, 1).Value = "member-a";
        members.Cell(2, 2).Value = "project-a";
        members.Cell(2, 3).Value = "user-a";
        members.Cell(2, 5).Value = "Member";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var formFile = new FormFile(stream, 0, stream.Length, "file", "import.xlsx") { Headers = new HeaderDictionary(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };

        var preview = await service.PreviewAsync(formFile);

        Assert.Equal(ProjectManagementExcelImportPreviewStatuses.CompletedWithErrors, preview.Status);
        Assert.Equal(7, preview.TotalRows);
        Assert.True(preview.ErrorRows >= 5);
        Assert.Contains(preview.Errors, error => error.Code == "DuplicateStableId");
        Assert.Contains(preview.Errors, error => error.Code == "FormulaNotAllowed");
        Assert.Contains(preview.Errors, error => error.Code == "HierarchyCycle");
        Assert.Contains(preview.Errors, error => error.Code == "InvalidNumber");
        Assert.Equal(1, await db.Queryable<ProjectManagementProjectEntity>().CountAsync());
        Assert.Equal(0, await db.Queryable<ProjectManagementTaskEntity>().CountAsync());
        Assert.Equal(0, await db.Queryable<ProjectManagementProjectMemberEntity>().CountAsync());
    }

    [Fact]
    public async Task Preview_honors_file_and_cancellation_boundaries()
    {
        using var db = CreateDatabase();
        var service = CreateService(db, CreateUser());
        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var invalidFile = new FormFile(stream, 0, stream.Length, "file", "import.xls") { Headers = new HeaderDictionary() };
        await Assert.ThrowsAsync<AsterERP.Shared.Exceptions.ValidationException>(() => service.PreviewAsync(invalidFile));
        using var cancellationStream = new MemoryStream(new byte[] { 1, 2, 3 });
        var cancellationFile = new FormFile(cancellationStream, 0, cancellationStream.Length, "file", "import.xlsx") { Headers = new HeaderDictionary() };
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PreviewAsync(cancellationFile, cts.Token));
    }

    private static ProjectManagementExcelImportService CreateService(SqlSugarClient db, FixedAsterErpCurrentUser user) =>
        new(new TestWorkspaceDatabaseAccessor(db), user, new SelectableCandidateService());

    private static string[] Header(IXLWorksheet worksheet) => worksheet.Row(1).CellsUsed().Select(cell => cell.GetString()).ToArray();

    private static SqlSugarClient CreateDatabase() => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source=file:project-management-excel-{Guid.NewGuid():N};Mode=Memory;Cache=Shared",
        DbType = DbType.Sqlite,
        IsAutoCloseConnection = false
    });

    private static FixedAsterErpCurrentUser CreateUser() => new(new ClaimsPrincipal(new ClaimsIdentity(new[]
    {
        new Claim(AsterErpClaimTypes.UserId, "operator"),
        new Claim(AsterErpClaimTypes.TenantId, "tenant-a"),
        new Claim(AsterErpClaimTypes.AppCode, "SYSTEM"),
        new Claim(AsterErpClaimTypes.PermissionCode, PermissionCodes.ProjectManagementSyncImport)
    }, "test")));

    private sealed class SelectableCandidateService : IProjectManagementMemberCandidateService
    {
        public Task<bool> IsSelectableAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult(userId == "user-a");
        public Task<bool> IsSelectableAsync(string userId, string? employmentId, CancellationToken cancellationToken = default) => IsSelectableAsync(userId, cancellationToken);

        public Task<GridPageResult<ProjectManagementMemberCandidateResponse>> QueryAsync(ProjectManagementMemberCandidateQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GridPageResult<ProjectManagementMemberCandidateResponse> { Total = 0, Items = [] });
    }

    private sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
    {
        public ISqlSugarClient MainDb => db;
        public ISqlSugarClient GetCurrentDb() => db;
        public ISqlSugarClient RequireApplicationDb() => db;
        public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
        public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
    }
}
