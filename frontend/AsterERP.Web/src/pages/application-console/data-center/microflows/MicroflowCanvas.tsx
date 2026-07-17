import {
  applyNodeChanges,
  Background,
  Controls,
  MarkerType,
  MiniMap,
  ReactFlow,
  ReactFlowProvider,
  type Connection,
  type EdgeChange,
  type EdgeTypes,
  type NodeChange,
  type NodeTypes,
  type ReactFlowInstance,
  useEdgesState,
  useNodesState,
  useUpdateNodeInternals
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import {
  Brush,
  Eye,
  EyeOff,
  GitBranch,
  Grid3X3,
  Magnet,
  Maximize2,
  Minus,
  Plus,
  Redo2,
  Search,
  Undo2
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent, type MouseEvent } from 'react';

import type { MicroflowDefinition, MicroflowNodeType } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { FlowCanvasButtonEdge } from '../../../../shared/flow-canvas/FlowCanvasButtonEdge';
import { ensureFlowCanvasResizeObserver } from '../../../../shared/flow-canvas/FlowCanvasResizeObserver';
import { FlowCanvasShell } from '../../../../shared/flow-canvas/FlowCanvasShell';

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
  type MicroflowCanvasEdge,
  type MicroflowCanvasNode as MicroflowCanvasNodeType
} from './microflowCanvasModel';
import { MicroflowCanvasNode } from './MicroflowCanvasNode';
import { MicroflowConnectionLine } from './MicroflowConnectionLine';
import { microflowNodeCatalog } from './microflowDefaults';
import { findGlobalVariableNodeDeleteBlockers } from './microflowGlobalVariableNode';
import './microflowCanvas.css';

ensureFlowCanvasResizeObserver();

interface MicroflowCanvasProps {
  definition: MicroflowDefinition;
  selectedEdgeId: string | null;
  selectedNodeId: string | null;
  readOnly?: boolean;
  onChange: (definition: MicroflowDefinition) => void;
  onEditEdgeConfig?: (edgeId: string) => void;
  onEditNodeConfig?: (nodeId: string) => void;
  onSelectEdge: (edgeId: string | null) => void;
  onSelectNode: (nodeId: string | null) => void;
}

export function MicroflowCanvas(props: MicroflowCanvasProps) {
  return (
    <ReactFlowProvider>
      <MicroflowCanvasContent {...props} />
    </ReactFlowProvider>
  );
}

