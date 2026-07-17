using System.Text;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataPreviewReaderCsvTests
{
    [Fact]
    public void Preview_csv_preserves_quoted_delimiters_embedded_newlines_and_escaped_quotes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"astererp-csv-{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(path, "name,note\r\nAlice,\"comma, newline\r\ntext and \"\"quote\"\"\"\r\n", Encoding.UTF8);
            var reader = new ApplicationDataPreviewReader(new ApplicationDataSourceProviderRegistry([]));

            var result = reader.PreviewCsv(path, ",", true, 2, Encoding.UTF8, 20);

            Assert.Equal(2, result.Fields.Count);
            Assert.Single(result.Rows);
            Assert.Equal("Alice", result.Rows[0]["name"]);
            Assert.Equal("comma, newline\r\ntext and \"quote\"", result.Rows[0]["note"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Preview_csv_rejects_multi_character_delimiter()
    {
        var reader = new ApplicationDataPreviewReader(new ApplicationDataSourceProviderRegistry([]));
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "a||b", Encoding.UTF8);
            Assert.Throws<ValidationException>(() => reader.PreviewCsv(path, "||", true, 2, Encoding.UTF8, 20));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
