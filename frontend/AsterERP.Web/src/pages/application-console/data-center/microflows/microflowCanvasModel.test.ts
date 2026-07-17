import type { Connection } from '@xyflow/react';
import { describe, expect, it } from 'vitest';


import type { MicroflowDefinition, MicroflowValueExpression } from '../../../../api/application-data-center/applicationDataCenter.types';

import {
  addMicroflowCanvasNode,
  applyMicroflowCanvasNodePositions,
  canConnectMicroflowNodes,
  connectMicroflowNodes,
  createMicroflowCanvasEdges,
  createMicroflowCanvasNodes,
  deleteMicroflowCanvasEdge,
  deleteMicroflowCanvasNode,
  deleteMicroflowCanvasNodes,
  duplicateMicroflowCanvasNode,
  microflowCanvasNodeSize,
  updateMicroflowEdgeCondition
} from './microflowCanvasModel';
import { createDefaultMicroflowDefinition } from './microflowDefaults';
import { findGlobalVariableNodeDeleteBlockers, writeGlobalVariableNodeVariables } from './microflowGlobalVariableNode';

describe('microflowCanvasModel', () => {
  it('maps persisted microflow nodes and edges to React Flow nodes and edges', () => {
    const definition = createDefaultMicroflowDefinition('order_query');

    const nodes = createMicroflowCanvasNodes(definition, 'order_query_query');
    const edges = createMicroflowCanvasEdges(definition, 'order_query_start_order_query_query');

    expect(nodes.find((node) => node.id === 'order_query_query')?.selected).toBe(true);
    expect(nodes.find((node) => node.id === 'order_query_query')?.position).toEqual({ x: 310, y: 160 });
    expect(edges[0]).toMatchObject({
      source: 'order_query_start',
      target: 'order_query_query',
      type: 'microflowButtonEdge'
    });
  });

  it('writes dragged React Flow positions back to the microflow definition', () => {
    const definition = createDefaultMicroflowDefinition('order_query');
    const nodes = createMicroflowCanvasNodes(definition, null).map((node) =>
      node.id === 'order_query_start'
        ? { ...node, position: { x: 98.2, y: 70.8 } }
        : node.id === 'order_query_query'
          ? { ...node, position: { x: 411.4, y: 206.7 } }
          : node
    );

    const updated = applyMicroflowCanvasNodePositions(definition, nodes);

    expect(updated.nodes.find((node) => node.id === 'order_query_start')).toMatchObject({ x: 98, y: 71 });
    expect(updated.nodes.find((node) => node.id === 'order_query_query')).toMatchObject({ x: 411, y: 207 });
  });

  it('adds nodes with unique ids and node-type defaults', () => {
    const definition = createDefaultMicroflowDefinition('order_query');
    const result = addMicroflowCanvasNode(definition, 'callApi', 'Call API', { x: 10, y: 20 });

    const node = result.definition.nodes.find((item) => item.id === result.nodeId);
    expect(node).toMatchObject({
      config: { bodyMappings: [], httpMethod: 'GET', queryMappings: [], routePath: '', targetVariable: 'apiResult' },
      name: 'Call API',
      type: 'callApi',
      x: 10,
      y: 20
    });
  });

  it('adds loop nodes with variables.items array collection by default', () => {
    const definition = createDefaultMicroflowDefinition('order_query');
    const result = addMicroflowCanvasNode(definition, 'loop', 'Loop', { x: 120, y: 80 });

    const node = result.definition.nodes.find((item) => item.id === result.nodeId);
    expect(node?.config).toMatchObject({
      bodyNodeId: '',
      collectionExpression: {
        dataType: 'array',
        kind: 'ref'
      },
      itemVariable: 'item'
    });
  });

  it('adds global variable nodes as design-only nodes without allowing connections', () => {
    const definition = createDefaultMicroflowDefinition('order_query');
    const added = addMicroflowCanvasNode(definition, 'globalVariables', 'Global Variables', { x: 120, y: 80 });
    const node = added.definition.nodes.find((item) => item.id === added.nodeId);

    expect(node).toMatchObject({
      config: { variables: [] },
      name: 'Global Variables',
      type: 'globalVariables',
      x: 120,
      y: 80
    });
    expect(canConnectMicroflowNodes({ source: added.nodeId, target: 'order_query_return' } as Connection, added.definition)).toBe(false);
    expect(canConnectMicroflowNodes({ source: 'order_query_start', target: added.nodeId } as Connection, added.definition)).toBe(false);
  });

  it('syncs global variable node declarations into definition variables and removes them on delete', () => {
    const definition = createDefaultMicroflowDefinition('order_query');
    const added = addMicroflowCanvasNode(definition, 'globalVariables', 'Global Variables', { x: 120, y: 80 });
    const withVariables = writeGlobalVariableNodeVariables(added.definition, added.nodeId, [
      {
        defaultValue: 7,
        fields: [],
        schemaObjectCode: null,
        sourceNodeId: added.nodeId,
        valueType: 'number',
        variableCode: 'globalCount',
        variableName: '全局数量'
      }
    ]);

    const canvasNodes = createMicroflowCanvasNodes(withVariables, null);
    const globalNode = canvasNodes.find((node) => node.id === added.nodeId);
    expect(withVariables.variables.some((variable) => variable.variableCode === 'globalCount' && variable.sourceNodeId === added.nodeId)).toBe(true);
    expect(globalNode?.data.outputTags.map((tag) => tag.label)).toEqual(['globalCount']);

    const deleted = deleteMicroflowCanvasNode(withVariables, added.nodeId);
    expect(deleted.variables.some((variable) => variable.variableCode === 'globalCount')).toBe(false);
  });

  it('reports delete blockers when other nodes reference a global variable node', () => {
    const definition = createDefaultMicroflowDefinition('order_query');
    const added = addMicroflowCanvasNode(definition, 'globalVariables', 'Global Variables', { x: 120, y: 80 });
    const withVariables = writeGlobalVariableNodeVariables(added.definition, added.nodeId, [
      {
        defaultValue: 7,
        fields: [],
        schemaObjectCode: null,
        sourceNodeId: added.nodeId,
        valueType: 'number',
        variableCode: 'globalCount',
        variableName: '全局数量'
      }
    ]);
    const withReference: MicroflowDefinition = {
      ...withVariables,
      nodes: withVariables.nodes.map((node) => node.type === 'return'
        ? {
            ...node,
            config: {
              outputSchema: {
                fields: [
                  {
                    dataType: 'number',
                    fieldCode: 'count',
                    fieldName: '数量',
                    expression: refExpr('variables', 'globalCount', 'number'),
                    visible: true,
                    writable: false
                  }
                ],
                valueType: 'object',
                variableCode: 'result',
                variableName: '返回结果'
              }
            }
          }
        : node)
    };

    const blockers = findGlobalVariableNodeDeleteBlockers(withReference, [added.nodeId]);

    expect(blockers[0]).toContain('globalCount');
  });

  it('adds editable input and output defaults for non-start node types', () => {
    const definition = createDefaultMicroflowDefinition('order_query');

    const changed = addMicroflowCanvasNode(definition, 'change', 'Change', { x: 120, y: 80 });
    const deleted = addMicroflowCanvasNode(definition, 'delete', 'Delete', { x: 120, y: 80 });
    const called = addMicroflowCanvasNode(definition, 'callApi', 'Call API', { x: 120, y: 80 });
    const setVariable = addMicroflowCanvasNode(definition, 'setVariable', 'Set Variable', { x: 120, y: 80 });
    const returned = addMicroflowCanvasNode(definition, 'return', 'Return', { x: 120, y: 80 });

    expect(changed.definition.nodes.find((item) => item.id === changed.nodeId)?.config).toMatchObject({
      fieldMappings: [],
      idExpression: refExpr('currentRow', '__runtimeKey', 'string'),
      targetVariable: 'updatedRow'
    });
    expect(deleted.definition.nodes.find((item) => item.id === deleted.nodeId)?.config).toMatchObject({
      idExpression: refExpr('currentRow', '__runtimeKey', 'string'),
      targetVariable: 'deleteResult'
    });
    expect(called.definition.nodes.find((item) => item.id === called.nodeId)?.config).toMatchObject({
      bodyMappings: [],
      queryMappings: [],
      routePath: '',
      targetVariable: 'apiResult'
    });
    expect(setVariable.definition.nodes.find((item) => item.id === setVariable.nodeId)?.config).toMatchObject({
      valueExpression: { kind: 'literal', value: '' },
      variableCode: 'nextValue'
    });
    expect(returned.definition.nodes.find((item) => item.id === returned.nodeId)?.config).toMatchObject({
      outputSchema: {
        fields: [{ fieldCode: 'total', expression: { functionId: 'count', kind: 'function' } }],
        valueType: 'object',
        variableCode: 'result'
      }
    });
  });

  it('creates default structured return schema', () => {
    const definition = createDefaultMicroflowDefinition('order_query');
    const returnNode = definition.nodes.find((node) => node.type === 'return');

    expect(returnNode?.config.outputSchema).toBeDefined();
    expect(returnNode?.config.outputSchema).toMatchObject({
      fields: [{ fieldCode: 'total', expression: { functionId: 'count', kind: 'function' } }],
      valueType: 'object',
      variableCode: 'result',
      variableName: '返回结果'
    });
  });

  it('rejects self and duplicate connections while accepting new valid edges', () => {
    const definition = createDefaultMicroflowDefinition('order_query');

    expect(canConnectMicroflowNodes({ source: 'order_query_start', target: 'order_query_start' } as Connection, definition)).toBe(false);
    expect(canConnectMicroflowNodes({ source: 'order_query_start', target: 'order_query_query' } as Connection, definition)).toBe(false);
    expect(canConnectMicroflowNodes({ source: 'order_query_start', target: 'order_query_return' } as Connection, definition)).toBe(true);

    const connected = connectMicroflowNodes(definition, { source: 'order_query_start', target: 'order_query_return' } as Connection);
    expect(connected.edges.some((edge) => edge.sourceNodeId === 'order_query_start' && edge.targetNodeId === 'order_query_return')).toBe(true);
  });

  it('deletes nodes with connected edges and updates edge conditions', () => {
    const definition: MicroflowDefinition = {
      ...createDefaultMicroflowDefinition('order_query'),
      edges: [
        { id: 'start-query', sourceNodeId: 'order_query_start', targetNodeId: 'order_query_query' },
        { id: 'query-return', sourceNodeId: 'order_query_query', targetNodeId: 'order_query_return' }
      ]
    };

    const withoutQuery = deleteMicroflowCanvasNode(definition, 'order_query_query');
    expect(withoutQuery.nodes.map((node) => node.id)).toEqual(['order_query_start', 'order_query_return']);
    expect(withoutQuery.edges).toEqual([]);

    const withCondition = updateMicroflowEdgeCondition(definition, 'start-query', 'status == ok');
    expect(withCondition.edges.find((edge) => edge.id === 'start-query')?.condition).toBe('status == ok');
    expect(updateMicroflowEdgeCondition(withCondition, 'start-query', '').edges.find((edge) => edge.id === 'start-query')?.condition).toBeNull();
  });

  it('duplicates a node without copying edges', () => {
    const definition = createDefaultMicroflowDefinition('order_query');

    const duplicated = duplicateMicroflowCanvasNode(definition, 'order_query_query');

    expect(duplicated.nodeId).toBe('retrieve_0');
    const sourceNode = definition.nodes.find((node) => node.id === 'order_query_query');
    expect(duplicated.definition.nodes.find((node) => node.id === duplicated.nodeId)).toMatchObject({
      name: 'Retrieve Copy',
      type: 'retrieve',
      x: Number(sourceNode?.x ?? 0) + microflowCanvasNodeSize.width + 40,
      y: Number(sourceNode?.y ?? 0) + 20
    });
    expect(duplicated.definition.edges).toHaveLength(definition.edges.length);
  });

  it('creates inline input and output tags for node cards from real node config', () => {
    const definition: MicroflowDefinition = {
      ...createDefaultMicroflowDefinition('order_query'),
      nodes: createDefaultMicroflowDefinition('order_query').nodes.map((node) => {
        if (node.id === 'order_query_query') {
          return {
            ...node,
            config: {
              ...node.config,
              outputSchema: {
                fields: [
                  { dataType: 'string', fieldCode: 'orderNo', fieldName: '订单号', visible: true, writable: false },
                  { dataType: 'number', fieldCode: 'amount', fieldName: '金额', visible: true, writable: false },
                  { dataType: 'string', fieldCode: 'status', fieldName: '状态', visible: true, writable: false }
                ],
                valueType: 'array',
                variableCode: 'items',
                variableName: '订单列表'
              }
            }
          };
        }

        return node;
      })
    };

    const nodes = createMicroflowCanvasNodes(definition, null);
    const queryNode = nodes.find((node) => node.id === 'order_query_query');
    const returnNode = nodes.find((node) => node.id === 'order_query_return');

    expect(queryNode?.data.outputTags.map((tag) => tag.label)).toEqual(['orderNo', 'amount', 'status']);
    expect(returnNode?.data.inputTags.some((tag) => tag.label.includes('items'))).toBe(true);
  });

  it('deletes one edge without changing nodes', () => {
    const definition = createDefaultMicroflowDefinition('order_query');

    const updated = deleteMicroflowCanvasEdge(definition, 'order_query_start_order_query_query');

    expect(updated.nodes).toHaveLength(definition.nodes.length);
    expect(updated.edges.map((edge) => edge.id)).toEqual(['order_query_query_order_query_return']);
  });

  it('deletes multiple nodes and all connected edges in one pass', () => {
    const definition: MicroflowDefinition = {
      ...createDefaultMicroflowDefinition('order_query'),
      edges: [
        { id: 'start-query', sourceNodeId: 'order_query_start', targetNodeId: 'order_query_query' },
        { id: 'query-return', sourceNodeId: 'order_query_query', targetNodeId: 'order_query_return' },
        { id: 'start-return', sourceNodeId: 'order_query_start', targetNodeId: 'order_query_return' }
      ]
    };

    const updated = deleteMicroflowCanvasNodes(definition, ['order_query_start', 'order_query_query']);

    expect(updated.nodes.map((node) => node.id)).toEqual(['order_query_return']);
    expect(updated.edges).toEqual([]);
  });
});

function refExpr(source: string, path: string, dataType = 'string'): MicroflowValueExpression {
  const parts = path.split('.').filter(Boolean);
  const variableId = source === 'variables' ? parts[0] ?? '' : source;
  return {
    dataType,
    kind: 'ref',
    ref: {
      dataType,
      fieldPath: source === 'variables' ? parts.slice(1) : parts,
      label: path || source,
      outputKey: source === 'variables' ? variableId : source,
      sourceType: source === 'variables' ? 'global' : 'loopItem',
      variableId
    }
  };
}
