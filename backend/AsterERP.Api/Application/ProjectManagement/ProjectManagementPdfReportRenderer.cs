using System.Text;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 使用 PDF 标准 CJK 字体 STSong-Light 输出项目报表，避免依赖浏览器打印或临时 HTML 文件。
/// </summary>
internal static class ProjectManagementPdfReportRenderer
{
    private const int RowsPerPage = 32;

    public static byte[] Render(IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var pages = new List<string>();
        var documentRows = new List<string[]> { headers.ToArray() };
        documentRows.AddRange(rows);
        for (var index = 0; index < documentRows.Count || index == 0; index += RowsPerPage)
        {
            pages.Add(BuildPage(documentRows.Skip(index).Take(RowsPerPage).ToList(), index == 0));
        }

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
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentId} 0 R >>");
            var content = pages[index];
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream");
        }
        objects.Add("<< /Type /Font /Subtype /Type0 /BaseFont /STSong-Light /Encoding /UniGB-UCS2-H /DescendantFonts [" + (fontId + 1) + " 0 R] >>");
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

    private static string BuildPage(IReadOnlyList<string[]> rows, bool firstPage)
    {
        var content = new StringBuilder("BT\n/F1 10 Tf\n");
        if (firstPage)
        {
            content.Append("/F1 16 Tf\n50 805 Td\n<987976EE7BA1> Tj\n/F1 10 Tf\n");
        }
        var y = firstPage ? 780 : 810;
        foreach (var row in rows)
        {
            content.Append($"50 {y} Td\n<{ToPdfHex(FormatRow(row))}> Tj\n");
            y -= 22;
        }
        content.Append("ET");
        return content.ToString();
    }

    private static string FormatRow(IReadOnlyList<string> row) => string.Join("  |  ", row.Select(value => value.Length > 24 ? value[..24] + "…" : value));
    private static string ToPdfHex(string value) => Convert.ToHexString(Encoding.BigEndianUnicode.GetBytes(value));
    private static void Write(Stream output, string value) => output.Write(Encoding.ASCII.GetBytes(value));
}
