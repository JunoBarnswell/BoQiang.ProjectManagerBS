using System.Text.Json;
using AsterERP.Api.Application.Workflows;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowSubmittedFormSnapshotTests
{
    [Fact]
    public void Capture_FiltersSystemVariables()
    {
        var snapshot = WorkflowSubmittedFormSnapshot.Capture(new Dictionary<string, object?>
        {
            ["tenantId"] = "default",
            ["appCode"] = "erp",
            ["_runtimeToken"] = "secret",
            ["amount"] = 1280,
            ["reason"] = "采购审批"
        });

        var form = WorkflowSubmittedFormSnapshot.Build(snapshot, null);

        Assert.Equal(WorkflowSubmittedFormSnapshot.SubmittedSnapshotSource, form.Source);
        Assert.DoesNotContain(form.Fields, field => field.Field == "tenantId");
        Assert.DoesNotContain(form.Fields, field => field.Field == "appCode");
        Assert.DoesNotContain(form.Fields, field => field.Field == "_runtimeToken");
        AssertJsonNumber(form, "amount", 1280);
        AssertJsonString(form, "reason", "采购审批");
    }

    [Fact]
    public void Capture_IsImmutableAfterVariablesChange()
    {
        var variables = new Dictionary<string, object?>
        {
            ["amount"] = 100
        };
        var snapshot = WorkflowSubmittedFormSnapshot.Capture(variables);

        variables["amount"] = 900;

        var form = WorkflowSubmittedFormSnapshot.Build(snapshot, null);

        AssertJsonNumber(form, "amount", 100);
    }

    [Fact]
    public void Build_UsesSubmittedSnapshotBeforeRuntimeFallback()
    {
        var submitted = WorkflowSubmittedFormSnapshot.Capture(new Dictionary<string, object?>
        {
            ["amount"] = 200
        });
        var runtime = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["amount"] = 300
        });

        var form = WorkflowSubmittedFormSnapshot.Build(submitted, runtime);

        Assert.Equal(WorkflowSubmittedFormSnapshot.SubmittedSnapshotSource, form.Source);
        AssertJsonNumber(form, "amount", 200);
    }

    [Fact]
    public void Build_UsesFieldLabelsWhenProvided()
    {
        var submitted = WorkflowSubmittedFormSnapshot.Capture(new Dictionary<string, object?>
        {
            ["amount"] = 200,
            ["reason"] = "采购审批"
        });

        var form = WorkflowSubmittedFormSnapshot.Build(
            submitted,
            null,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = "金额"
            });

        Assert.Equal("金额", Assert.Single(form.Fields, field => field.Field == "amount").Label);
        Assert.Equal("reason", Assert.Single(form.Fields, field => field.Field == "reason").Label);
    }

    [Fact]
    public void Build_FallsBackToFilteredRuntimeSnapshot()
    {
        var runtime = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["approvalAction"] = "complete",
            ["starterUserId"] = "u1",
            ["_engineState"] = "internal",
            ["approved"] = true,
            ["costCenter"] = "IT"
        });

        var form = WorkflowSubmittedFormSnapshot.Build(null, runtime);

        Assert.Equal(WorkflowSubmittedFormSnapshot.RuntimeSnapshotFallbackSource, form.Source);
        Assert.DoesNotContain(form.Fields, field => field.Field == "approvalAction");
        Assert.DoesNotContain(form.Fields, field => field.Field == "starterUserId");
        Assert.DoesNotContain(form.Fields, field => field.Field == "_engineState");
        AssertJsonBoolean(form, "approved", true);
        AssertJsonString(form, "costCenter", "IT");
    }

    private static void AssertJsonBoolean(AsterERP.Contracts.Workflows.WorkflowSubmittedFormResponse form, string field, bool expected)
    {
        var value = GetJsonValue(form, field);
        Assert.Equal(expected, value.GetBoolean());
    }

    private static void AssertJsonNumber(AsterERP.Contracts.Workflows.WorkflowSubmittedFormResponse form, string field, int expected)
    {
        var value = GetJsonValue(form, field);
        Assert.Equal(expected, value.GetInt32());
    }

    private static void AssertJsonString(AsterERP.Contracts.Workflows.WorkflowSubmittedFormResponse form, string field, string expected)
    {
        var value = GetJsonValue(form, field);
        Assert.Equal(expected, value.GetString());
    }

    private static JsonElement GetJsonValue(AsterERP.Contracts.Workflows.WorkflowSubmittedFormResponse form, string field)
    {
        var item = Assert.Single(form.Fields, value => value.Field == field);
        return Assert.IsType<JsonElement>(item.Value);
    }
}
