using AsterERP.Api.Modules.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.AiCenter;

internal static class FlowiseOfficialDemoAgentflowSeed
{
    private const string SystemTenantId = "tenant-system";
    private const string SystemAppCode = "SYSTEM";
    private const string TemplateKeyMarker = "\"templateKey\":\"supervisor-workers-demo\"";
    private const string DemoName = "Supervisor & Workers Demo";
    private const string DemoCategory = "Official Demo";
    private const string MetadataJson = """
{"source":"flowise-official-demo","templateKey":"supervisor-workers-demo","officialReferences":["agentflowv2","supervisor-and-workers"],"nodeMapping":{"startAgentflow":"Start","llmAgentflow":"Supervisor","conditionAgentflow":"Router","agentAgentflow":["Research Worker","Planner Worker","Reviewer Worker"],"directReplyAgentflow":"Final response"}}
""";

    public static void Upsert(ISqlSugarClient db, string tenantId, string appCode)
    {
        if (!string.Equals(tenantId, SystemTenantId, StringComparison.Ordinal) ||
            !string.Equals(appCode, SystemAppCode, StringComparison.Ordinal))
        {
            return;
        }

        var workspace = db.Queryable<FlowiseWorkspaceEntity>()
            .First(item => !item.IsDeleted && item.TenantId == tenantId && item.AppCode == appCode && item.WorkspaceKey == "default");
        if (workspace is null)
        {
            return;
        }

        var modelConfigId = ResolveDemoModelConfigId(db, tenantId, appCode);
        var flowData = BuildFlowData(modelConfigId);
        var demoRows = db.Queryable<FlowiseChatFlowEntity>()
            .Where(item => item.Type == FlowiseChatflowTypes.Agentflow && item.MetadataJson.Contains(TemplateKeyMarker))
            .ToList();
        var existing = demoRows.FirstOrDefault(item =>
            string.Equals(item.TenantId, tenantId, StringComparison.Ordinal) &&
            string.Equals(item.AppCode, appCode, StringComparison.Ordinal));
        RetireDuplicateDemoRows(db, demoRows, existing?.Id);

        if (existing is null)
        {
            db.Insertable(new FlowiseChatFlowEntity
            {
                TenantId = tenantId,
                AppCode = appCode,
                OwnerUserId = "system",
                WorkspaceId = workspace.Id,
                Name = DemoName,
                FlowData = flowData,
                Type = FlowiseChatflowTypes.Agentflow,
                Deployed = true,
                IsPublic = false,
                Category = DemoCategory,
                MetadataJson = MetadataJson
            }).ExecuteCommand();
            return;
        }

        if (existing.WorkspaceId == workspace.Id &&
            existing.OwnerUserId == "system" &&
            existing.Name == DemoName &&
            existing.FlowData == flowData &&
            existing.Deployed &&
            !existing.IsPublic &&
            existing.Category == DemoCategory &&
            existing.MetadataJson == MetadataJson &&
            !existing.IsDeleted)
        {
            return;
        }

        existing.WorkspaceId = workspace.Id;
        existing.OwnerUserId = "system";
        existing.Name = DemoName;
        existing.FlowData = flowData;
        existing.Type = FlowiseChatflowTypes.Agentflow;
        existing.Deployed = true;
        existing.IsPublic = false;
        existing.Category = DemoCategory;
        existing.MetadataJson = MetadataJson;
        existing.IsDeleted = false;
        existing.DeletedTime = null;
        existing.UpdatedTime = DateTime.UtcNow;
        db.Updateable(existing).ExecuteCommand();
    }

    private static void RetireDuplicateDemoRows(
        ISqlSugarClient db,
        IReadOnlyCollection<FlowiseChatFlowEntity> demoRows,
        string? activeId)
    {
        var duplicateIds = demoRows
            .Where(item => activeId is null || !string.Equals(item.Id, activeId, StringComparison.Ordinal))
            .Select(item => item.Id)
            .ToList();
        if (duplicateIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.Updateable<FlowiseChatFlowEntity>()
            .SetColumns(item => new FlowiseChatFlowEntity
            {
                IsDeleted = true,
                DeletedBy = "system",
                DeletedTime = now,
                UpdatedTime = now
            })
            .Where(item => duplicateIds.Contains(item.Id))
            .ExecuteCommand();
    }

    private static string? ResolveDemoModelConfigId(ISqlSugarClient db, string tenantId, string appCode)
    {
        var deepSeekModel = db.Queryable<AiModelConfigEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.IsEnabled &&
                item.TenantId == tenantId &&
                item.AppCode == appCode &&
                item.ModelCode == "deepseek-v4-pro")
            .OrderBy(item => item.SortOrder)
            .OrderBy(item => item.CreatedTime)
            .First();
        if (deepSeekModel is not null)
        {
            return deepSeekModel.Id;
        }

        return db.Queryable<AiModelConfigEntity>()
            .Where(item =>
                !item.IsDeleted &&
                item.IsEnabled &&
                item.TenantId == tenantId &&
                item.AppCode == appCode)
            .OrderBy(item => item.SortOrder)
            .OrderBy(item => item.CreatedTime)
            .First()
            ?.Id;
    }