function MicroflowCanvasContent({
  definition,
  selectedEdgeId,
  selectedNodeId,
  readOnly = false,
  onChange,
  onEditEdgeConfig,
  onEditNodeConfig,
  onSelectEdge,
  onSelectNode
}: MicroflowCanvasProps) {
  const message = useMessage();
  const [reactFlowInstance, setReactFlowInstance] = useState<ReactFlowInstance<MicroflowCanvasNodeType, MicroflowCanvasEdge> | null>(null);
  const [backgroundEnabled, setBackgroundEnabled] = useState(true);
  const [keyword, setKeyword] = useState('');
  const [nodes, setNodes, onNodesChange] = useNodesState<MicroflowCanvasNodeType>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<MicroflowCanvasEdge>([]);
  const updateNodeInternals = useUpdateNodeInternals();
  const [snapToGridEnabled, setSnapToGridEnabled] = useState(false);
  const lastNodeClickRef = useRef<{ nodeId: string; timestamp: number } | null>(null);
  const multiSelectionActiveRef = useRef(false);
  const nodesRef = useRef<MicroflowCanvasNodeType[]>([]);
  const draggingNodeIdRef = useRef<string | null>(null);
  const suppressedEdgeRemoveIdsRef = useRef<Set<string>>(new Set());
  const canvasLayerRef = useRef<HTMLDivElement | null>(null);
  const lastAutoFitSignatureRef = useRef('');
  const [canvasSize, setCanvasSize] = useState({ height: 0, width: 0 });
  const nodeTypes = useMemo<NodeTypes>(() => ({ microflowCanvasNode: MicroflowCanvasNode }), []);
  const edgeTypes = useMemo<EdgeTypes>(() => ({ microflowButtonEdge: FlowCanvasButtonEdge }), []);
  const defaultEdgeOptions = useMemo(() => ({
    markerEnd: {
      color: '#2563eb',
      height: 16,
      type: MarkerType.ArrowClosed,
      width: 16
    },
    style: { stroke: '#2563eb', strokeWidth: 2 }
  }), []);
  const definitionNodes = useMemo(
    () => createMicroflowCanvasNodes(definition, null),
    [definition]
  );
  const definitionEdges = useMemo(
    () => createMicroflowCanvasEdges(definition, null),
    [definition]
  );
  const nodeInternalIdKey = useMemo(() => nodes.map((node) => node.id).join('\u001f'), [nodes]);
  const canvasTopologySignature = useMemo(
    () => [
      nodes.map((node) => `${node.id}:${node.type}`).join('\u001f'),
      edges.map((edge) => `${edge.id}:${edge.source}:${edge.target}`).join('\u001f')
    ].join('\u001d'),
    [edges, nodes]
  );

  useEffect(() => {
    if (draggingNodeIdRef.current || multiSelectionActiveRef.current) {
      return;
    }

    setNodes((currentNodes) => {
      if (areMicroflowCanvasNodesEqual(currentNodes, definitionNodes)) {
        nodesRef.current = currentNodes;
        return currentNodes;
      }

      nodesRef.current = definitionNodes;
      return definitionNodes;
    });
    window.requestAnimationFrame(() => {
      setEdges((currentEdges) => areMicroflowCanvasEdgesEqual(currentEdges, definitionEdges) ? currentEdges : definitionEdges);
    });
  }, [definitionEdges, definitionNodes, setEdges, setNodes]);

  useEffect(() => {
    nodesRef.current = nodes;
  }, [nodes]);

  useEffect(() => {
    const element = canvasLayerRef.current;
    if (!element) {
      return undefined;
    }

    let frameId: number | null = null;
    const readSize = () => {
      frameId = null;
      const rect = element.getBoundingClientRect();
      const nextSize = {
        height: Math.round(rect.height),
        width: Math.round(rect.width)
      };
      setCanvasSize((currentSize) =>
        currentSize.height === nextSize.height && currentSize.width === nextSize.width
          ? currentSize
          : nextSize
      );
    };
    const scheduleRead = () => {
      if (frameId !== null) {
        window.cancelAnimationFrame(frameId);
      }
      frameId = window.requestAnimationFrame(readSize);
    };

    readSize();
    const observer = typeof ResizeObserver !== 'undefined' ? new ResizeObserver(scheduleRead) : null;
    observer?.observe(element);
    window.addEventListener('resize', scheduleRead);

    return () => {
      if (frameId !== null) {
        window.cancelAnimationFrame(frameId);
      }
      observer?.disconnect();
      window.removeEventListener('resize', scheduleRead);
    };
  }, []);

  useEffect(() => {
    if (edges.length > 0 || definition.edges.length === 0 || nodes.length === 0) {
      return;
    }

    const nodeIds = new Set(nodes.map((node) => node.id));
    const restorableEdges = definitionEdges.filter((edge) => nodeIds.has(edge.source) && nodeIds.has(edge.target));
    if (restorableEdges.length === 0) {
      return;
    }

    window.requestAnimationFrame(() => {
      setEdges((currentEdges) => currentEdges.length === 0 ? restorableEdges : currentEdges);
    });
  }, [definition.edges.length, definitionEdges, edges.length, nodes, setEdges]);

  useEffect(() => {
    if (!nodeInternalIdKey) {
      return;
    }

    const nodeInternalIds = nodeInternalIdKey.split('\u001f');
    const refreshNodeInternals = () => updateNodeInternals(nodeInternalIds);
    const frameId = window.requestAnimationFrame(refreshNodeInternals);
    const timeoutId = window.setTimeout(refreshNodeInternals, 120);
    return () => {
      window.cancelAnimationFrame(frameId);
      window.clearTimeout(timeoutId);
    };
  }, [edges.length, nodeInternalIdKey, updateNodeInternals]);

  useEffect(() => {
    if (!nodeInternalIdKey || canvasSize.width < 80 || canvasSize.height < 80) {
      return undefined;
    }

    const nodeInternalIds = nodeInternalIdKey.split('\u001f').filter(Boolean);
    const frameId = window.requestAnimationFrame(() => updateNodeInternals(nodeInternalIds));
    return () => window.cancelAnimationFrame(frameId);
  }, [canvasSize.height, canvasSize.width, nodeInternalIdKey, updateNodeInternals]);

  useEffect(() => {
    if (!reactFlowInstance || !canvasTopologySignature || nodes.length === 0 || canvasSize.width < 80 || canvasSize.height < 80) {
      return undefined;
    }

    if (draggingNodeIdRef.current || multiSelectionActiveRef.current) {
      return undefined;
    }

    const signature = canvasTopologySignature;
    if (lastAutoFitSignatureRef.current === signature) {
      return undefined;
    }

    const nodeInternalIds = nodeInternalIdKey.split('\u001f').filter(Boolean);
    let timeoutId: number | null = null;
    let canceled = false;
    const frameId = window.requestAnimationFrame(() => {
      if (canceled) {
        return;
      }

      updateNodeInternals(nodeInternalIds);
      timeoutId = window.setTimeout(() => {
        if (canceled || draggingNodeIdRef.current || multiSelectionActiveRef.current) {
          return;
        }

        reactFlowInstance.fitView({ duration: 160, maxZoom: 1, minZoom: 0.5, padding: 0.18 });
        lastAutoFitSignatureRef.current = signature;
      }, 60);
    });

    return () => {
      canceled = true;
      window.cancelAnimationFrame(frameId);
      if (timeoutId !== null) {
        window.clearTimeout(timeoutId);
      }
    };
  }, [canvasSize.height, canvasSize.width, canvasTopologySignature, nodeInternalIdKey, nodes.length, reactFlowInstance, updateNodeInternals]);


  const clearSelection = useCallback(() => {
    multiSelectionActiveRef.current = false;
    onSelectNode(null);
    onSelectEdge(null);
  }, [onSelectEdge, onSelectNode]);

  const deleteNode = useCallback((nodeId: string) => {
    if (readOnly) return;
    const blockers = findGlobalVariableNodeDeleteBlockers(definition, [nodeId]);
    if (blockers.length > 0) {
      message.error(`无法删除变量节点：${blockers[0]}`);
      return;
    }

    multiSelectionActiveRef.current = false;
    onChange(deleteMicroflowCanvasNode(definition, nodeId));
    if (selectedNodeId === nodeId) {
      onSelectNode(null);
    }
    onSelectEdge(null);
  }, [definition, message, onChange, onSelectEdge, onSelectNode, readOnly, selectedNodeId]);

  const duplicateNode = useCallback((nodeId: string) => {
    if (readOnly) return;
    multiSelectionActiveRef.current = false;
    const duplicated = duplicateMicroflowCanvasNode(definition, nodeId);
    onChange(duplicated.definition);
    if (duplicated.nodeId) {
      onSelectNode(duplicated.nodeId);
      onSelectEdge(null);
    }
  }, [definition, onChange, onSelectEdge, onSelectNode, readOnly]);

  const deleteEdge = useCallback((edgeId: string) => {
    if (readOnly) return;
    multiSelectionActiveRef.current = false;
    onChange(deleteMicroflowCanvasEdge(definition, edgeId));
    if (selectedEdgeId === edgeId) {
      onSelectEdge(null);
    }
  }, [definition, onChange, onSelectEdge, readOnly, selectedEdgeId]);

  const renderedNodes = useMemo(
    () => nodes.map((node) => ({
      ...node,
      data: {
        ...node.data,
        onDeleteNode: deleteNode,
        onDuplicateNode: duplicateNode,
        onEditNodeConfig
      }
    })),
    [deleteNode, duplicateNode, nodes, onEditNodeConfig]
  );
  const renderedEdges = useMemo(
    () => edges.map((edge) => ({
      ...edge,
      data: {
        ...(edge.data ?? {}),
        onDeleteEdge: deleteEdge
      }
    })),
    [deleteEdge, edges]
  );
  const filteredCatalog = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    if (!normalizedKeyword) {
      return microflowNodeCatalog;
    }

    return microflowNodeCatalog.filter((item) =>
      [item.title, item.type, item.description].some((value) => value.toLowerCase().includes(normalizedKeyword))
    );
  }, [keyword]);

  const addNode = useCallback((type: MicroflowNodeType | string, title: string, position?: { x: number; y: number }) => {
    if (readOnly) return;
    multiSelectionActiveRef.current = false;
    const result = addMicroflowCanvasNode(definition, type, title, position);
    onChange(result.definition);
    onSelectNode(result.nodeId);
    onSelectEdge(null);
    if (result.nodeId && type !== 'start') {
      onEditNodeConfig?.(result.nodeId);
    }
  }, [definition, onChange, onEditNodeConfig, onSelectEdge, onSelectNode, readOnly]);

  const handleConnect = useCallback((connection: Connection) => {
    if (readOnly) return;
    multiSelectionActiveRef.current = false;
    if (!canConnectMicroflowNodes(connection, definition)) {
      message.error('无效连线：全局变量节点不参与连线，请确认源/目标节点存在且不要重复连接。');
      return;
    }

    onChange(connectMicroflowNodes(definition, connection));
    onSelectNode(null);
    onSelectEdge(null);
  }, [definition, message, onChange, onSelectEdge, onSelectNode, readOnly]);

  const handleNodesChange = useCallback((changes: NodeChange<MicroflowCanvasNodeType>[]) => {
    if (readOnly) return;
    const nextNodes = applyNodeChanges(changes, nodesRef.current) as MicroflowCanvasNodeType[];
    nodesRef.current = nextNodes;
    onNodesChange(changes);

    const removeChanges = changes.filter((change) => change.type === 'remove');
    if (removeChanges.length > 0) {
      multiSelectionActiveRef.current = false;
      const removedNodeIds = removeChanges.map((change) => change.id);
      const blockers = findGlobalVariableNodeDeleteBlockers(definition, removedNodeIds);
      if (blockers.length > 0) {
        message.error(`无法删除变量节点：${blockers[0]}`);
        setNodes(createMicroflowCanvasNodes(definition, selectedNodeId));
        return;
      }

      const removedNodeIdSet = new Set(removedNodeIds);
      const suppressedEdgeIds = definition.edges
        .filter((edge) => removedNodeIdSet.has(edge.sourceNodeId) || removedNodeIdSet.has(edge.targetNodeId))
        .map((edge) => edge.id);
      suppressedEdgeIds.forEach((edgeId) => suppressedEdgeRemoveIdsRef.current.add(edgeId));
      if (suppressedEdgeIds.length > 0) {
        window.setTimeout(() => {
          suppressedEdgeIds.forEach((edgeId) => suppressedEdgeRemoveIdsRef.current.delete(edgeId));
        }, 250);
      }
      onChange(deleteMicroflowCanvasNodes(definition, removedNodeIds));
      clearSelection();
      return;
    }
  }, [clearSelection, definition, message, onChange, onNodesChange, readOnly, selectedNodeId, setNodes]);

  const handleNodeDragStart = useCallback((_: unknown, node: MicroflowCanvasNodeType) => {
    draggingNodeIdRef.current = node.id;
  }, []);

  const handleNodeDragStop = useCallback((_: unknown, node: MicroflowCanvasNodeType) => {
    if (readOnly) return;
    draggingNodeIdRef.current = null;
    const committedNodes = nodesRef.current.map((currentNode) =>
      currentNode.id === node.id ? { ...currentNode, position: node.position } : currentNode
    );
    nodesRef.current = committedNodes;
    onChange(applyMicroflowCanvasNodePositions(definition, committedNodes));
  }, [definition, onChange, readOnly]);

  const handleEdgesChange = useCallback((changes: EdgeChange<MicroflowCanvasEdge>[]) => {
    if (readOnly) return;
    onEdgesChange(changes);

    const removeChanges = changes.filter((change) => change.type === 'remove');
    if (removeChanges.length === 0) {
      return;
    }

    multiSelectionActiveRef.current = false;
    const removedEdgeIds = new Set(removeChanges.map((change) => change.id));
    const explicitRemoveChanges = removeChanges.filter((change) => {
      if (!suppressedEdgeRemoveIdsRef.current.has(change.id)) {
        return true;
      }

      suppressedEdgeRemoveIdsRef.current.delete(change.id);
      return false;
    });

    if (selectedEdgeId && removedEdgeIds.has(selectedEdgeId)) {
      onSelectEdge(null);
    }

    if (explicitRemoveChanges.length === 0) {
      return;
    }

    const nextDefinition = explicitRemoveChanges.reduce(
      (current, change) => deleteMicroflowCanvasEdge(current, change.id),
      definition
    );
    onChange(nextDefinition);
  }, [definition, onChange, onEdgesChange, onSelectEdge, readOnly, selectedEdgeId]);

  const handleSelectionChange = useCallback((selection: { nodes: MicroflowCanvasNodeType[]; edges: MicroflowCanvasEdge[] }) => {
    if (selection.nodes.length + selection.edges.length > 1) {
      multiSelectionActiveRef.current = true;
      if (selectedNodeId) {
        onSelectNode(null);
      }
      if (selectedEdgeId) {
        onSelectEdge(null);
      }
      return;
    }

    multiSelectionActiveRef.current = false;
    if (selection.nodes.length > 0) {
      onSelectNode(selection.nodes[0].id);
      onSelectEdge(null);
      return;
    }

    if (selection.edges.length > 0) {
      onSelectEdge(selection.edges[0].id);
      onSelectNode(null);
      return;
    }
  }, [onSelectEdge, onSelectNode, selectedEdgeId, selectedNodeId]);

  const handleNodeClick = useCallback((event: MouseEvent, node: MicroflowCanvasNodeType) => {
    const actionElement = (event.target as Element | null)?.closest<HTMLButtonElement>('[data-microflow-node-action]');
    multiSelectionActiveRef.current = false;
    onSelectNode(node.id);
    onSelectEdge(null);
    if (!actionElement) {
      if (node.data.microflowNode.type !== 'start') {
        onEditNodeConfig?.(node.id);
      }
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    const action = actionElement.dataset.microflowNodeAction;
    if (action === 'duplicate') {
      duplicateNode(node.id);
      return;
    }

    if (action === 'node-config') {
      onEditNodeConfig?.(node.id);
      return;
    }

    if (action === 'delete') {
      deleteNode(node.id);
    }
  }, [deleteNode, duplicateNode, onEditNodeConfig, onSelectEdge, onSelectNode]);

  const handleNodeDoubleClick = useCallback((event: MouseEvent, node: MicroflowCanvasNodeType) => {
    event.preventDefault();
    event.stopPropagation();
    multiSelectionActiveRef.current = false;
    onSelectNode(node.id);
    onSelectEdge(null);
    if (node.data.microflowNode.type !== 'start') {
      onEditNodeConfig?.(node.id);
    }
  }, [onEditNodeConfig, onSelectEdge, onSelectNode]);

  const findNodeAtPoint = useCallback((clientX: number, clientY: number): MicroflowCanvasNodeType | null => {
    const nodeElements = Array.from(document.querySelectorAll<HTMLElement>('.microflow-canvas-stage .react-flow__node'));
    const matchedElement = nodeElements.find((element) => {
      const rect = element.getBoundingClientRect();
      return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom;
    });
    const nodeId = matchedElement?.getAttribute('data-id');
    return nodeId ? nodesRef.current.find((node) => node.id === nodeId) ?? null : null;
  }, []);

  const handleCanvasClickCapture = useCallback((event: MouseEvent) => {
    const target = event.target as Element | null;
    if (target?.closest('.react-flow__node')) {
      return;
    }

    const node = findNodeAtPoint(event.clientX, event.clientY);
    if (!node) {
      return;
    }

    event.stopPropagation();
    multiSelectionActiveRef.current = false;
    onSelectNode(node.id);
    onSelectEdge(null);

    const now = window.performance.now();
    const lastClick = lastNodeClickRef.current;
    lastNodeClickRef.current = { nodeId: node.id, timestamp: now };
    if (lastClick?.nodeId === node.id && now - lastClick.timestamp <= 450) {
      handleNodeDoubleClick(event, node);
    }
  }, [findNodeAtPoint, handleNodeDoubleClick, onSelectEdge, onSelectNode]);

  const handleCanvasDoubleClickCapture = useCallback((event: MouseEvent) => {
    const target = event.target as Element | null;
    if (target?.closest('.react-flow__node')) {
      return;
    }

    const node = findNodeAtPoint(event.clientX, event.clientY);
    if (!node) {
      return;
    }

    handleNodeDoubleClick(event, node);
  }, [findNodeAtPoint, handleNodeDoubleClick]);

  const toggleSnapToGrid = useCallback((event: MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
    setSnapToGridEnabled((current) => !current);
  }, []);

  const toggleBackground = useCallback((event: MouseEvent<HTMLButtonElement>) => {
    event.stopPropagation();
    setBackgroundEnabled((current) => !current);
  }, []);

  const handleCanvasKeyDown = useCallback((event: KeyboardEvent<HTMLDivElement>) => {
    if (isEditableCanvasTarget(event.target)) {
      return;
    }

    const key = event.key.toLowerCase();
    const commandKeyPressed = event.ctrlKey || event.metaKey;
    if (commandKeyPressed && key === 'd' && selectedNodeId) {
      preventCanvasShortcutDefault(event);
      duplicateNode(selectedNodeId);
      return;
    }

    if (commandKeyPressed && key === '0') {
      preventCanvasShortcutDefault(event);
      reactFlowInstance?.fitView({ duration: 160, maxZoom: 1, minZoom: 0.5, padding: 0.18 });
      return;
    }

    if (event.key === 'Escape') {
      preventCanvasShortcutDefault(event);
      clearSelection();
      return;
    }

    if ((event.key === 'Delete' || event.key === 'Backspace') && selectedNodeId) {
      preventCanvasShortcutDefault(event);
      deleteNode(selectedNodeId);
      return;
    }

    if ((event.key === 'Delete' || event.key === 'Backspace') && selectedEdgeId) {
      preventCanvasShortcutDefault(event);
      deleteEdge(selectedEdgeId);
    }
  }, [clearSelection, deleteEdge, deleteNode, duplicateNode, reactFlowInstance, selectedEdgeId, selectedNodeId]);

  const focusCanvasForKeyboard = useCallback((event: MouseEvent<HTMLDivElement>) => {
    const target = event.target as Element | null;
    if (target?.closest('button,input,textarea,select,[contenteditable="true"]')) {
      return;
    }

    event.currentTarget.focus({ preventScroll: true });
  }, []);

  return (
    <FlowCanvasShell
      bodyClassName="microflow-canvas-shell__body"
      className="microflow-canvas-shell"
      palette={
        <aside className="microflow-node-palette">
          <div className="microflow-panel-title">
            <GitBranch className="h-4 w-4" />{translateCurrentLiteral("节点")}</div>
          <label className="microflow-palette-search">
            <Search className="h-3.5 w-3.5" />
            <input placeholder={translateCurrentLiteral("搜索节点")} value={keyword} onChange={(event) => setKeyword(event.target.value)} />
          </label>
          <div className="microflow-node-catalog">
            {filteredCatalog.map((item) => (
              <button
                className="microflow-node-catalog__item"
                key={item.type}
                type="button"
                disabled={readOnly}
                onClick={() => addNode(item.type, item.title)}
              >
                <span className="microflow-node-catalog__icon"><Plus className="h-3.5 w-3.5" /></span>
                <strong>{item.title}</strong>
                <small>{item.description}</small>
              </button>
            ))}
          </div>
        </aside>
      }
      stage={
        <div
          aria-label="微流画布"
          className="microflow-reactflow-layer reactflow-parent-wrapper"
          ref={canvasLayerRef}
          tabIndex={0}
          onClickCapture={handleCanvasClickCapture}
          onDoubleClickCapture={handleCanvasDoubleClickCapture}
          onKeyDownCapture={handleCanvasKeyDown}
          onMouseDownCapture={focusCanvasForKeyboard}
        >
          <div className="microflow-canvas-toolbar">
            <button className="microflow-canvas-toolbar__button" disabled title={translateCurrentLiteral("撤销")} type="button">
              <Undo2 size={13} />{translateCurrentLiteral("撤销")}</button>
            <button className="microflow-canvas-toolbar__button" disabled title={translateCurrentLiteral("重做")} type="button">
              <Redo2 size={13} />{translateCurrentLiteral("重做")}</button>
            <span className="microflow-canvas-toolbar__divider" />
            <button className="microflow-canvas-toolbar__icon" title={translateCurrentLiteral("缩小")} type="button" onClick={() => reactFlowInstance?.zoomOut({ duration: 120 })}>
              <Minus size={13} />
            </button>
            <span className="microflow-canvas-toolbar__zoom">100%</span>
            <button className="microflow-canvas-toolbar__icon" title={translateCurrentLiteral("放大")} type="button" onClick={() => reactFlowInstance?.zoomIn({ duration: 120 })}>
              <Plus size={13} />
            </button>
            <span className="microflow-canvas-toolbar__divider" />
            <button className="microflow-canvas-toolbar__button" title={translateCurrentLiteral("适应画布")} type="button" onClick={() => reactFlowInstance?.fitView({ duration: 160, maxZoom: 1, minZoom: 0.5, padding: 0.18 })}>
              <Maximize2 size={13} />{translateCurrentLiteral("适应画布")}</button>
            <button className="microflow-canvas-toolbar__button" title={translateCurrentLiteral("格式刷")} type="button">
              <Brush size={13} />{translateCurrentLiteral("格式刷")}</button>
            <button className="microflow-canvas-toolbar__button" aria-pressed={backgroundEnabled} title={backgroundEnabled ? '隐藏网格' : '显示网格'} type="button" onClick={toggleBackground}>
              <Grid3X3 size={13} />{translateCurrentLiteral("显示网格")}</button>
          </div>
          <ReactFlow
            autoPanOnConnect
            autoPanOnNodeDrag
            fitView
            className="microflow-reactflow"
            connectionRadius={24}
            data-microflow-edge-count={renderedEdges.length}
            data-microflow-node-count={renderedNodes.length}
            deleteKeyCode={readOnly ? undefined : ['Backspace', 'Delete']}
            defaultEdgeOptions={defaultEdgeOptions}
            edgeTypes={edgeTypes}
            edges={renderedEdges}
            elementsSelectable
            fitViewOptions={{ duration: 160, maxZoom: 1, minZoom: 0.5, padding: 0.18 }}
            connectionLineComponent={MicroflowConnectionLine}
            nodes={renderedNodes}
            nodesConnectable={!readOnly}
            nodesDraggable={!readOnly}
            nodeTypes={nodeTypes}
            maxZoom={1.5}
            minZoom={0.45}
            nodeDragThreshold={1}
            proOptions={{ hideAttribution: true }}
            snapGrid={[25, 25]}
            snapToGrid={snapToGridEnabled}
            onConnect={handleConnect}
            onEdgeClick={(_, edge) => {
              multiSelectionActiveRef.current = false;
              onSelectEdge(edge.id);
              onSelectNode(null);
              onEditEdgeConfig?.(edge.id);
            }}
            onEdgesChange={handleEdgesChange}
            onEdgesDelete={(deletedEdges) => {
              if (deletedEdges.some((edge) => edge.id === selectedEdgeId)) {
                onSelectEdge(null);
              }
            }}
            onInit={setReactFlowInstance}
            onNodeClick={handleNodeClick}
            onNodeDoubleClick={handleNodeDoubleClick}
            onNodesChange={handleNodesChange}
            onNodeDragStart={handleNodeDragStart}
            onNodeDragStop={handleNodeDragStop}
            onNodesDelete={clearSelection}
            onPaneClick={clearSelection}
            onSelectionChange={handleSelectionChange}
          >
            <MiniMap className="microflow-minimap" pannable zoomable />
            <Controls
              style={{
                display: 'flex',
                flexDirection: 'row',
                left: '50%',
                transform: 'translate(-50%, -50%)'
              }}
            >
              <button
                aria-pressed={snapToGridEnabled}
                className="react-flow__controls-button react-flow__controls-interactive"
                title={snapToGridEnabled ? '关闭网格吸附' : '开启网格吸附'}
                type="button"
                onClick={toggleSnapToGrid}
              >
                <Magnet className={snapToGridEnabled ? 'microflow-control-icon--active' : ''} size={15} />
              </button>
              <button
                aria-pressed={backgroundEnabled}
                className="react-flow__controls-button react-flow__controls-interactive"
                title={backgroundEnabled ? '隐藏网格背景' : '显示网格背景'}
                type="button"
                onClick={toggleBackground}
              >
                {backgroundEnabled ? <Eye size={15} /> : <EyeOff size={15} />}
              </button>
            </Controls>
            {backgroundEnabled ? <Background color="#cbd5e1" gap={16} /> : null}
          </ReactFlow>
          <div className="microflow-canvas-status">
            <span><GitBranch className="h-3.5 w-3.5" /> {definition.nodes.length} 个节点 · {definition.edges.length} 条连线</span>
          </div>
        </div>
      }
      stageClassName="microflow-canvas-stage"
    />
  );
}

