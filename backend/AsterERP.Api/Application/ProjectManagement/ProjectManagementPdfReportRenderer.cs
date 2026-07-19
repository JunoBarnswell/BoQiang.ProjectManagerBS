using System.Globalization;
using System.Text;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 生成自包含、分页稳定的 PDF。STSong-Light 是 PDF 阅读器内置 CJK 字体，避免服务器依赖本地字体文件。
/// 所有用户数据写入 UTF-16BE 十六进制字符串，不经过 PDF 模板语法，避免模板注入。
/// </summary>
internal static class ProjectManagementPdfReportRenderer
{
    private const int RowsPerPage = 38;

    public static byte[] Render(ProjectManagementReportPdfDocument document)
    {
        var lines = BuildLines(document);
        var pages = lines.Chunk(RowsPerPage).Select((page, index) => BuildPage(page, index == 0)).ToList();
        if (pages.Count == 0) pages.Add(BuildPage([], true));

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(' ', Enumerable.Range(0, pages.Count).Select(index => $"{3 + index * 2} 0 R"))}] /Count {pages.Count} >>"
        };
        var fontId = 3 + pages.Count * 2;
        for (var index = 0; index < pages.Count; index++)
        {
            var pageId = 3 + index * 2;
            var contentId = pageId + 1;
            var content = pages[index];
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentId} 0 R >>");
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }
        objects.Add($"<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light /Encoding /UniGB-UCS2-H /DescendantFonts [{fontId + 1} 0 R] >>");
        objects.Add("<< /Type /Font /Subtype /CIDFontType0 /BaseFont /STSong-Light /CIDSystemInfo << /Registry (Adobe) /Ordering (GB1) /Supplement 5 >> >>");

        using var output = new MemoryStream();
        Write(output, "%PDF-1.4\n%\u00E2\u00E3\u00CF\u00D3\n");
        var offsets = new List<long> { 0 };
        for (var index = 0; index < objects.Count; index++)
        {
            offsets.Add(output.Position);
            Write(output, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
        }
        var xref = output.Position;
        Write(output, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var index = 1; index < offsets.Count; index++) Write(output, $"{offsets[index]:D10} 00000 n \n");
        Write(output, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return output.ToArray();
    }

    private static IReadOnlyList<string> BuildLines(ProjectManagementReportPdfDocument document)
    {
        var lines = new List<string>
        {
            "项目管理项目报告",
            $"生成时间：{document.GeneratedAt.ToUniversalTime():yyyy-MM-dd HH:mm:ss} UTC",
            $"租户：{document.TenantId}  应用：{document.AppCode}  生成用户：{document.UserId}",
            $"项目数：{document.Projects.Count}  任务数：{document.Tasks.Count}  已删除选项：{(document.IncludeDeleted ? "包含" : "排除")}",
            $"工作量：预计 {document.EstimatedMinutes.ToString(CultureInfo.InvariantCulture)} 分钟 / 实际 {document.ActualMinutes.ToString(CultureInfo.InvariantCulture)} 分钟",
            $"未来到期：{document.FutureDueCount}  逾期：{document.OverdueCount}  阻塞：{document.BlockedCount}",
            "任务状态分布："
        };
        foreach (var item in document.TaskStatusDistribution.OrderBy(item => item.Key, StringComparer.Ordinal))
            lines.Add($"  {item.Key}：{item.Value} {new string('■', Math.Min(item.Value, 40))}");

        lines.Add("关键路径：");
        lines.AddRange(document.CriticalPath.Count == 0
            ? ["  无可计算关键路径"]
            : document.CriticalPath.Select(item => $"  {item.TaskCode} {item.Title}（{item.Status}，{item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "无截止日期"}）"));

        lines.Add("项目摘要：");
        lines.AddRange(document.Projects.Select(item =>
            $"  {item.ProjectCode} | {item.ProjectName} | {item.Status} | 进度 {item.ProgressPercent.ToString("0.##", CultureInfo.InvariantCulture)}% | 任务 {item.TaskCount} | 到期 {item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-"}"));

        lines.Add("里程碑：");
        lines.AddRange(document.Milestones.Count == 0
            ? ["  无里程碑"]
            : document.Milestones.Select(item => $"  {item.Name} | {item.Status} | 进度 {item.ProgressPercent.ToString("0.##", CultureInfo.InvariantCulture)}% | 到期 {item.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "-"}"));

        lines.Add(document.IncludeGanttSnapshot ? "甘特快照（任务明细）：" : "任务明细：");
        lines.AddRange(document.Tasks.Count == 0
            ? ["  无任务"]
            : document.Tasks.Select(item => $"  {new string(' ', Math.Min(item.Depth, 8) * 2)}{item.TaskCode} | {item.Title} | {item.Status} | {item.StartDate?.ToString("MM-dd", CultureInfo.InvariantCulture) ?? "-"} ~ {item.DueDate?.ToString("MM-dd", CultureInfo.InvariantCulture) ?? "-"} | 预计 {item.EstimateMinutes} / 实际 {item.ActualMinutes}{(item.IsBlocked ? " | 阻塞" : string.Empty)}"));

        if (document.CommentSummaries.Count > 0)
        {
            lines.Add("评论摘要：");
            lines.AddRange(document.CommentSummaries.Select(item => $"  {item}"));
        }
        if (document.AttachmentList.Count > 0)
        {
            lines.Add("附件清单：");
            lines.AddRange(document.AttachmentList.Select(item => $"  {item}"));
        }
        return lines.SelectMany(WrapLine).ToList();
    }

    private static IEnumerable<string> WrapLine(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (normalized.Length == 0) return [string.Empty];
        return Enumerable.Range(0, (normalized.Length + 74) / 75).Select(index => normalized.Substring(index * 75, Math.Min(75, normalized.Length - index * 75)));
    }

    private static string BuildPage(IReadOnlyList<string> lines, bool firstPage)
    {
        var content = new StringBuilder("BT\n/F1 9 Tf\n");
        for (var index = 0; index < lines.Count; index++)
        {
            var y = 805 - index * 20;
            content.Append(firstPage && index == 0 ? "/F1 16 Tf\n" : "/F1 9 Tf\n");
            content.Append($"1 0 0 1 40 {y} Tm\n<{ToPdfHex(lines[index])}> Tj\n");
        }
        content.Append("ET");
        return content.ToString();
    }

    private static string ToPdfHex(string value) => Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value));
    private static void Write(Stream output, string value) => output.Write(Encoding.ASCII.GetBytes(value));
}
