using AsterERP.Api.Application.Ai.Flowise;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class FlowiseFlowDataValidatorTests
{
    [Fact]
    public void Validate_reports_disconnected_non_sticky_nodes_as_warning()
    {
        var validator = new FlowiseFlowDataValidator();

        var result = validator.Validate("""
        {
          "nodes": [
            {
              "id": "startAgentflow_0",
              "data": {
                "name": "startAgentflow",
                "label": "Start",
                "inputs": {},
                "inputParams": []
              }
            },
            {
              "id": "runtimeDataModel_0",
              "data": {
                "name": "runtimeDataModel",
                "label": "Runtime Data Model",
                "inputs": {},
                "inputParams": []
              }
            },
            {
              "id": "stickyNoteAgentflow_0",
              "data": {
                "name": "stickyNoteAgentflow",
                "label": "Sticky Note",
                "inputs": {},
                "inputParams": []
              }
            }
          ],
          "edges": []
        }
        """);

        Assert.True(result.Valid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "disconnected_flow_node" &&
            issue.NodeId == "startAgentflow_0" &&
            issue.Severity == "warning");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "disconnected_flow_node" &&
            issue.NodeId == "runtimeDataModel_0" &&
            issue.Severity == "warning");
        Assert.DoesNotContain(result.Issues, issue => issue.NodeId == "stickyNoteAgentflow_0");
    }

    [Fact]
    public void Validate_reports_visible_missing_required_inputs_as_warning()
    {
        var validator = new FlowiseFlowDataValidator();

        var result = validator.Validate("""
        {
          "nodes": [
            {
              "id": "runtimeDataModel_0",
              "data": {
                "name": "runtimeDataModel",
                "label": "Runtime Data Model",
                "inputs": {
                  "mode": "query"
                },
                "inputParams": [
                  {
                    "name": "modelCode",
                    "label": "Model Code"
                  },
                  {
                    "name": "hiddenValue",
                    "label": "Hidden Value",
                    "hide": {
                      "mode": "query"
                    }
                  },
                  {
                    "name": "optionalValue",
                    "label": "Optional Value",
                    "optional": true
                  }
                ]
              }
            }
          ],
          "edges": []
        }
        """);

        Assert.True(result.Valid);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "missing_required_node_input" &&
            issue.NodeId == "runtimeDataModel_0" &&
            issue.Message.Contains("Model Code", StringComparison.Ordinal) &&
            issue.Severity == "warning");
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains("Hidden Value", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Issues, issue => issue.Message.Contains("Optional Value", StringComparison.Ordinal));
    }
}
