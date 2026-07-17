import {
  Background,
  Controls,
  MiniMap,
  ReactFlow,
  applyEdgeChanges,
  applyNodeChanges,
  type Connection,
  type Edge,
  type EdgeChange,
  type Node,
  type NodeChange
} from '@xyflow/react';
import { useEffect, useMemo, useState } from 'react';

import { formatMessage } from '../../../../core/i18n/formatMessage';
import { useI18n } from '../../../../core/i18n/I18nProvider';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { AppIcon } from '../../../../shared/icons/AppIcon';
import type {
  KnowledgeGraphNodeView,
  KnowledgeGraphSelection,
  KnowledgeGraphSnapshotView
} from '../types';
import { buildFlowEdges, buildFlowNodes, canConnectKnowledgeGraph } from '../utils/knowledgeGraphFlow';

import {
  KnowledgeGraphEmptyState,
  KnowledgeGraphErrorState,
  KnowledgeGraphLoadingState,
  KnowledgeGraphStatusBadge,
  KnowledgeGraphTruncatedBanner
} from './KnowledgeGraphStateViews';

import '@xyflow/react/dist/style.css';

interface KnowledgeGraphCanvasProps {
  error: unknown;
  layoutOverrides: Record<string, { x: number; y: number }>;
  loading: boolean;
  selection: KnowledgeGraphSelection | null;
  snapshot: KnowledgeGraphSnapshotView;
  onConnectNodes: (sourceNodeId: string, targetNodeId: string) => void;
  onCreateEdge: () => void;
  onCreateNode: () => void;
  onDeleteEdge: (edgeId: string) => void;
  onDeleteNode: (nodeId: string) => void;
  onEditEdge: (edgeId: string) => void;
  onEditNode: (nodeId: string) => void;
  onNodePositionCommit: (nodeId: string, position: { x: number; y: number }) => void;
  onRetry: () => void;
  onSelectionChange: (selection: KnowledgeGraphSelection | null) => void;
}