    private static string BuildFlowData(string? modelConfigId) => """
{
  "nodes": [
    {
      "id": "startAgentflow_0",
      "type": "flowiseWorkflowNode",
      "position": { "x": -760, "y": 120 },
      "data": {
        "id": "startAgentflow_0",
        "label": "Start",
        "displayName": "Start",
        "name": "startAgentflow",
        "nodeType": "startAgentflow",
        "type": "Start",
        "category": "Agent Flows",
        "description": "Starting point of the official Supervisor and Workers demo.",
        "color": "#7EE787",
        "inputs": {
          "startInputType": "chatInput",
          "startPersistState": true,
          "startState": [
            { "key": "next", "value": "PLANNER" },
            { "key": "instruction", "value": "Route the user request to the best worker." }
          ]
        },
        "config": {
          "startInputType": "chatInput",
          "startPersistState": true,
          "startState": [
            { "key": "next", "value": "PLANNER" },
            { "key": "instruction", "value": "Route the user request to the best worker." }
          ]
        },
        "selected": false,
        "version": 1,
        "outputAnchors": [
          { "id": "startAgentflow_0-output-output", "name": "output", "label": "Output", "type": "Workflow" }
        ]
      }
    },
    {
      "id": "llmAgentflow_supervisor",
      "type": "flowiseWorkflowNode",
      "position": { "x": -430, "y": 120 },
      "data": {
        "id": "llmAgentflow_supervisor",
        "label": "Supervisor",
        "displayName": "Supervisor",
        "name": "llmAgentflow",
        "nodeType": "llmAgentflow",
        "type": "LLM",
        "category": "Agent Flows",
        "description": "Decides which worker should handle the request.",
        "color": "#64B5F6",
        "inputs": {
          "llmModel": "__MODEL_CONFIG_ID__",
          "llmModelConfigId": "__MODEL_CONFIG_ID__",
          "llmEnableMemory": true,
          "llmMemoryType": "windowSize",
          "llmMemoryWindowSize": 4,
          "llmReturnResponseAs": "userMessage",
          "llmMessages": [
            {
              "role": "system",
              "content": "You are the Supervisor in a Flowise Agentflow V2 style workflow. Classify the user request and prepare concise routing instructions for Research, Planner, or Reviewer. Return useful guidance for downstream workers."
            }
          ],
          "llmUserMessage": "User request: {{$question}}\n\nChoose the best worker and provide concrete instructions. If the user asks for an ERP implementation plan, route to Planner."
        },
        "config": {
          "llmModel": "__MODEL_CONFIG_ID__",
          "llmModelConfigId": "__MODEL_CONFIG_ID__",
          "llmEnableMemory": true,
          "llmMemoryType": "windowSize",
          "llmMemoryWindowSize": 4,
          "llmReturnResponseAs": "userMessage",
          "llmStructuredOutput": [],
          "llmUpdateState": [],
          "llmMessages": [
            {
              "role": "system",
              "content": "You are the Supervisor in a Flowise Agentflow V2 style workflow. Classify the user request and prepare concise routing instructions for Research, Planner, or Reviewer. Return useful guidance for downstream workers."
            }
          ],
          "llmUserMessage": "User request: {{$question}}\n\nChoose the best worker and provide concrete instructions. If the user asks for an ERP implementation plan, route to Planner."
        },
        "selected": false,
        "version": 1,
        "inputAnchors": [
          { "id": "llmAgentflow_supervisor-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ],
        "outputAnchors": [
          { "id": "llmAgentflow_supervisor-output-output", "name": "output", "label": "Output", "type": "Workflow" }
        ]
      }
    },
    {
      "id": "conditionAgentflow_router",
      "type": "flowiseWorkflowNode",
      "position": { "x": -100, "y": 120 },
      "data": {
        "id": "conditionAgentflow_router",
        "label": "Route Request",
        "displayName": "Route Request",
        "name": "conditionAgentflow",
        "nodeType": "conditionAgentflow",
        "type": "Condition",
        "category": "Agent Flows",
        "description": "Routes to Reviewer, Researcher, or Planner.",
        "color": "#A78BFA",
        "inputs": {
          "conditions": [
            { "operation": "contains", "value1": "$question", "value2": "风险" },
            { "operation": "regex", "value1": "$question", "value2": "调研|现状|资料|研究" }
          ]
        },
        "config": {
          "conditions": [
            { "operation": "contains", "value1": "$question", "value2": "����" },
            { "operation": "regex", "value1": "$question", "value2": "����|��״|����|�о�" }
          ]
        },
        "selected": false,
        "version": 1,
        "inputAnchors": [
          { "id": "conditionAgentflow_router-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ],
        "outputAnchors": [
          { "id": "conditionAgentflow_router-output-0", "name": "reviewer", "label": "Reviewer" },
          { "id": "conditionAgentflow_router-output-1", "name": "research", "label": "Research" },
          { "id": "conditionAgentflow_router-output-2", "name": "planner", "label": "Planner" }
        ]
      }
    },
    {
      "id": "agentAgentflow_reviewer",
      "type": "flowiseWorkflowNode",
      "position": { "x": 250, "y": -90 },
      "data": {
        "id": "agentAgentflow_reviewer",
        "label": "Reviewer Worker",
        "displayName": "Reviewer Worker",
        "name": "agentAgentflow",
        "nodeType": "agentAgentflow",
        "type": "Agent",
        "category": "Agent Flows",
        "description": "Reviews risks, missing controls, and rollout concerns.",
        "color": "#F59E0B",
        "inputs": {
          "agentModel": "__MODEL_CONFIG_ID__",
          "agentModelConfigId": "__MODEL_CONFIG_ID__",
          "agentReturnResponseAs": "userMessage",
          "agentMessages": [
            {
              "role": "system",
              "content": "You are the Reviewer Worker. Review the Supervisor's guidance and identify operational, data, permission, and rollout risks. Be concrete and concise."
            }
          ],
          "agentUserMessage": "Original request: {{$question}}\nSupervisor guidance: $llmAgentflow_supervisor.output.content\n\nReturn a review checklist and mitigation plan."
        },
        "config": {
          "agentModel": "__MODEL_CONFIG_ID__",
          "agentModelConfigId": "__MODEL_CONFIG_ID__",
          "agentReturnResponseAs": "userMessage",
          "agentMessages": [
            {
              "role": "system",
              "content": "You are the Reviewer Worker. Review the Supervisor's guidance and identify operational, data, permission, and rollout risks. Be concrete and concise."
            }
          ],
          "agentUserMessage": "Original request: {{$question}}\nSupervisor guidance: $llmAgentflow_supervisor.output.content\n\nReturn a review checklist and mitigation plan.",
          "agentTools": [],
          "agentKnowledgeDocumentStores": [],
          "agentKnowledgeVSEmbeddings": [],
          "agentStructuredOutput": [],
          "agentUpdateState": []
        },
        "selected": false,
        "version": 1,
        "inputAnchors": [
          { "id": "agentAgentflow_reviewer-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ],
        "outputAnchors": [
          { "id": "agentAgentflow_reviewer-output-output", "name": "output", "label": "Output", "type": "Workflow" }
        ]
      }
    },
    {
      "id": "agentAgentflow_research",
      "type": "flowiseWorkflowNode",
      "position": { "x": 250, "y": 120 },
      "data": {
        "id": "agentAgentflow_research",
        "label": "Research Worker",
        "displayName": "Research Worker",
        "name": "agentAgentflow",
        "nodeType": "agentAgentflow",
        "type": "Agent",
        "category": "Agent Flows",
        "description": "Summarizes context and facts before planning.",
        "color": "#10B981",
        "inputs": {
          "agentModel": "__MODEL_CONFIG_ID__",
          "agentModelConfigId": "__MODEL_CONFIG_ID__",
          "agentReturnResponseAs": "userMessage",
          "agentMessages": [
            {
              "role": "system",
              "content": "You are the Research Worker. Summarize known facts, assumptions, unknowns, and data needed before making a decision."
            }
          ],
          "agentUserMessage": "Original request: {{$question}}\nSupervisor guidance: $llmAgentflow_supervisor.output.content\n\nReturn research notes for an ERP team."
        },
        "config": {
          "agentModel": "__MODEL_CONFIG_ID__",
          "agentModelConfigId": "__MODEL_CONFIG_ID__",
          "agentReturnResponseAs": "userMessage",
          "agentMessages": [
            {
              "role": "system",
              "content": "You are the Research Worker. Summarize known facts, assumptions, unknowns, and data needed before making a decision."
            }
          ],
          "agentUserMessage": "Original request: {{$question}}\nSupervisor guidance: $llmAgentflow_supervisor.output.content\n\nReturn research notes for an ERP team.",
          "agentTools": [],
          "agentKnowledgeDocumentStores": [],
          "agentKnowledgeVSEmbeddings": [],
          "agentStructuredOutput": [],
          "agentUpdateState": []
        },
        "selected": false,
        "version": 1,
        "inputAnchors": [
          { "id": "agentAgentflow_research-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ],
        "outputAnchors": [
          { "id": "agentAgentflow_research-output-output", "name": "output", "label": "Output", "type": "Workflow" }
        ]
      }
    },
    {
      "id": "agentAgentflow_planner",
      "type": "flowiseWorkflowNode",
      "position": { "x": 250, "y": 330 },
      "data": {
        "id": "agentAgentflow_planner",
        "label": "Planner Worker",
        "displayName": "Planner Worker",
        "name": "agentAgentflow",
        "nodeType": "agentAgentflow",
        "type": "Agent",
        "category": "Agent Flows",
        "description": "Builds an implementation plan with phases and acceptance criteria.",
        "color": "#3B82F6",
        "inputs": {
          "agentModel": "__MODEL_CONFIG_ID__",
          "agentModelConfigId": "__MODEL_CONFIG_ID__",
          "agentReturnResponseAs": "userMessage",
          "agentMessages": [
            {
              "role": "system",
              "content": "You are the Planner Worker for ERP workflows. Produce a structured, testable plan with phases, data inputs, alerts, permissions, dashboards, and validation cases."
            }
          ],
          "agentUserMessage": "Original request: {{$question}}\nSupervisor guidance: $llmAgentflow_supervisor.output.content\n\nBuild a practical ERP implementation plan. Include inventory warning rules, workflow steps, exception handling, and test cases when relevant."
        },
        "config": {
          "agentModel": "__MODEL_CONFIG_ID__",
          "agentModelConfigId": "__MODEL_CONFIG_ID__",
          "agentReturnResponseAs": "userMessage",
          "agentMessages": [
            {
              "role": "system",
              "content": "You are the Planner Worker for ERP workflows. Produce a structured, testable plan with phases, data inputs, alerts, permissions, dashboards, and validation cases."
            }
          ],
          "agentUserMessage": "Original request: {{$question}}\nSupervisor guidance: $llmAgentflow_supervisor.output.content\n\nBuild a practical ERP implementation plan. Include inventory warning rules, workflow steps, exception handling, and test cases when relevant.",
          "agentTools": [],
          "agentKnowledgeDocumentStores": [],
          "agentKnowledgeVSEmbeddings": [],
          "agentStructuredOutput": [],
          "agentUpdateState": []
        },
        "selected": false,
        "version": 1,
        "inputAnchors": [
          { "id": "agentAgentflow_planner-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ],
        "outputAnchors": [
          { "id": "agentAgentflow_planner-output-output", "name": "output", "label": "Output", "type": "Workflow" }
        ]
      }
    },
    {
      "id": "directReplyAgentflow_review",
      "type": "flowiseWorkflowNode",
      "position": { "x": 620, "y": -90 },
      "data": {
        "id": "directReplyAgentflow_review",
        "label": "Reviewer Response",
        "displayName": "Reviewer Response",
        "name": "directReplyAgentflow",
        "nodeType": "directReplyAgentflow",
        "type": "Direct Reply",
        "category": "Agent Flows",
        "description": "Returns the reviewer worker output.",
        "color": "#FB923C",
        "inputs": {
          "directReplyMessage": "Supervisor & Workers Demo routed this request to Reviewer.\n\n$agentAgentflow_reviewer.output.content"
        },
        "inputAnchors": [
          { "id": "directReplyAgentflow_review-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ]
      }
    },
    {
      "id": "directReplyAgentflow_research",
      "type": "flowiseWorkflowNode",
      "position": { "x": 620, "y": 120 },
      "data": {
        "id": "directReplyAgentflow_research",
        "label": "Research Response",
        "displayName": "Research Response",
        "name": "directReplyAgentflow",
        "nodeType": "directReplyAgentflow",
        "type": "Direct Reply",
        "category": "Agent Flows",
        "description": "Returns the research worker output.",
        "color": "#34D399",
        "inputs": {
          "directReplyMessage": "Supervisor & Workers Demo routed this request to Research.\n\n$agentAgentflow_research.output.content"
        },
        "inputAnchors": [
          { "id": "directReplyAgentflow_research-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ]
      }
    },
    {
      "id": "directReplyAgentflow_plan",
      "type": "flowiseWorkflowNode",
      "position": { "x": 620, "y": 330 },
      "data": {
        "id": "directReplyAgentflow_plan",
        "label": "Planner Response",
        "displayName": "Planner Response",
        "name": "directReplyAgentflow",
        "nodeType": "directReplyAgentflow",
        "type": "Direct Reply",
        "category": "Agent Flows",
        "description": "Returns the planner worker output.",
        "color": "#60A5FA",
        "inputs": {
          "directReplyMessage": "Supervisor & Workers Demo routed this request to Planner.\n\n$agentAgentflow_planner.output.content"
        },
        "inputAnchors": [
          { "id": "directReplyAgentflow_plan-input-input", "name": "input", "label": "Input", "type": "Workflow" }
        ]
      }
    }
  ],
  "edges": [
    {
      "id": "startAgentflow_0-startAgentflow_0-output-startAgentflow-llmAgentflow_supervisor-llmAgentflow_supervisor",
      "source": "startAgentflow_0",
      "sourceHandle": "startAgentflow_0-output-output",
      "target": "llmAgentflow_supervisor",
      "targetHandle": "llmAgentflow_supervisor-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "data": { "label": "Start" }
    },
    {
      "id": "llmAgentflow_supervisor-llmAgentflow_supervisor-output-llmAgentflow-conditionAgentflow_router-conditionAgentflow_router",
      "source": "llmAgentflow_supervisor",
      "sourceHandle": "llmAgentflow_supervisor-output-output",
      "target": "conditionAgentflow_router",
      "targetHandle": "conditionAgentflow_router-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "data": { "label": "Supervisor decision" }
    },
    {
      "id": "conditionAgentflow_router-conditionAgentflow_router-output-0-agentAgentflow_reviewer-agentAgentflow_reviewer",
      "source": "conditionAgentflow_router",
      "sourceHandle": "conditionAgentflow_router-output-0",
      "target": "agentAgentflow_reviewer",
      "targetHandle": "agentAgentflow_reviewer-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "label": "Risk review",
      "data": { "conditionLabel": "Risk review", "label": "Risk review" }
    },
    {
      "id": "conditionAgentflow_router-conditionAgentflow_router-output-1-agentAgentflow_research-agentAgentflow_research",
      "source": "conditionAgentflow_router",
      "sourceHandle": "conditionAgentflow_router-output-1",
      "target": "agentAgentflow_research",
      "targetHandle": "agentAgentflow_research-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "label": "Research",
      "data": { "conditionLabel": "Research", "label": "Research" }
    },
    {
      "id": "conditionAgentflow_router-conditionAgentflow_router-output-2-agentAgentflow_planner-agentAgentflow_planner",
      "source": "conditionAgentflow_router",
      "sourceHandle": "conditionAgentflow_router-output-2",
      "target": "agentAgentflow_planner",
      "targetHandle": "agentAgentflow_planner-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "label": "Plan",
      "data": { "conditionLabel": "Plan", "label": "Plan" }
    },
    {
      "id": "agentAgentflow_reviewer-agentAgentflow_reviewer-output-agentAgentflow-directReplyAgentflow_review-directReplyAgentflow_review",
      "source": "agentAgentflow_reviewer",
      "sourceHandle": "agentAgentflow_reviewer-output-output",
      "target": "directReplyAgentflow_review",
      "targetHandle": "directReplyAgentflow_review-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "data": { "label": "Reply" }
    },
    {
      "id": "agentAgentflow_research-agentAgentflow_research-output-agentAgentflow-directReplyAgentflow_research-directReplyAgentflow_research",
      "source": "agentAgentflow_research",
      "sourceHandle": "agentAgentflow_research-output-output",
      "target": "directReplyAgentflow_research",
      "targetHandle": "directReplyAgentflow_research-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "data": { "label": "Reply" }
    },
    {
      "id": "agentAgentflow_planner-agentAgentflow_planner-output-agentAgentflow-directReplyAgentflow_plan-directReplyAgentflow_plan",
      "source": "agentAgentflow_planner",
      "sourceHandle": "agentAgentflow_planner-output-output",
      "target": "directReplyAgentflow_plan",
      "targetHandle": "directReplyAgentflow_plan-input-input",
      "type": "flowiseWorkflowEdge",
      "animated": true,
      "data": { "label": "Reply" }
    }
  ],
  "viewport": { "x": 180, "y": 80, "zoom": 0.75 }
}
""".Replace("__MODEL_CONFIG_ID__", modelConfigId ?? string.Empty, StringComparison.Ordinal);
}