function areMicroflowCanvasNodesEqual(currentNodes: MicroflowCanvasNodeType[], nextNodes: MicroflowCanvasNodeType[]): boolean {
  if (currentNodes.length !== nextNodes.length) {
    return false;
  }

  return currentNodes.every((currentNode, index) => {
    const nextNode = nextNodes[index];
    return Boolean(nextNode)
      && currentNode.id === nextNode.id
      && currentNode.type === nextNode.type
      && currentNode.selected === nextNode.selected
      && currentNode.position.x === nextNode.position.x
      && currentNode.position.y === nextNode.position.y
      && currentNode.width === nextNode.width
      && currentNode.height === nextNode.height
      && currentNode.data.title === nextNode.data.title
      && currentNode.data.description === nextNode.data.description
      && currentNode.data.configSummary === nextNode.data.configSummary
      && createTagSignature(currentNode.data.inputTags) === createTagSignature(nextNode.data.inputTags)
      && createTagSignature(currentNode.data.outputTags) === createTagSignature(nextNode.data.outputTags)
      && currentNode.data.microflowNode.id === nextNode.data.microflowNode.id
      && currentNode.data.microflowNode.name === nextNode.data.microflowNode.name
      && currentNode.data.microflowNode.type === nextNode.data.microflowNode.type;
  });
}

