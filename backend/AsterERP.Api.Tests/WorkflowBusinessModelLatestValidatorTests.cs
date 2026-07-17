using AsterERP.Api.Application.Workflows;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowBusinessModelLatestValidatorTests
{
    [Fact]
    public void ValidatePersisted_AcceptsTheCanonicalLatestBusinessModel()
    {
        var validator = new WorkflowBusinessModelLatestValidator();

        validator.ValidatePersisted("""
        {
          "version":"latest",
          "kind":"WorkflowBusinessModelLatest",
          "businessDesign":{
            "version":"latest",
            "selectedNodeId":"start",
            "nodes":[{"id":"start","type":"start","label":"Start","position":{}},{"id":"end","type":"end","label":"End","position":{}}],
            "edges":[{"id":"start_end","source":"start","target":"end"}]
          }
        }
        """);
    }

    [Fact]
    public void ValidatePersisted_RejectsAnUnversionedModelInsteadOfTreatingBpmnAsAuthority()
    {
        var validator = new WorkflowBusinessModelLatestValidator();

        var exception = Assert.Throws<ValidationException>(() => validator.ValidatePersisted("""
        {"businessDesign":{"nodes":[{"id":"start","type":"start"},{"id":"end","type":"end"}],"edges":[]}}
        """));

        Assert.Contains("MigrationBlocked", exception.Message, StringComparison.Ordinal);
    }
}
