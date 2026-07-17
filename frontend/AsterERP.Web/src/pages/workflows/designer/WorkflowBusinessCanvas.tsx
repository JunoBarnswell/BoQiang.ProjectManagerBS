import {
  Background,
  Controls,
  MiniMap,
  Position,
  ReactFlow,
  addEdge,
  applyEdgeChanges,
  applyNodeChanges,
  type Connection,
  type Edge,
  type EdgeChange,
  type Node,
  type NodeChange
} from '@xyflow/react';
import { useMemo, useState } from 'react';

import type { WorkflowParticipantDto } from '../../../api/workflow/workflows.api';
import { useI18n } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';

import {
  createWorkflowBusinessNodeLabels,
  createWorkflowBusinessNodePalette,
  createWorkflowBusinessPropertyTabs,
  renderWorkflowBusinessNodeHint
} from './workflowBusinessI18n';
import {
  createBusinessNode,
  type BusinessNodeType,
  type WorkflowBusinessDesign,
  type WorkflowBusinessFormField,
  type WorkflowBusinessNode
} from './workflowBusinessModel';
import {
  NodePropertyPanel,
  businessNodeSize,
  type PanelTab
} from './WorkflowBusinessNodePropertyPanel';

import '@xyflow/react/dist/style.css';

interface WorkflowBusinessCanvasProps {
  design: WorkflowBusinessDesign;
  formFields: WorkflowBusinessFormField[];
  onChange: (design: WorkflowBusinessDesign) => void;
  participants: WorkflowParticipantDto[];
  participantKeyword: string;
  onParticipantKeywordChange: (keyword: string) => void;
}