function createTagSignature(tags: Array<{ invalid?: boolean; label: string; title: string; valueType?: string }>): string {
  return tags.map((tag) => `${tag.label}:${tag.title}:${tag.valueType ?? ''}:${tag.invalid ? '1' : '0'}`).join('\u001e');
}

function areMicroflowCanvasEdgesEqual(currentEdges: MicroflowCanvasEdge[], nextEdges: MicroflowCanvasEdge[]): boolean {
  if (currentEdges.length !== nextEdges.length) {
    return false;
  }

  return currentEdges.every((currentEdge, index) => {
    const nextEdge = nextEdges[index];
    return Boolean(nextEdge)
      && currentEdge.id === nextEdge.id
      && currentEdge.type === nextEdge.type
      && currentEdge.source === nextEdge.source
      && currentEdge.target === nextEdge.target
      && currentEdge.selected === nextEdge.selected
      && currentEdge.animated === nextEdge.animated
      && currentEdge.label === nextEdge.label
      && currentEdge.data?.condition === nextEdge.data?.condition
      && currentEdge.data?.label === nextEdge.data?.label;
  });
}

function isEditableCanvasTarget(target: EventTarget | null): boolean {
  if (!(target instanceof Element)) {
    return false;
  }

  return Boolean(target.closest('input,textarea,select,[contenteditable="true"],.monaco-editor,.microflow-sql-script-editor,[data-microflow-code-editor]'));
}

function preventCanvasShortcutDefault(event: KeyboardEvent<HTMLDivElement>): void {
  event.preventDefault();
  event.stopPropagation();
}
