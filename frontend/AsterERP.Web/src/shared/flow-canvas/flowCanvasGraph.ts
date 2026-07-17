import type { Connection, Edge, Node } from '@xyflow/react';

type CanvasNodeLike = Pick<Node, 'id'> & {
  parentId?: string;
};

type CanvasEdgeLike = Pick<Edge, 'id' | 'source' | 'target'> & {
  sourceHandle?: string | null;
  targetHandle?: string | null;
};

export interface CanvasConnectionRuleOptions<TNode extends CanvasNodeLike, TEdge extends CanvasEdgeLike> {
  allowSelfConnection?: boolean;
  canConnectNodePair?: (sourceNode: TNode, targetNode: TNode, connection: Connection) => boolean;
  getNodeParentId?: (node: TNode) => string | null | undefined;
  preventCycles?: boolean;
  preventDuplicate?: boolean;
  sameEdge?: (edge: TEdge, connection: Connection) => boolean;
}

export function createUniqueCanvasNodeId(prefix: string, existingNodes: Array<Pick<Node, 'id'>> = []): string {
  const normalizedPrefix = prefix.trim() || 'node';
  const used = new Set(existingNodes.map((node) => node.id));
  let index = 0;
  let candidate = `${normalizedPrefix}_${index}`;
  while (used.has(candidate)) {
    index += 1;
    candidate = `${normalizedPrefix}_${index}`;
  }

  return candidate;
}

export function canConnectCanvasNodes<TNode extends CanvasNodeLike, TEdge extends CanvasEdgeLike>(
  connection: Connection,
  nodes: TNode[],
  edges: TEdge[] = [],
  options: CanvasConnectionRuleOptions<TNode, TEdge> = {}
): boolean {
  if (!connection.source || !connection.target) {
    return false;
  }

  if (!options.allowSelfConnection && connection.source === connection.target) {
    return false;
  }

  const sourceNode = nodes.find((node) => node.id === connection.source);
  const targetNode = nodes.find((node) => node.id === connection.target);
  if (!sourceNode || !targetNode) {
    return false;
  }

  if (options.canConnectNodePair && !options.canConnectNodePair(sourceNode, targetNode, connection)) {
    return false;
  }

  if (options.preventDuplicate && edges.some((edge) => isSameCanvasEdge(edge, connection, options.sameEdge))) {
    return false;
  }

  if (options.preventCycles && wouldCreateCanvasCycle(connection.source, connection.target, edges)) {
    return false;
  }

  return true;
}

export function deleteCanvasNodeWithEdges<TNode extends CanvasNodeLike, TEdge extends CanvasEdgeLike>(
  nodeId: string,
  nodes: TNode[],
  edges: TEdge[],
  options: Pick<CanvasConnectionRuleOptions<TNode, TEdge>, 'getNodeParentId'> = {}
): { edges: TEdge[]; nodes: TNode[]; removedNodeIds: string[] } {
  const idsToRemove = collectNodeAndDescendantIds(nodeId, nodes, options.getNodeParentId);
  return {
    edges: edges.filter((edge) => !idsToRemove.has(edge.source) && !idsToRemove.has(edge.target)),
    nodes: nodes.filter((node) => !idsToRemove.has(node.id)),
    removedNodeIds: Array.from(idsToRemove)
  };
}

export function deleteCanvasEdge<TEdge extends CanvasEdgeLike>(edgeId: string, edges: TEdge[]): TEdge[] {
  return edges.filter((edge) => edge.id !== edgeId);
}

export function wouldCreateCanvasCycle<TEdge extends CanvasEdgeLike>(
  sourceId: string,
  targetId: string,
  edges: TEdge[]
): boolean {
  if (sourceId === targetId) {
    return true;
  }

  const graph = new Map<string, string[]>();
  edges.forEach((edge) => {
    const targets = graph.get(edge.source) ?? [];
    targets.push(edge.target);
    graph.set(edge.source, targets);
  });

  return hasDirectedCanvasPath(targetId, sourceId, graph, new Set<string>());
}

export function collectNodeAndDescendantIds<TNode extends CanvasNodeLike>(
  nodeId: string,
  nodes: TNode[],
  getNodeParentId: (node: TNode) => string | null | undefined = (node) => node.parentId
): Set<string> {
  const ids = new Set<string>([nodeId]);
  let changed = true;
  while (changed) {
    changed = false;
    nodes.forEach((node) => {
      const parentId = getNodeParentId(node);
      if (parentId && ids.has(parentId) && !ids.has(node.id)) {
        ids.add(node.id);
        changed = true;
      }
    });
  }

  return ids;
}

function hasDirectedCanvasPath(currentId: string, destinationId: string, graph: Map<string, string[]>, visited: Set<string>): boolean {
  if (currentId === destinationId) {
    return true;
  }

  if (visited.has(currentId)) {
    return false;
  }

  visited.add(currentId);
  return (graph.get(currentId) ?? []).some((nextId) => hasDirectedCanvasPath(nextId, destinationId, graph, visited));
}

function isSameCanvasEdge<TEdge extends CanvasEdgeLike>(
  edge: TEdge,
  connection: Connection,
  sameEdge?: (edge: TEdge, connection: Connection) => boolean
): boolean {
  if (sameEdge) {
    return sameEdge(edge, connection);
  }

  return (
    edge.source === connection.source &&
    edge.target === connection.target &&
    normalizeHandle(edge.sourceHandle) === normalizeHandle(connection.sourceHandle) &&
    normalizeHandle(edge.targetHandle) === normalizeHandle(connection.targetHandle)
  );
}

function normalizeHandle(handle?: string | null): string {
  return handle ?? '';
}
