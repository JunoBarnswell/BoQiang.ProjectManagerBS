import type { Connection, Edge, Node } from '@xyflow/react';
import { describe, expect, it } from 'vitest';


import {
  canConnectCanvasNodes,
  createUniqueCanvasNodeId,
  deleteCanvasEdge,
  deleteCanvasNodeWithEdges,
  wouldCreateCanvasCycle
} from './flowCanvasGraph';

function node(id: string, parentId?: string): Pick<Node, 'id'> & { parentId?: string } {
  return { id, parentId };
}

function edge(id: string, source: string, target: string): Pick<Edge, 'id' | 'source' | 'target'> {
  return { id, source, target };
}

describe('flowCanvasGraph', () => {
  it('creates stable unique ids from an existing node set', () => {
    expect(createUniqueCanvasNodeId('query', [node('query_0'), node('query_1')])).toBe('query_2');
    expect(createUniqueCanvasNodeId('', [])).toBe('node_0');
  });

  it('rejects empty, missing, self, duplicate and cyclic connections', () => {
    const nodes = [node('start'), node('query'), node('return')];
    const edges = [edge('start-query', 'start', 'query'), edge('query-return', 'query', 'return')];

    expect(canConnectCanvasNodes({ source: null, sourceHandle: null, target: 'query', targetHandle: null } as unknown as Connection, nodes, edges)).toBe(false);
    expect(canConnectCanvasNodes(connection('start', 'missing'), nodes, edges)).toBe(false);
    expect(canConnectCanvasNodes(connection('start', 'start'), nodes, edges)).toBe(false);
    expect(canConnectCanvasNodes(connection('start', 'query'), nodes, edges, { preventDuplicate: true })).toBe(false);
    expect(canConnectCanvasNodes(connection('return', 'start'), nodes, edges, { preventCycles: true })).toBe(false);
    expect(canConnectCanvasNodes(connection('start', 'return'), nodes, edges, { preventCycles: true })).toBe(true);
  });

  it('runs custom node pair validation before accepting a connection', () => {
    const nodes = [node('start'), node('note')];

    expect(canConnectCanvasNodes(
      connection('start', 'note'),
      nodes,
      [],
      { canConnectNodePair: (_source, target) => target.id !== 'note' }
    )).toBe(false);
  });

  it('removes a node, descendants, and all connected edges', () => {
    const nodes = [node('group'), node('child', 'group'), node('sibling')];
    const edges = [
      edge('group-sibling', 'group', 'sibling'),
      edge('child-sibling', 'child', 'sibling'),
      edge('sibling-group', 'sibling', 'group')
    ];

    const result = deleteCanvasNodeWithEdges('group', nodes, edges);

    expect(result.removedNodeIds.sort()).toEqual(['child', 'group']);
    expect(result.nodes).toEqual([node('sibling')]);
    expect(result.edges).toEqual([]);
  });

  it('deletes one edge without touching the remaining graph', () => {
    const edges = [edge('a-b', 'a', 'b'), edge('b-c', 'b', 'c')];

    expect(deleteCanvasEdge('a-b', edges)).toEqual([edge('b-c', 'b', 'c')]);
  });

  it('detects directed cycles through existing edges', () => {
    expect(wouldCreateCanvasCycle('c', 'a', [edge('a-b', 'a', 'b'), edge('b-c', 'b', 'c')])).toBe(true);
    expect(wouldCreateCanvasCycle('a', 'c', [edge('a-b', 'a', 'b')])).toBe(false);
  });
});

function connection(source: string, target: string): Connection {
  return {
    source,
    sourceHandle: null,
    target,
    targetHandle: null
  };
}
