import type { Connection } from '@xyflow/react';
import { describe, expect, it } from 'vitest';


import type { FlowiseCanvasEdge, FlowiseCanvasNode } from '../types/canvas.types';

import {
  applyNodeInputChange,
  canConnectFlowiseNodes,
  createFlowiseEdge,
  createFlowiseNodeLabel,
  createNodeFromCatalog,
  deleteFlowiseEdgeWithInputCleanup,
  deleteFlowiseNodeWithConnections,
  duplicateFlowiseNode,
  hasStartAgentflowNode,
  parseFlowDataString,
  placeWorkflowNode,
  prepareFlowiseNodeForSave,
  syncFlowiseNodesWithCatalog
} from './FlowiseCanvasModel';

function node(id: string, nodeType: string, parentId?: string): FlowiseCanvasNode {
  return {
    data: {
      config: {},
      displayName: nodeType,
      nodeType
    },
    id,
    parentId,
    position: { x: 0, y: 0 },
    type: 'flowiseWorkflowNode'
  };
}

describe('FlowiseCanvasModel Agentflow connections', () => {
  it('rejects self connections and directed cycles like Flowise Agentflow', () => {
    const nodes = [node('conditionAgentflow_0', 'conditionAgentflow'), node('agentAgentflow_0', 'agentAgentflow')];
    const existingEdges: FlowiseCanvasEdge[] = [
      {
        data: {},
        id: 'conditionAgentflow_0-conditionAgentflow_0-output-conditionAgentflow-0-agentAgentflow_0-agentAgentflow_0-input-agentAgentflow',
        source: 'conditionAgentflow_0',
        sourceHandle: 'conditionAgentflow_0-output-conditionAgentflow-0',
        target: 'agentAgentflow_0',
        targetHandle: 'agentAgentflow_0-input-agentAgentflow'
      }
    ];

    expect(canConnectFlowiseNodes({
      source: 'conditionAgentflow_0',
      sourceHandle: 'conditionAgentflow_0-output-conditionAgentflow-0',
      target: 'conditionAgentflow_0',
      targetHandle: 'conditionAgentflow_0-input-conditionAgentflow'
    }, nodes, [], 'agentflow')).toBe(false);

    expect(canConnectFlowiseNodes({
      source: 'agentAgentflow_0',
      sourceHandle: 'agentAgentflow_0-output-agentAgentflow-output',
      target: 'conditionAgentflow_0',
      targetHandle: 'conditionAgentflow_0-input-conditionAgentflow'
    }, nodes, existingEdges, 'agentflow')).toBe(false);
  });

  it('creates source-compatible condition edge metadata', () => {
    const connection: Connection = {
      source: 'conditionAgentflow_0',
      sourceHandle: 'conditionAgentflow_0-output-conditionAgentflow-1',
      target: 'agentAgentflow_0',
      targetHandle: 'agentAgentflow_0-input-agentAgentflow'
    };

    const edge = createFlowiseEdge(connection, 'agentflow');

    expect(edge).toMatchObject({
      data: {
        conditionLabel: '1',
        edgeLabel: '1',
        humanInputLabel: null,
        isHumanInput: false,
        sourceColor: '#7c3aed'
      },
      id: 'conditionAgentflow_0-conditionAgentflow_0-output-conditionAgentflow-1-agentAgentflow_0-agentAgentflow_0-input-agentAgentflow',
      label: '1',
      type: 'flowiseWorkflowEdge'
    });
  });

  it('creates source-compatible human input edge metadata', () => {
    const proceedEdge = createFlowiseEdge({
      source: 'humanInputAgentflow_0',
      sourceHandle: 'humanInputAgentflow_0-output-humanInputAgentflow-0',
      target: 'agentAgentflow_0',
      targetHandle: 'agentAgentflow_0-input-agentAgentflow'
    }, 'agentflow');
    const rejectEdge = createFlowiseEdge({
      source: 'humanInputAgentflow_0',
      sourceHandle: 'humanInputAgentflow_0-output-humanInputAgentflow-1',
      target: 'agentAgentflow_0',
      targetHandle: 'agentAgentflow_0-input-agentAgentflow'
    }, 'agentflow');

    expect(proceedEdge?.data).toMatchObject({
      edgeLabel: 'proceed',
      humanInputLabel: 'proceed',
      isHumanInput: true,
      sourceColor: '#f97316'
    });
    expect(rejectEdge?.data).toMatchObject({
      edgeLabel: 'reject',
      humanInputLabel: 'reject',
      isHumanInput: true
    });
  });

  it('marks edges inside the same iteration parent with high z-index', () => {
    const nodes = [
      node('agentAgentflow_0', 'agentAgentflow', 'iteration_0'),
      node('conditionAgentflow_0', 'conditionAgentflow', 'iteration_0')
    ];

    const edge = createFlowiseEdge({
      source: 'conditionAgentflow_0',
      sourceHandle: 'conditionAgentflow_0-output-conditionAgentflow-0',
      target: 'agentAgentflow_0',
      targetHandle: 'agentAgentflow_0-input-agentAgentflow'
    }, 'agentflow', nodes);

    expect(edge?.data?.isWithinIterationNode).toBe(true);
    expect(edge?.zIndex).toBe(9999);
  });

  it('updates Flowise-style inputs and removes hidden input values when params change', () => {
    const startNode: FlowiseCanvasNode = {
      data: {
        config: { cronExpression: '0 * * * *', startInputType: 'scheduleInput' },
        displayName: 'Start Agentflow',
        inputParams: [
          {
            label: 'Start Input Type',
            name: 'startInputType',
            options: [
              { label: 'Chat Input', name: 'chatInput' },
              { label: 'Schedule Input', name: 'scheduleInput' }
            ],
            type: 'options'
          },
          {
            label: 'Cron',
            name: 'cronExpression',
            show: { startInputType: 'scheduleInput' },
            type: 'string'
          }
        ],
        inputs: { cronExpression: '0 * * * *', startInputType: 'scheduleInput' },
        nodeType: 'startAgentflow'
      },
      id: 'startAgentflow_0',
      position: { x: 0, y: 0 },
      type: 'flowiseWorkflowNode'
    };

    const updated = applyNodeInputChange(startNode, 'startInputType', 'chatInput');

    expect(updated.data.inputs).toMatchObject({ startInputType: 'chatInput' });
    expect(updated.data.config).toMatchObject({ startInputType: 'chatInput' });
    expect(updated.data.inputs?.cronExpression).toBeUndefined();
    expect(updated.data.inputParams?.find((param) => param.name === 'cronExpression')?.display).toBe(false);
  });

  it('initializes catalog nodes with Flowise-style id, label, inputs and output anchors', () => {
    const created = createNodeFromCatalog({
      baseClasses: ['Agentflow'],
      category: 'Agentflow',
      description: 'Wait for human input.',
      displayName: 'Human Input',
      inputParams: [
        { default: 'Approve?', label: 'Message', name: 'message', type: 'string' },
        { label: 'Agent', name: 'agentAgentflow', type: 'Agentflow' as never }
      ],
      nodeType: 'humanInputAgentflow',
      outputAnchors: [],
      version: 1
    }, { x: 10, y: 20 }, []);

    expect(created.id).toBe('humanInputAgentflow_0');
    expect(created.data.name).toBe('humanInputAgentflow');
    expect(created.data.label).toBe('Human Input 0');
    expect(created.data.inputs).toMatchObject({ message: 'Approve?', agentAgentflow: '' });
    expect(created.data.config).toMatchObject({ message: 'Approve?', agentAgentflow: '' });
    expect(created.data.inputParams?.map((param) => param.name)).toEqual(['message']);
    expect(created.data.inputAnchors?.map((anchor) => anchor.name)).toEqual(['agentAgentflow']);
    expect(created.data.outputAnchors?.[0]?.id).toBe('humanInputAgentflow_0-output-humanInputAgentflow');
  });

  it('initializes catalog defaults from backend defaultJson and keeps Flowise start node metadata', () => {
    const created = createNodeFromCatalog({
      baseClasses: ['Agentflow'],
      category: 'Agentflow',
      description: 'Start trigger.',
      displayName: 'Start Agentflow',
      inputParams: [
        {
          defaultJson: '"chatInput"',
          label: 'Start Input Type',
          name: 'startInputType',
          options: [
            { label: 'Chat Input', value: 'chatInput' },
            { label: 'Schedule Input', value: 'scheduleInput' }
          ],
          type: 'options'
        },
        {
          defaultJson: '"* * * * *"',
          label: 'Cron Expression',
          name: 'cronExpression',
          show: { startInputType: 'scheduleInput' },
          type: 'string'
        }
      ],
      nodeType: 'startAgentflow',
      outputAnchors: [{ label: 'Output', name: 'output', type: 'Agentflow' }],
      version: 1
    }, { x: 100, y: 100 }, []);

    expect(created.id).toBe('startAgentflow_0');
    expect(created.data.name).toBe('startAgentflow');
    expect(created.data.label).toBe('Start Agentflow');
    expect(created.data.inputs).toMatchObject({
      cronExpression: '* * * * *',
      startInputType: 'chatInput'
    });
    expect(created.data.inputParams?.find((param) => param.name === 'cronExpression')?.display).toBe(false);
    expect(created.data.outputAnchors?.[0]?.id).toBe('startAgentflow_0-output-output');
  });

  it('keeps Flowise Agentflow start nodes unique and labels non-start nodes by next suffix', () => {
    const existing = [node('startAgentflow_0', 'startAgentflow'), node('agentAgentflow_0', 'agentAgentflow')];

    expect(hasStartAgentflowNode(existing)).toBe(true);
    expect(createFlowiseNodeLabel({
      category: 'Agentflow',
      description: '',
      displayName: 'Agent',
      nodeType: 'agentAgentflow'
    }, existing)).toBe('Agent 1');
    expect(createFlowiseNodeLabel({
      category: 'Agentflow',
      description: '',
      displayName: 'Start Agentflow',
      nodeType: 'startAgentflow'
    }, existing)).toBe('Start Agentflow');
  });

  it('places dropped nodes inside iteration containers and rejects unsupported nested drops', () => {
    const iteration = {
      ...node('iterationAgentflow_0', 'iterationAgentflow'),
      height: 250,
      position: { x: 100, y: 100 },
      type: 'flowiseIterationNode',
      width: 300
    } satisfies FlowiseCanvasNode;
    const agent = node('agentAgentflow_0', 'agentAgentflow');
    const nestedIteration = node('iterationAgentflow_1', 'iterationAgentflow');
    const humanInput = node('humanInputAgentflow_0', 'humanInputAgentflow');

    const placed = placeWorkflowNode(agent, [iteration], { x: 180, y: 190 });
    expect(placed.reason).toBeUndefined();
    expect(placed.node.parentId).toBe('iterationAgentflow_0');
    expect((placed.node as FlowiseCanvasNode & { parentNode?: string }).parentNode).toBe('iterationAgentflow_0');
    expect(placed.node.position).toEqual({ x: 80, y: 90 });

    expect(placeWorkflowNode(nestedIteration, [iteration], { x: 180, y: 190 }).reason).toBe('nestedIteration');
    expect(placeWorkflowNode(humanInput, [iteration], { x: 180, y: 190 }).reason).toBe('humanInputInsideIteration');
  });

  it('sanitizes nodes before save like Flowise handleSaveFlow', () => {
    const selectedCredentialNode: FlowiseCanvasNode = {
      data: {
        config: {},
        displayName: 'Credential Node',
        inputs: {
          FLOWISE_CREDENTIAL_ID: 'credential-123',
          prompt: 'hello'
        },
        nodeType: 'credentialNode',
        selected: true,
        status: 'running'
      },
      id: 'credentialNode_0',
      position: { x: 0, y: 0 },
      selected: true,
      type: 'flowiseWorkflowNode'
    };

    const sanitized = prepareFlowiseNodeForSave(selectedCredentialNode);

    expect(sanitized.selected).toBe(false);
    expect(sanitized.data.selected).toBe(false);
    expect(sanitized.data.status).toBeUndefined();
    expect(sanitized.data.credential).toBe('credential-123');
    expect(sanitized.data.inputs).toEqual({ prompt: 'hello' });
    expect(selectedCredentialNode.data.inputs?.FLOWISE_CREDENTIAL_ID).toBe('credential-123');
  });

  it('duplicates nodes with Flowise-compatible ids, labels, anchors and cleared connected inputs', () => {
    const original: FlowiseCanvasNode & { positionAbsolute?: { x: number; y: number } } = {
      data: {
        config: {},
        displayName: 'Agent',
        inputAnchors: [{ id: 'agent_0-input-source-Agentflow', label: 'Source', name: 'source', type: 'Agentflow' }],
        inputParams: [{ id: 'agent_0-input-prompt-string', label: 'Prompt', name: 'prompt', type: 'string' }],
        inputs: {
          listInput: ['{{tool_0.data.instance}}', 'manual'],
          prompt: '{{agent_1.data.instance}}'
        },
        label: 'Agent 0',
        name: 'agent',
        nodeType: 'agent',
        outputAnchors: [{ id: 'agent_0-output-agent', label: 'Agent', name: 'agent', type: 'Agentflow' }]
      },
      id: 'agent_0',
      position: { x: 100, y: 200 },
      positionAbsolute: { x: 100, y: 200 },
      type: 'flowiseWorkflowNode',
      width: 230
    };

    const duplicated = duplicateFlowiseNode(original, [original]);

    expect(duplicated.id).toBe('agent_1');
    expect(duplicated.data.id).toBe('agent_1');
    expect(duplicated.data.label).toBe('Agent 0 (1)');
    expect(duplicated.position).toEqual({ x: 380, y: 200 });
    expect((duplicated as FlowiseCanvasNode & { positionAbsolute?: { x: number; y: number } }).positionAbsolute).toEqual({ x: 380, y: 200 });
    expect(duplicated.data.inputParams?.[0]?.id).toBe('agent_1-input-prompt-string');
    expect(duplicated.data.inputAnchors?.[0]?.id).toBe('agent_1-input-source-Agentflow');
    expect(duplicated.data.outputAnchors?.[0]?.id).toBe('agent_1-output-agent');
    expect(duplicated.data.inputs).toEqual({ listInput: ['manual'], prompt: '' });
    expect(original.data.inputs).toEqual({ listInput: ['{{tool_0.data.instance}}', 'manual'], prompt: '{{agent_1.data.instance}}' });
  });

  it('uses the rendered Flowise node width fallback when duplicating before React Flow records width', () => {
    const original = node('agent_0', 'agent');
    original.data.label = 'Agent 0';
    original.data.name = 'agent';
    original.position = { x: 140, y: 120 };

    const duplicated = duplicateFlowiseNode(original, [original]);

    expect(duplicated.id).toBe('agent_1');
    expect(duplicated.position).toEqual({ x: 420, y: 120 });
  });

  it('deletes nodes with descendants and clears Flowise connected inputs', () => {
    const source = node('source_0', 'source');
    const child = node('child_0', 'child', 'source_0');
    const target: FlowiseCanvasNode = {
      ...node('target_0', 'target'),
      data: {
        config: { input: 'source_0-output', listInput: ['source_0-output', 'manual'], variableInput: '{{source_0.data.instance}} {{$source_0.output.content}} suffix' },
        displayName: 'target',
        inputAnchors: [
          { label: 'Input', name: 'input', type: 'Agentflow' },
          { label: 'List', list: true, name: 'listInput', type: 'Agentflow' }
        ],
        inputParams: [{ acceptVariable: true, label: 'Variable', name: 'variableInput', type: 'string' }],
        inputs: { input: 'source_0-output', listInput: ['source_0-output', 'manual'], variableInput: '{{source_0.data.instance}} {{$source_0.output.content}} suffix' },
        nodeType: 'target'
      }
    };
    const edges: FlowiseCanvasEdge[] = [
      { id: 'edge-input', source: 'source_0', sourceHandle: 'source_0-output-source', target: 'target_0', targetHandle: 'target_0-input-input-Agentflow' },
      { id: 'edge-list', source: 'source_0', sourceHandle: 'source_0-output-source', target: 'target_0', targetHandle: 'target_0-input-listInput-Agentflow' },
      { id: 'edge-var', source: 'source_0', sourceHandle: 'source_0-output-source', target: 'target_0', targetHandle: 'target_0-input-variableInput-string' },
      { id: 'edge-child', source: 'child_0', sourceHandle: 'child_0-output-child', target: 'target_0', targetHandle: 'target_0-input-input-Agentflow' }
    ];

    const result = deleteFlowiseNodeWithConnections('source_0', [source, child, target], edges);
    const remainingTarget = result.nodes.find((candidate) => candidate.id === 'target_0');

    expect(result.removedNodeIds.sort()).toEqual(['child_0', 'source_0']);
    expect(result.edges).toEqual([]);
    expect(remainingTarget?.data.inputs).toMatchObject({ input: '', listInput: ['manual'], variableInput: ' suffix' });
    expect(remainingTarget?.data.config).toMatchObject({ input: '', listInput: ['manual'], variableInput: ' suffix' });
  });

  it('deletes edges with input cleanup without removing nodes', () => {
    const source = node('source_0', 'source');
    const target: FlowiseCanvasNode = {
      ...node('target_0', 'target'),
      data: {
        config: { input: 'source_0-output' },
        displayName: 'target',
        inputAnchors: [{ label: 'Input', name: 'input', type: 'Agentflow' }],
        inputs: { input: 'source_0-output' },
        nodeType: 'target'
      }
    };
    const edges: FlowiseCanvasEdge[] = [
      { id: 'edge-input', source: 'source_0', sourceHandle: 'source_0-output-source', target: 'target_0', targetHandle: 'target_0-input-input-Agentflow' }
    ];

    const result = deleteFlowiseEdgeWithInputCleanup('edge-input', [source, target], edges);

    expect(result.edges).toEqual([]);
    expect(result.nodes.map((candidate) => candidate.id)).toEqual(['source_0', 'target_0']);
    expect(result.nodes.find((candidate) => candidate.id === 'target_0')?.data.inputs).toEqual({ input: '' });
  });

  it('syncs outdated loaded nodes with catalog definitions and removes invalid edges', () => {
    const source = node('source_0', 'source');
    source.data.outputAnchors = [{ id: 'source_0-output-output', label: 'Output', name: 'output', type: 'Agentflow' }];
    const outdated: FlowiseCanvasNode = {
      ...node('directReplyAgentflow_0', 'directReplyAgentflow'),
      data: {
        config: { message: 'hello' },
        displayName: 'Direct Reply',
        inputAnchors: [{ id: 'directReplyAgentflow_0-input-input-Agentflow', label: 'Input', name: 'input', type: 'Agentflow' }],
        inputParams: [{ acceptVariable: true, label: 'Message', name: 'message', type: 'string' }],
        inputs: { message: 'hello' },
        nodeType: 'directReplyAgentflow',
        outputAnchors: [{ id: 'directReplyAgentflow_0-output-output', label: 'Output', name: 'output', type: 'Agentflow' }],
        version: 1
      }
    };
    const edges: FlowiseCanvasEdge[] = [
      { id: 'valid', source: 'source_0', sourceHandle: 'source_0-output-output', target: 'directReplyAgentflow_0', targetHandle: 'directReplyAgentflow_0-input-input-Agentflow' },
      { id: 'invalid', source: 'source_0', sourceHandle: 'source_0-output-output', target: 'directReplyAgentflow_0', targetHandle: 'directReplyAgentflow_0-input-removed-Agentflow' }
    ];

    const result = syncFlowiseNodesWithCatalog([source, outdated], edges, [
      {
        baseClasses: ['Agentflow'],
        category: 'Workflow',
        description: 'Direct reply',
        displayName: 'Direct Reply',
        inputAnchors: [{ id: 'directReplyAgentflow_0-input-input-Agentflow', label: 'Input', name: 'input', type: 'Agentflow' }],
        inputParams: [
          { acceptVariable: true, default: '', label: 'Message', name: 'message', type: 'string' },
          { acceptVariable: true, default: '[]', label: 'Follow Ups', name: 'followUps', type: 'array' }
        ],
        nodeType: 'directReplyAgentflow',
        outputAnchors: [{ id: 'directReplyAgentflow_0-output-output', label: 'Output', name: 'output', type: 'Agentflow' }],
        tags: ['Workflow'],
        version: 2
      }
    ]);

    const synced = result.nodes.find((candidate) => candidate.id === 'directReplyAgentflow_0');
    expect(result.changed).toBe(true);
    expect(result.edges.map((edge) => edge.id)).toEqual(['valid']);
    expect(synced?.data.version).toBe(2);
    expect(synced?.data.inputs).toMatchObject({ followUps: '[]', message: 'hello' });
    expect(synced?.data.inputParams?.map((param) => param.name)).toEqual(['message', 'followUps']);
  });

  it('normalizes persisted Flowise workflow renderer types before React Flow renders', () => {
    const parsed = parseFlowDataString(JSON.stringify({
      nodes: [
        {
          data: {
            displayName: 'Runtime Data Model',
            inputAnchors: [{ label: 'Input', name: 'input', type: 'Agentflow' }],
            nodeType: 'runtimeDataModel',
            name: 'runtimeDataModel'
          },
          id: 'runtimeDataModel_0',
          position: { x: 320, y: 120 },
          type: 'flowiseAgentFlowNode'
        },
        {
          data: {
            displayName: 'Sticky Note',
            nodeType: 'stickyNote',
            stickyNote: true
          },
          id: 'stickyNote_0',
          position: { x: 40, y: 40 },
          type: 'flowiseAgentFlowNode'
        }
      ],
      edges: [
        {
          id: 'startAgentflow_0-output-output-runtimeDataModel_0-input-input',
          source: 'startAgentflow_0',
          sourceHandle: 'startAgentflow_0-output-output',
          target: 'runtimeDataModel_0',
          targetHandle: 'runtimeDataModel_0-input-input',
          type: 'buttonedge'
        }
      ]
    }));

    expect(parsed.nodes.map((item) => item.type)).toEqual(['flowiseWorkflowNode', 'flowiseStickyNote']);
    expect(parsed.nodes[0]?.data.inputAnchors?.[0]?.id).toBe('runtimeDataModel_0-input-input');
    expect(parsed.edges[0]?.type).toBe('flowiseWorkflowEdge');
  });

  it('keeps legacy Flowise edges when catalog sync renames the only available handles', () => {
    const nodes: FlowiseCanvasNode[] = [
      {
        data: {
          displayName: 'Start',
          inputAnchors: [],
          inputParams: [],
          nodeType: 'startAgentflow',
          outputAnchors: [{ id: 'startAgentflow_0-output-output', label: 'Output', name: 'output', type: 'Agentflow' }]
        },
        id: 'startAgentflow_0',
        position: { x: 0, y: 0 },
        type: 'flowiseWorkflowNode'
      },
      {
        data: {
          displayName: 'Runtime Data Model',
          inputAnchors: [{ id: 'runtimeDataModel_0-input-input', label: 'Input', name: 'input', type: 'json' }],
          inputParams: [],
          nodeType: 'runtime-data-model',
          outputAnchors: [{ id: 'runtimeDataModel_0-output-data', label: 'Data', name: 'data', type: 'json' }]
        },
        id: 'runtimeDataModel_0',
        position: { x: 300, y: 0 },
        type: 'flowiseWorkflowNode'
      }
    ];
    const edges: FlowiseCanvasEdge[] = [
      {
        data: {},
        id: 'startAgentflow_0-output-output-runtimeDataModel_0-input-input',
        source: 'startAgentflow_0',
        sourceHandle: 'startAgentflow_0-output-output',
        target: 'runtimeDataModel_0',
        targetHandle: 'runtimeDataModel_0-input-input',
        type: 'flowiseWorkflowEdge'
      }
    ];

    const synced = syncFlowiseNodesWithCatalog(nodes, edges, [
      {
        baseClasses: ['Agentflow'],
        category: 'Workflow',
        description: 'Start node',
        displayName: 'Start',
        inputParams: [],
        nodeType: 'startAgentflow',
        outputAnchors: [{ id: 'startAgentflow_0-output-startAgentflow', label: 'Output', name: 'startAgentflow', type: 'Agentflow' }],
        version: 2
      },
      {
        baseClasses: ['Tool'],
        category: 'Integration',
        description: 'Runtime data model',
        displayName: 'Runtime Data Model',
        inputAnchors: [{ id: 'runtimeDataModel_0-input-runtimeDataModel', label: 'Input', name: 'runtimeDataModel', type: 'json' }],
        inputParams: [],
        nodeType: 'runtime-data-model',
        outputAnchors: [{ id: 'runtimeDataModel_0-output-data', label: 'Data', name: 'data', type: 'json' }],
        version: 2
      }
    ]);

    expect(synced.edges).toHaveLength(1);
  });
});
