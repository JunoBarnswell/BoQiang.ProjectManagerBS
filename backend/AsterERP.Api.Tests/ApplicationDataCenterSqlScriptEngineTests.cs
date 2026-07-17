using System.Reflection;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataCenterSqlScriptEngineTests
{
    [Theory]
    [InlineData("BEGIN")]
    [InlineData("COMMIT")]
    [InlineData("ROLLBACK")]
    [InlineData("SAVEPOINT work")]
    [InlineData("ROLLBACK TO work")]
    public void RejectUserTransactionStatements_RejectsUserControlledTransactionBoundaries(string statement)
    {
        var method = typeof(ApplicationDataCenterSqlScriptEngine).GetMethod(
            "RejectUserTransactionStatements",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var exception = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, [statement]));
        Assert.IsType<ValidationException>(exception.InnerException);
    }

    [Fact]
    public void ReturnSelect_ReadsWithExecutionCancellationAndRollsBackOnFailure()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "backend",
            "AsterERP.Api",
            "Application",
            "ApplicationDataCenter",
            "ApplicationDataCenterSqlScriptEngine.cs"));

        Assert.Contains("ExecuteReaderAsync(cancellationToken)", source, StringComparison.Ordinal);
        Assert.Contains("ReadAsync(cancellationToken)", source, StringComparison.Ordinal);
        Assert.Contains("RollbackTransactionAsync(db)", source, StringComparison.Ordinal);
        Assert.Contains("await auditWriter.EnsureAvailableAsync(CancellationToken.None)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_writer_is_insert_only_and_never_creates_runtime_tables()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "backend",
            "AsterERP.Api",
            "Application",
            "ApplicationDataCenter",
            "ApplicationDataCenterSqlScriptAuditWriter.cs"));

        Assert.Contains("Insertable(audit).ExecuteCommandAsync(cancellationToken)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CodeFirst", source, StringComparison.Ordinal);
        Assert.DoesNotContain("InitTables", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("AsterERP repository root was not found.");
    }
}
