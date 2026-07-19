using System.Text;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Controllers;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ProjectManagementPdfReportTests
{
    [Fact]
    public void Pdf_report_is_paginated_cjk_safe_and_contains_required_sections()
    {
        var tasks = Enumerable.Range(1, 45)
            .Select(index => new ProjectManagementReportPdfTask($"task-{index}", "project-1", $"T-{index:000}", $"任务 {index}", index % 2 == 0 ? "Todo" : "Blocked", "High", "operator", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(index), 20, 60, 10, index % 3, index % 2 == 0, false))
            .ToList();
        var document = new ProjectManagementReportPdfDocument(
            DateTime.UtcNow, "tenant-a", "MES", "operator",
            [new ProjectManagementReportRow("P-001", "项目一", "Active", "High", "operator", 50, tasks.Count, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30), DateTime.UtcNow, 2700, 450)],
            [new ProjectManagementReportPdfMilestone("project-1", "里程碑一", "Active", 40, DateTime.UtcNow.Date.AddDays(10))],
            tasks,
            new Dictionary<string, int> { ["Blocked"] = 22, ["Todo"] = 23 },
            2700, 450, 45, 2, 22, tasks.Take(3).ToList(), ["T-001: 评论摘要"], ["T-001: 设计说明.pdf (1024 bytes)"], true, false);

        var pdf = ProjectManagementPdfReportRenderer.Render(document);
        var text = Encoding.ASCII.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.Contains("/STSong-Light", text, StringComparison.Ordinal);
        Assert.Contains("/Count 2", text, StringComparison.Ordinal);
        Assert.Contains(Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes("项目管理项目报告")), text, StringComparison.Ordinal);
        Assert.True(pdf.Length > 3000);
    }

    [Fact]
    public void Reports_controller_exposes_snapshot_retry_with_export_permission()
    {
        var controller = typeof(ProjectManagementReportsController);
        var classPermission = Assert.Single(controller.GetCustomAttributes(typeof(PermissionAttribute), true));
        Assert.Equal(PermissionCodes.ProjectManagementReportExport, ((PermissionAttribute)classPermission).Code);
        Assert.Contains(controller.GetMethods(), method => method.Name == nameof(ProjectManagementReportsController.RetrySnapshotAsync));
    }
}