export function WorkflowBusinessCanvas({
  design,
  formFields,
  onChange,
  participants,
  participantKeyword,
  onParticipantKeywordChange
}: WorkflowBusinessCanvasProps) {
  const [panelTab, setPanelTab] = useState<PanelTab>('base');
  const { translate } = useI18n();
  const nodeLabels = useMemo(() => createWorkflowBusinessNodeLabels(translate), [translate]);
  const nodePalette = useMemo(() => createWorkflowBusinessNodePalette(translate), [translate]);
  const propertyTabs = useMemo(() => createWorkflowBusinessPropertyTabs(translate), [translate]);
  const selectedNode = useMemo(
    () => design.nodes.find((node) => node.id === design.selectedNodeId) ?? design.nodes[0],
    [design.nodes, design.selectedNodeId]
  );
  const flowNodes = useMemo<Node[]>(
    () => design.nodes.map((node) => ({
      id: node.id,
      position: node.position,
      height: businessNodeSize.height,
      initialHeight: businessNodeSize.height,
      initialWidth: businessNodeSize.width,
      measured: businessNodeSize,
      sourcePosition: Position.Right,
      style: businessNodeSize,
      targetPosition: Position.Left,
      width: businessNodeSize.width,
      data: {
        label: (
          <div className={`workflow-business-node workflow-business-node--${node.type}`}>
            <span>{nodeLabels[node.type]}</span>
            <strong>{node.label}</strong>
            <small>{renderWorkflowBusinessNodeHint(node, translate)}</small>
          </div>
        )
      },
      selected: node.id === design.selectedNodeId,
      type: 'default'
    })),
    [design.nodes, design.selectedNodeId, nodeLabels, translate]
  );
  const flowEdges = useMemo<Edge[]>(
    () => design.edges.map((edge) => ({
      id: edge.id,
      label: edge.label,
      source: edge.source,
      target: edge.target,
      animated: Boolean(edge.conditionExpression),
      type: 'smoothstep'
    })),
    [design.edges]
  );

  const updateNode = (nodeId: string, updater: (node: WorkflowBusinessNode) => WorkflowBusinessNode) => {
    onChange({
      ...design,
      nodes: design.nodes.map((node) => node.id === nodeId ? updater(node) : node)
    });
  };

  const addNode = (type: BusinessNodeType) => {
    const id = `${type}_${Date.now().toString(36)}`;
    const newNode = createBusinessNode(type, id, nodeLabels[type], {
      x: 220 + design.nodes.length * 34,
      y: 90 + design.nodes.length * 20
    });
    onChange({
      ...design,
      selectedNodeId: id,
      nodes: [...design.nodes, newNode]
    });
  };

  const deleteSelected = () => {
    if (!selectedNode || selectedNode.type === 'start') {
      return;
    }

    onChange({
      ...design,
      selectedNodeId: design.nodes.find((node) => node.id !== selectedNode.id)?.id ?? '',
      nodes: design.nodes.filter((node) => node.id !== selectedNode.id),
      edges: design.edges.filter((edge) => edge.source !== selectedNode.id && edge.target !== selectedNode.id)
    });
  };

  const onNodesChange = (changes: NodeChange[]) => {
    const changed = applyNodeChanges(changes, flowNodes);
    onChange({
      ...design,
      nodes: design.nodes.map((node) => {
        const flowNode = changed.find((item) => item.id === node.id);
        return flowNode ? { ...node, position: flowNode.position } : node;
      })
    });
  };

  const onEdgesChange = (changes: EdgeChange[]) => {
    const changed = applyEdgeChanges(changes, flowEdges);
    onChange({
      ...design,
      edges: changed.map((edge) => ({
        id: edge.id,
        source: edge.source,
        target: edge.target,
        label: design.edges.find((item) => item.id === edge.id)?.label,
        conditionExpression: design.edges.find((item) => item.id === edge.id)?.conditionExpression
      }))
    });
  };

  const onConnect = (connection: Connection) => {
    if (!connection.source || !connection.target) {
      return;
    }

    const edgeId = `flow_${connection.source}_${connection.target}`;
    const nextEdges = addEdge({ ...connection, id: edgeId, type: 'smoothstep' }, flowEdges);
    onChange({
      ...design,
      edges: nextEdges.map((edge) => ({
        id: edge.id,
        source: edge.source,
        target: edge.target,
        label: design.edges.find((item) => item.id === edge.id)?.label,
        conditionExpression: design.edges.find((item) => item.id === edge.id)?.conditionExpression
      }))
    });
  };

  return (
    <div className="workflow-business-layout">
      <aside className="workflow-business-palette">
        <div className="workflow-panel-title">{translate('workflowBusiness.panel.nodes')}</div>
        <div className="workflow-node-palette">
          {nodePalette.map((item) => (
            <button key={item.type} type="button" onClick={() => addNode(item.type)}>
              <AppIcon name={item.icon} />
              <span>{item.label}</span>
            </button>
          ))}
        </div>
      </aside>

      <section className="workflow-business-canvas">
        <ReactFlow
          fitView
          defaultEdgeOptions={{
            style: { stroke: '#2563eb', strokeWidth: 2 }
          }}
          edges={flowEdges}
          nodes={flowNodes}
          onConnect={onConnect}
          onEdgesChange={onEdgesChange}
          onNodeClick={(_, node) => onChange({ ...design, selectedNodeId: node.id })}
          onNodesChange={onNodesChange}
        >
          <Background />
          <MiniMap pannable zoomable />
          <Controls />
        </ReactFlow>
      </section>

      <aside className="workflow-business-panel">
        <div className="workflow-panel-header">
          <div>
            <div className="workflow-panel-title">{selectedNode?.label ?? translate('workflowBusiness.panel.nodeProperties')}</div>
            <div className="workflow-panel-subtitle">{selectedNode ? nodeLabels[selectedNode.type] : '-'}</div>
          </div>
          <button className="workflow-icon-button" type="button" title={translate('workflowBusiness.action.deleteNode')} onClick={deleteSelected}>
            <AppIcon name="trash" />
          </button>
        </div>
        {selectedNode ? (
          <>
            <div className="workflow-property-tabs workflow-property-tabs--wide">
              {propertyTabs.map((item) => (
                <button key={item.key} className={panelTab === item.key ? 'active' : ''} type="button" onClick={() => setPanelTab(item.key)}>
                  {item.label}
                </button>
              ))}
            </div>
            <NodePropertyPanel
              node={selectedNode}
              panelTab={panelTab}
              formFields={formFields}
              participants={participants}
              participantKeyword={participantKeyword}
              onParticipantKeywordChange={onParticipantKeywordChange}
              onChange={(updater) => updateNode(selectedNode.id, updater)}
            />
          </>
        ) : null}
      </aside>
    </div>
  );
}
