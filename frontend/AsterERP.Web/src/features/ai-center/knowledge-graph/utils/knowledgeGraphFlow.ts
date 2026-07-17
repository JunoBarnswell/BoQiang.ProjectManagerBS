import type { Connection, Edge, Node } from '@xyflow/react';

import type {
  KnowledgeGraphEdgeView,
  KnowledgeGraphNodeView,
  KnowledgeGraphOption,
  KnowledgeGraphSelection,
  KnowledgeGraphSnapshotView
} from '../types';

export interface KnowledgeGraphFlowNodeData extends Record<string, unknown> {
  node: KnowledgeGraphNodeView;
}

export interface KnowledgeGraphFlowEdgeData extends Record<string, unknown> {
  edge: KnowledgeGraphEdgeView;
}

export type KnowledgeGraphFlowNode = Node<KnowledgeGraphFlowNodeData, 'knowledgeGraphNode'>;
export type KnowledgeGraphFlowEdge = Edge<KnowledgeGraphFlowEdgeData>;

export function buildFlowNodes(
  snapshot: KnowledgeGraphSnapshotView,
  selection: KnowledgeGraphSelection | null,
  layoutOverrides: Record<string, { x: number; y: number }>
): KnowledgeGraphFlowNode[] {
  return snapshot.nodes.map((node) => ({
    data: { node },
    id: node.id,
    position: layoutOverrides[node.id] ?? node.position,
    selected: selection?.kind === 'node' && selection.id === node.id,
    type: 'knowledgeGraphNode'
  }));
}

export function buildFlowEdges(
  snapshot: KnowledgeGraphSnapshotView,
  selection: KnowledgeGraphSelection | null
): KnowledgeGraphFlowEdge[] {
  return snapshot.edges
    .filter((edge) => snapshot.nodes.some((node) => node.id === edge.source) && snapshot.nodes.some((node) => node.id === edge.target))
    .map((edge) => ({
      animated: edge.status.toLowerCase() === 'pending',
      data: { edge },
      id: edge.id,
      label: edge.label,
      selected: selection?.kind === 'edge' && selection.id === edge.id,
      source: edge.source,
      style: {
        stroke: edge.status.toLowerCase() === 'disabled' ? '#94a3b8' : '#2563eb',
        strokeWidth: Math.max(1.5, Math.min(4, edge.weight))
      },
      target: edge.target,
      type: 'smoothstep'
    }));
}

export function canConnectKnowledgeGraph(snapshot: KnowledgeGraphSnapshotView, connection: Connection): boolean {
  if (!connection.source || !connection.target || connection.source === connection.target) {
    return false;
  }

  const nodeIds = new Set(snapshot.nodes.map((node) => node.id));
  if (!nodeIds.has(connection.source) || !nodeIds.has(connection.target)) {
    return false;
  }

  return !snapshot.edges.some((edge) => edge.source === connection.source && edge.target === connection.target);
}

export function createEdgeDraftFromConnection(connection: Connection) {
  return {
    sourceNodeId: connection.source ?? '',
    targetNodeId: connection.target ?? ''
  };
}

export function getKnowledgeGraphOptions(snapshot: KnowledgeGraphSnapshotView): {
  nodeOptions: KnowledgeGraphOption[];
  nodeTypeOptions: KnowledgeGraphOption[];
  relationTypeOptions: KnowledgeGraphOption[];
  sourceOptions: KnowledgeGraphOption[];
  statusOptions: KnowledgeGraphOption[];
} {
  return {
    nodeOptions: snapshot.nodes.map((node) => ({ label: `${node.label} (${node.nodeCode})`, value: node.id })),
    nodeTypeOptions: distinctOptions(snapshot.nodes.map((node) => node.nodeType)),
    relationTypeOptions: distinctOptions(snapshot.edges.map((edge) => edge.relationType)),
    sourceOptions: distinctSourceOptions(snapshot.nodes),
    statusOptions: distinctOptions([
      ...snapshot.nodes.map((node) => node.status),
      ...snapshot.edges.map((edge) => edge.status)
    ])
  };
}

export function findSelectedNode(snapshot: KnowledgeGraphSnapshotView, selection: KnowledgeGraphSelection | null): KnowledgeGraphNodeView | null {
  if (selection?.kind !== 'node') {
    return null;
  }

  return snapshot.nodes.find((node) => node.id === selection.id) ?? null;
}

export function findSelectedEdge(snapshot: KnowledgeGraphSnapshotView, selection: KnowledgeGraphSelection | null): KnowledgeGraphEdgeView | null {
  if (selection?.kind !== 'edge') {
    return null;
  }

  return snapshot.edges.find((edge) => edge.id === selection.id) ?? null;
}

function distinctOptions(values: string[]): KnowledgeGraphOption[] {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean)))
    .sort((left, right) => left.localeCompare(right))
    .map((value) => ({ label: value, value }));
}

function distinctSourceOptions(nodes: KnowledgeGraphNodeView[]): KnowledgeGraphOption[] {
  const options = new Map<string, string>();
  nodes.forEach((node) => {
    if (node.sourceId) {
      options.set(node.sourceId, node.sourceName || node.sourceId);
    }
  });
  return Array.from(options.entries())
    .sort((left, right) => left[1].localeCompare(right[1]))
    .map(([value, label]) => ({ label, value }));
}