export function KnowledgeGraphCanvas({
  error,
  layoutOverrides,
  loading,
  onConnectNodes,
  onCreateEdge,
  onCreateNode,
  onDeleteEdge,
  onDeleteNode,
  onEditEdge,
  onEditNode,
  onNodePositionCommit,
  onRetry,
  onSelectionChange,
  selection,
  snapshot
}: KnowledgeGraphCanvasProps) {
  const { translate } = useI18n();
  const derivedNodes = useMemo(
    () => toRenderableNodes(buildFlowNodes(snapshot, selection, layoutOverrides)),
    [layoutOverrides, selection, snapshot]
  );
  const derivedEdges = useMemo(() => buildFlowEdges(snapshot, selection), [selection, snapshot]);
  const [nodes, setNodes] = useState<Node[]>(derivedNodes);
  const [edges, setEdges] = useState<Edge[]>(derivedEdges);

  useEffect(() => setNodes(derivedNodes), [derivedNodes]);
  useEffect(() => setEdges(derivedEdges), [derivedEdges]);

  const selectedNodeId = selection?.kind === 'node' ? selection.id : null;
  const selectedEdgeId = selection?.kind === 'edge' ? selection.id : null;
  const hasGraph = snapshot.nodes.length > 0;

  const handleNodesChange = (changes: NodeChange[]) => {
    setNodes((current) => applyNodeChanges(changes, current));
  };

  const handleEdgesChange = (changes: EdgeChange[]) => {
    setEdges((current) => applyEdgeChanges(changes, current));
  };

  const handleConnect = (connection: Connection) => {
    if (!canConnectKnowledgeGraph(snapshot, connection) || !connection.source || !connection.target) {
      return;
    }

    onConnectNodes(connection.source, connection.target);
  };

  return (
    <section className="kg-canvas-shell">
      <header className="kg-canvas-toolbar">
        <div>
          <h2>{translate('kg.canvas.title')}</h2>
          <span>{formatMessage(translate('kg.canvas.summary'), { edgeCount: snapshot.edges.length, nodeCount: snapshot.nodes.length })}</span>
        </div>
        <div className="kg-action-row">
          <PermissionButton className="ghost-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={onCreateNode}>
            {translate('kg.canvas.actions.createNode')}
          </PermissionButton>
          <PermissionButton className="ghost-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={onCreateEdge}>
            {translate('kg.canvas.actions.createEdge')}
          </PermissionButton>
          {selectedNodeId ? (
            <>
              <PermissionButton className="ghost-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onEditNode(selectedNodeId)}>
                {translate('kg.canvas.actions.editNode')}
              </PermissionButton>
              <PermissionButton className="danger-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onDeleteNode(selectedNodeId)}>
                {translate('kg.canvas.actions.deleteNode')}
              </PermissionButton>
            </>
          ) : null}
          {selectedEdgeId ? (
            <>
              <PermissionButton className="ghost-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onEditEdge(selectedEdgeId)}>
                {translate('kg.canvas.actions.editEdge')}
              </PermissionButton>
              <PermissionButton className="danger-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={() => onDeleteEdge(selectedEdgeId)}>
                {translate('kg.canvas.actions.deleteEdge')}
              </PermissionButton>
            </>
          ) : null}
        </div>
      </header>

      {snapshot.truncated ? (
        <KnowledgeGraphTruncatedBanner
          edgeTotal={snapshot.edgeTotal}
          nodeTotal={snapshot.nodeTotal}
          reason={snapshot.truncateReason}
        />
      ) : null}

      <div className="kg-canvas">
        {error ? <KnowledgeGraphErrorState error={error} onRetry={onRetry} /> : null}
        {!error && loading ? <KnowledgeGraphLoadingState label={translate('kg.canvas.loading')} /> : null}
        {!error && !loading && !hasGraph ? (
          <KnowledgeGraphEmptyState
            action={
              <PermissionButton className="primary-button" code="ai:knowledge:graph:edit" fallback="disable" type="button" onClick={onCreateNode}>
                {translate('kg.canvas.actions.createNode')}
              </PermissionButton>
            }
            description={translate('kg.canvas.empty.description')}
            title={translate('kg.canvas.empty.title')}
          />
        ) : null}
        {!error && hasGraph ? (
          <ReactFlow
            fitView
            edges={edges}
            nodes={nodes}
            onConnect={handleConnect}
            onEdgesChange={handleEdgesChange}
            onEdgeClick={(_, edge) => onSelectionChange({ id: edge.id, kind: 'edge' })}
            onNodeClick={(_, node) => onSelectionChange({ id: node.id, kind: 'node' })}
            onNodeDragStop={(_, node) => onNodePositionCommit(node.id, node.position)}
            onNodesChange={handleNodesChange}
            onPaneClick={() => onSelectionChange(null)}
          >
            <Background color="#cbd5e1" gap={20} />
            <MiniMap pannable zoomable nodeColor={(node) => getMiniMapColor(String(node.data?.nodeType ?? ''))} />
            <Controls />
          </ReactFlow>
        ) : null}
      </div>
    </section>
  );
}

function toRenderableNodes(nodes: ReturnType<typeof buildFlowNodes>): Node[] {
  return nodes.map((node) => ({
    ...node,
    data: {
      ...node.data,
      label: <KnowledgeGraphNodeCard node={node.data.node} selected={node.selected === true} />,
      nodeType: node.data.node.nodeType
    },
    style: {
      background: 'transparent',
      border: 0,
      padding: 0,
      width: 190
    },
    type: 'default'
  }));
}

function KnowledgeGraphNodeCard({ node, selected }: { node: KnowledgeGraphNodeView; selected: boolean }) {
  return (
    <div className={['kg-node-card', selected ? 'kg-node-card--selected' : ''].filter(Boolean).join(' ')}>
      <div className="kg-node-card__header">
        <span>{node.nodeType}</span>
        <KnowledgeGraphStatusBadge status={node.status} />
      </div>
      <strong>{node.label}</strong>
      <small>{node.nodeCode}</small>
      <div className="kg-node-card__meta">
        <span><AppIcon name="git-branch" />{node.degree}</span>
        <span><AppIcon name="database" />{node.sourceName || '-'}</span>
      </div>
    </div>
  );
}

function getMiniMapColor(nodeType: string): string {
  const normalized = nodeType.toLowerCase();
  if (normalized.includes('document') || normalized.includes('doc')) {
    return '#0891b2';
  }
  if (normalized.includes('user') || normalized.includes('person')) {
    return '#16a34a';
  }
  if (normalized.includes('risk') || normalized.includes('error')) {
    return '#dc2626';
  }
  return '#2563eb';
}
