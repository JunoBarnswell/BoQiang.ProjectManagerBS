import { Button, IconButton, TextField } from '@mui/material';
import { IconArtboard, IconArtboardOff, IconCheck, IconMagnetFilled, IconMagnetOff, IconPencil, IconX } from '@tabler/icons-react';
import {
  addEdge,
  Background,
  Controls,
  MiniMap,
  ReactFlow,
  ReactFlowProvider,
  useEdgesState,
  useNodesState,
  useUpdateNodeInternals,
  type Connection,
  type EdgeTypes,
  type NodeTypes,
  type ReactFlowInstance
} from '@xyflow/react';
import { useCallback, useEffect, useMemo, useRef, useState, type MouseEvent } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';

import { useI18n } from '../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../core/query/useApiMutation';
import { useMessage } from '../../../shared/feedback/useMessage';
import { FlowCanvasButtonEdge } from '../../../shared/flow-canvas/FlowCanvasButtonEdge';
import { ensureFlowCanvasResizeObserver } from '../../../shared/flow-canvas/FlowCanvasResizeObserver';
import { FlowCanvasShell } from '../../../shared/flow-canvas/FlowCanvasShell';
import { PageLoading } from '../../../shared/status/PageLoading';
import { getErrorMessage } from '../../../shared/utils/errorMessage';
import { flowiseStudioApi } from '../api/flowiseStudio.api';
import { nativeChatflowsApi } from '../api/nativeChatflows.api';
import { useFlowiseCanvas } from '../hooks/useFlowiseCanvas';
import { useFlowiseNodeCatalog } from '../hooks/useFlowiseNodeCatalog';
import { flowiseI18nKeys } from '../i18n/flowiseI18nKeys';
import { ConnectionLine } from '../native/views/agentflows/ConnectionLine';
import { EditNodeDialog } from '../native/views/agentflows/EditNodeDialog';
import { IterationNode } from '../native/views/agentflows/IterationNode';
import { StickyNote } from '../native/views/agentflows/StickyNote';
import { WorkflowEdge } from '../native/views/agentflows/WorkflowEdge';
import { WorkflowNode } from '../native/views/agentflows/WorkflowNode';
import { WorkflowRuntimeFab, type WorkflowRuntimeKind } from '../native/views/agentflows/WorkflowRuntimeFab';
import '../native/views/agentflows/index.css';
import { ValidationPopUp } from '../native/views/chatmessage/ValidationPopUp';
import { useFlowiseStudioStore } from '../state/useFlowiseStudioStore';
import type { FlowiseCanvasEdge, FlowiseCanvasMode, FlowiseCanvasNode } from '../types/canvas.types';
import type { FlowiseChatflowDto, FlowiseChatflowUpsertRequest } from '../types/chatflow.types';
import type { FlowiseNodeCatalogItemDto } from '../types/node.types';

import { FlowiseCanvasDialogs } from './FlowiseCanvasDialogs';
import { FlowiseCanvasHeader } from './FlowiseCanvasHeader';
import { FlowiseCanvasHeaderDialogs, type FlowiseCanvasHeaderDialogKind } from './FlowiseCanvasHeaderDialogs';
import {
  applyConnectionToTargetInputs,
  applyNodeInputChange,
  buildCanvasUpsertRequest,
  canConnectFlowiseNodes,
  createFlowiseEdge,
  createNodeFromCatalog,
  createStickyNote,
  deleteFlowiseEdgeWithInputCleanup,
  deleteFlowiseNodeWithConnections,
  duplicateFlowiseNode,
  flowTypeFromMode,
  hasStartAgentflowNode,
  parseFlowDataString,
  placeWorkflowNode,
  resolveCanvasMode,
  syncFlowiseNodesWithCatalog
} from './FlowiseCanvasModel';
import { FlowiseCanvasNode as FlowiseCanvasNodeComponent } from './FlowiseCanvasNode';
import { FlowiseChatTestPanel } from './FlowiseChatTestPanel';
import { FlowiseNodePalette } from './FlowiseNodePalette';

ensureFlowCanvasResizeObserver();

function resolveNodeType(mode: FlowiseCanvasMode, item: FlowiseNodeCatalogItemDto): string {
  if (item.nodeType.toLowerCase().includes('iteration')) {
    return 'flowiseIterationNode';
  }

  if (mode.includes('agentflow') || mode === 'marketplace-template') {
    return 'flowiseWorkflowNode';
  }

  return 'flowiseCanvasNode';
}

interface FlowiseCanvasPageProps {
  forcedMode?: FlowiseCanvasMode;
}

export function FlowiseCanvasPage({ forcedMode }: FlowiseCanvasPageProps = {}) {
  return (
    <ReactFlowProvider>
      <FlowiseCanvasPageContent forcedMode={forcedMode} />
    </ReactFlowProvider>
  );
}

function FlowiseCanvasPageContent({ forcedMode }: FlowiseCanvasPageProps = {}) {
  const { resourceId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const mode = forcedMode ?? resolveCanvasMode(location.pathname);
  const { translate } = useI18n();
  const message = useMessage();
  const {
    chatPanelOpen,
    dirty,
    inspectorTab,
    selectedEdgeId,
    selectedNodeId,
    setChatPanelOpen,
    setDirty,
    setInspectorTab,
    setSelectedEdgeId,
    setSelectedNodeId,
    setValidationOpen,
    validationOpen
  } = useFlowiseStudioStore();
  const [reactFlowInstance, setReactFlowInstance] = useState<ReactFlowInstance<FlowiseCanvasNode, FlowiseCanvasEdge> | null>(null);
  const [activeHeaderDialog, setActiveHeaderDialog] = useState<FlowiseCanvasHeaderDialogKind | null>(null);
  const [saveNewDialogOpen, setSaveNewDialogOpen] = useState(false);
  const [newFlowDraft, setNewFlowDraft] = useState(() => ({
    name: ''
  }));
  const [localInitialState] = useState(() => {
    const duplicatedFlowData = localStorage.getItem('duplicatedFlowData');
    if (!duplicatedFlowData) {
      return { flowData: parseFlowDataString(null), isDuplicatedFlow: false };
    }
    localStorage.removeItem('duplicatedFlowData');
    return { flowData: parseFlowDataString(duplicatedFlowData), isDuplicatedFlow: true };
  });
  const [nodes, setNodes, onNodesChange] = useNodesState<FlowiseCanvasNode>([]);
  const [edges, setEdges, onEdgesChange] = useEdgesState<FlowiseCanvasEdge>([]);
  const updateNodeInternals = useUpdateNodeInternals();
  const [agentBackgroundEnabled, setAgentBackgroundEnabled] = useState(true);
  const [agentSnappingEnabled, setAgentSnappingEnabled] = useState(false);
  const [agentEditDialogOpen, setAgentEditDialogOpen] = useState(false);
  const lastNodeClickRef = useRef<{ nodeId: string; timestamp: number } | null>(null);
  const suppressedRestoredEdgeIdsRef = useRef<Set<string>>(new Set());

  const canvas = useFlowiseCanvas(resourceId, mode);
  const catalogQuery = useFlowiseNodeCatalog();
  const catalogItems = useMemo(
    () => (catalogQuery.data?.data ?? []).map(normalizeWorkflowCatalogItem),
    [catalogQuery.data?.data]
  );
  const isWorkflowMode = mode.includes('agentflow') || mode === 'marketplace-template';
  const saveChatflowConfigMutation = useApiMutation({
    mutationFn: (request: FlowiseChatflowUpsertRequest) => nativeChatflowsApi.update(canvas.chatflowType, resourceId ?? '', request),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => {
      await canvas.chatflowQuery.refetch();
      message.success(translate(flowiseI18nKeys.messages.canvasSaved));
    }
  });
  const renameChatflowMutation = useApiMutation({
    mutationFn: (name: string) => {
      if (!canvas.chatflow) {
        throw new Error(translate(flowiseI18nKeys.messages.saveBeforeAction));
      }

      return nativeChatflowsApi.update(canvas.chatflowType, resourceId ?? '', buildChatflowRenameRequest(canvas.chatflow, name));
    },
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: async () => {
      await Promise.all([canvas.chatflowQuery.refetch(), canvas.canvasQuery.refetch()]);
      message.success(translate(flowiseI18nKeys.messages.canvasSaved));
    }
  });
  const createChatflowMutation = useApiMutation({
    mutationFn: (request: FlowiseChatflowUpsertRequest) => nativeChatflowsApi.create(canvas.chatflowType, request),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed))),
    onSuccess: (response) => {
      setDirty(false);
      setSaveNewDialogOpen(false);
      message.success(translate(flowiseI18nKeys.messages.canvasSaved));
      navigate(canvasPathForMode(mode, response.data.id), { replace: true });
    }
  });

  useEffect(() => {
    const syncFlowData = (flowData: { edges: FlowiseCanvasEdge[]; nodes: FlowiseCanvasNode[] }) => (
      catalogItems.length > 0
        ? syncFlowiseNodesWithCatalog(flowData.nodes, flowData.edges, catalogItems)
        : { changed: false, edges: flowData.edges, nodes: flowData.nodes }
    );

    if (!resourceId) {
      const synced = syncFlowData(localInitialState.flowData);
      setNodes(synced.nodes);
      window.requestAnimationFrame(() => setEdges(synced.edges));
      setDirty(synced.changed || synced.nodes.length > 0 || synced.edges.length > 0);
      return;
    }

    const synced = syncFlowData(canvas.flowData);
    setNodes(synced.nodes);
    window.requestAnimationFrame(() => setEdges(synced.edges));
    setDirty(synced.changed);
  }, [canvas.flowData, catalogItems, localInitialState.flowData, resourceId, setDirty, setEdges, setNodes]);

  useEffect(() => {
    if (resourceId || !isWorkflowMode || localInitialState.isDuplicatedFlow || nodes.length > 0 || catalogQuery.isLoading) {
      return;
    }

    const startCatalogItem = catalogItems.find((item) => item.nodeType === 'startAgentflow');
    if (!startCatalogItem) {
      return;
    }

    const startNode = createNodeFromCatalog(startCatalogItem, { x: 100, y: 100 }, []);
    startNode.type = resolveNodeType(mode, startCatalogItem);
    startNode.data = {
      ...startNode.data,
      label: 'Start'
    };
    setNodes([startNode]);
    setEdges([]);
    setDirty(false);
  }, [
    catalogItems,
    catalogQuery.isLoading,
    isWorkflowMode,
    localInitialState.isDuplicatedFlow,
    mode,
    nodes.length,
    resourceId,
    setDirty,
    setEdges,
    setNodes
  ]);

  useEffect(() => {
    if (edges.length > 0 || canvas.flowData.edges.length === 0 || nodes.length === 0) {
      return;
    }

    const nodeIds = new Set(nodes.map((node) => node.id));
    const restorableEdges = canvas.flowData.edges.filter((edge) => nodeIds.has(edge.source) && nodeIds.has(edge.target));
    if (restorableEdges.length === 0) {
      return;
    }

    window.requestAnimationFrame(() => {
      setEdges((currentEdges) => (currentEdges.length === 0 ? restorableEdges : currentEdges));
    });
  }, [canvas.flowData.edges, edges.length, nodes, setEdges]);

  useEffect(() => {
    if (!dirty) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = '';
    };

    window.addEventListener('beforeunload', handleBeforeUnload);
    return () => window.removeEventListener('beforeunload', handleBeforeUnload);
  }, [dirty]);

  const runMutation = useApiMutation({
    mutationFn: () =>
      flowiseStudioApi.executions.run({
        idempotencyKey: `${resourceId}:${Date.now()}`,
        inputJson: '{}',
        resourceId: resourceId ?? ''
      }),
    onError: (error) => message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.runFailed))),
    onSuccess: (response) => {
      if (response.data.status === 'Failed') {
        message.error(response.data.errorMessage ?? translate(flowiseI18nKeys.messages.runFailed));
        return;
      }

      message.success(translate(flowiseI18nKeys.messages.runCompleted));
    }
  });

  const nodeTypes = useMemo<NodeTypes>(
    () => ({
      flowiseWorkflowNode: WorkflowNode,
      flowiseCanvasNode: FlowiseCanvasNodeComponent,
      flowiseIterationNode: IterationNode,
      flowiseStickyNote: StickyNote
    }),
    []
  );
  const edgeTypes = useMemo<EdgeTypes>(
    () => ({
      flowiseWorkflowEdge: WorkflowEdge,
      flowiseButtonEdge: FlowCanvasButtonEdge
    }),
    []
  );

  const restorableSourceEdges = useMemo(() => {
    const nodeIds = new Set(nodes.map((node) => node.id));
    return canvas.flowData.edges.filter((edge) =>
      nodeIds.has(edge.source) &&
      nodeIds.has(edge.target) &&
      !suppressedRestoredEdgeIdsRef.current.has(edge.id)
    );
  }, [canvas.flowData.edges, nodes]);
  const effectiveEdges = edges.length > 0 ? edges : restorableSourceEdges;
  useEffect(() => {
    if (nodes.length === 0) {
      return;
    }

    const refreshNodeInternals = () => updateNodeInternals(nodes.map((node) => node.id));
    const frameId = window.requestAnimationFrame(refreshNodeInternals);
    const timeoutId = window.setTimeout(refreshNodeInternals, 120);
    return () => {
      window.cancelAnimationFrame(frameId);
      window.clearTimeout(timeoutId);
    };
  }, [effectiveEdges.length, nodes, updateNodeInternals]);
  const selectedNode = useMemo(() => nodes.find((node) => node.id === selectedNodeId) ?? null, [nodes, selectedNodeId]);
  const selectedEdge = useMemo(() => effectiveEdges.find((edge) => edge.id === selectedEdgeId) ?? null, [effectiveEdges, selectedEdgeId]);
  const workflowConnectionLine = isWorkflowMode ? ConnectionLine : undefined;
  const workflowRuntimeKind = useMemo(() => resolveWorkflowRuntimeKind(nodes), [nodes]);
  const agentRuntimePanelOpen = chatPanelOpen || activeHeaderDialog === 'schedule' || activeHeaderDialog === 'webhook';
  const currentCanvasRequest = useMemo(
    () =>
      resourceId
        ? canvas.buildRequest(nodes, effectiveEdges, reactFlowInstance?.getViewport())
        : buildCanvasUpsertRequest('', flowTypeFromMode(mode), nodes, effectiveEdges, reactFlowInstance?.getViewport()),
    [canvas, effectiveEdges, mode, nodes, reactFlowInstance, resourceId]
  );
  const upsertAvailable = useMemo(() => hasUpsertTarget(nodes), [nodes]);
  const flowType = canvas.canvas?.flowType ?? flowTypeFromMode(mode);
  const clearWorkflowNodeStatus = useCallback(() => {
    setNodes((current) =>
      current.map((node) => ({
        ...node,
        data: {
          ...node.data,
          error: null,
          status: undefined
        }
      }))
    );
  }, [setNodes]);
  const updateWorkflowNodeStatus = useCallback((event: { error?: string; id: string; nodeId?: string; status: string }) => {
    const nodeId = event.nodeId ?? event.id;
    setNodes((current) =>
      current.map((node) =>
        node.id === nodeId
          ? {
              ...node,
              data: {
                ...node.data,
                error: event.error ?? null,
                status: event.status
              }
            }
          : node
      )
    );
  }, [setNodes]);
  const addCatalogNode = useCallback((catalogItem: FlowiseNodeCatalogItemDto, position?: { x: number; y: number }) => {
    setNodes((current) => {
      if (catalogItem.nodeType === 'startAgentflow' && hasStartAgentflowNode(current)) {
        message.error('Only one start node is allowed');
        return current;
      }

      const absolutePosition = position ?? { x: 140 + current.length * 30, y: 120 + current.length * 26 };
      const node = createNodeFromCatalog(catalogItem, absolutePosition, current);
      node.type = resolveNodeType(mode, catalogItem);
      const placement = isWorkflowMode ? placeWorkflowNode(node, current, absolutePosition) : { node };
      if (placement.reason === 'nestedIteration') {
        message.error('Nested iteration node is not supported yet');
        return current;
      }
      if (placement.reason === 'humanInputInsideIteration') {
        message.error('Human input node is not supported inside Iteration node');
        return current;
      }

      setSelectedNodeId(placement.node.id);
      setSelectedEdgeId(null);
      setInspectorTab('details');
      setDirty(true);
      return [...current, placement.node].map((nextNode) => ({
        ...nextNode,
        data: {
          ...nextNode.data,
          selected: nextNode.id === placement.node.id
        },
        selected: nextNode.id === placement.node.id
      }));
    });
  }, [isWorkflowMode, message, mode, setDirty, setInspectorTab, setNodes, setSelectedEdgeId, setSelectedNodeId]);

  const addStickyNote = useCallback(() => {
    setNodes((current) => [
      ...current,
      createStickyNote(
        { x: 180 + current.length * 20, y: 160 + current.length * 20 },
        current,
        translate(flowiseI18nKeys.canvas.stickyNote)
      )
    ]);
    setDirty(true);
  }, [setDirty, setNodes, translate]);

  const handleConnect = useCallback((connection: Connection) => {
    if (!canConnectFlowiseNodes(connection, nodes, edges, mode)) {
      message.error(translate(flowiseI18nKeys.messages.invalidConnection));
      return;
    }

    const edge = createFlowiseEdge(connection, mode, nodes);
    if (!edge) {
      return;
    }

    setEdges((current) => addEdge(edge, current));
    if (!isWorkflowMode) {
      setNodes((current) => applyConnectionToTargetInputs(current, edge));
    }
    setDirty(true);
  }, [edges, isWorkflowMode, message, mode, nodes, setDirty, setEdges, setNodes, translate]);

  const saveCanvas = async () => {
    if (!resourceId) {
      setSaveNewDialogOpen(true);
      return;
    }

    try {
      await canvas.saveMutation.mutateAsync(currentCanvasRequest);
      setDirty(false);
      message.success(translate(flowiseI18nKeys.messages.canvasSaved));
    } catch (error) {
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.saveFailed)));
    }
  };

  const createNewFlow = async () => {
    const trimmedName = newFlowDraft.name.trim();
    if (!trimmedName) {
      message.error(translate(flowiseI18nKeys.messages.nameRequired));
      return;
    }

    await createChatflowMutation.mutateAsync({
      analytic: '{}',
      apiConfig: '{}',
      category: null,
      chatbotConfig: '{}',
      deployed: false,
      flowData: currentCanvasRequest.flowData,
      followUpPrompts: '{}',
      isPublic: false,
      mcpServerConfig: '{}',
      name: trimmedName,
      speechToText: '{}',
      textToSpeech: '{}',
      type: canvas.chatflowType
    });
  };

  const validateCanvas = async () => {
    setValidationOpen(true);

    if (!resourceId) {
      return null;
    }

    return await canvas.validateMutation.mutateAsync(currentCanvasRequest);
  };

  const ensureCanvasValidBeforeRun = useCallback(async () => {
    if (!resourceId) {
      message.error(translate(flowiseI18nKeys.messages.saveBeforeAction));
      return false;
    }

    try {
      const response = await canvas.validateMutation.mutateAsync(currentCanvasRequest);
      const validation = response.data;
      if (!validation.valid) {
        setValidationOpen(true);
        const firstError = validation.issues.find((issue) => issue.severity === 'error');
        message.error(firstError?.message ?? translate(flowiseI18nKeys.messages.invalidConnection));
        return false;
      }

      return true;
    } catch (error) {
      setValidationOpen(true);
      message.error(getErrorMessage(error, translate(flowiseI18nKeys.messages.chatTestFailed)));
      return false;
    }
  }, [canvas.validateMutation, currentCanvasRequest, message, resourceId, setValidationOpen, translate]);

  const openChatPanel = useCallback(() => {
    setActiveHeaderDialog(null);
    setChatPanelOpen(true);
  }, [setChatPanelOpen]);

  const duplicateCurrentFlow = useCallback(() => {
    if (!canvas.chatflow && !currentCanvasRequest.flowData) {
      message.error(translate(flowiseI18nKeys.messages.flowCopyFailed));
      return;
    }

    try {
      const parsedFlowData = JSON.parse(currentCanvasRequest.flowData);
      localStorage.setItem('duplicatedFlowData', JSON.stringify(parsedFlowData));
      const targetPath = mode === 'agentflow-v2' ? '/flowise/v2/agentcanvas' : mode === 'agentflow' ? '/flowise/workflows' : '/flowise/canvas';
      window.open(targetPath, '_blank');
    } catch {
      message.error(translate(flowiseI18nKeys.messages.flowCopyFailed));
    }
  }, [canvas.chatflow, currentCanvasRequest.flowData, message, mode, translate]);

  const openHeaderDialog = useCallback((dialog: FlowiseCanvasHeaderDialogKind) => {
    if (!resourceId) {
      message.error(translate(flowiseI18nKeys.messages.saveBeforeAction));
      return;
    }

    if (dialog === 'schedule' || dialog === 'webhook') {
      setChatPanelOpen(false);
    }
    setActiveHeaderDialog(dialog);
  }, [message, resourceId, setChatPanelOpen, translate]);

  const updateNodeConfig = useCallback((nodeId: string, name: string, value: unknown) => {
    setNodes((current) =>
      current.map((node) =>
        node.id === nodeId
          ? applyNodeInputChange(node, name, value)
          : node
      )
    );
    setDirty(true);
  }, [setDirty, setNodes]);

  const updateNodeLabel = useCallback((nodeId: string, label: string) => {
    const nextLabel = label.trim();
    if (!nextLabel) {
      return;
    }

    setNodes((current) =>
      current.map((node) =>
        node.id === nodeId
          ? {
              ...node,
              data: {
                ...node.data,
                displayName: nextLabel,
                label: nextLabel
              }
            }
          : node
      )
    );
    setDirty(true);
  }, [setDirty, setNodes]);

  const renderedNodes = useMemo(
    () =>
      nodes.map((node) =>
        node.data.stickyNote
          ? {
              ...node,
              data: {
                ...node.data,
                onStickyTextChange: (nodeId: string, value: string) => updateNodeConfig(nodeId, 'text', value)
              }
            }
          : node
      ),
    [nodes, updateNodeConfig]
  );

  const deleteEdge = useCallback((edgeId: string) => {
    suppressedRestoredEdgeIdsRef.current.add(edgeId);
    setNodes((currentNodes) => {
      setEdges((currentEdges) => {
        const result = deleteFlowiseEdgeWithInputCleanup(edgeId, currentNodes, currentEdges);
        return result.edges;
      });
      const result = deleteFlowiseEdgeWithInputCleanup(edgeId, currentNodes, effectiveEdges);
      return result.nodes;
    });
    setSelectedEdgeId(null);
    setDirty(true);
  }, [effectiveEdges, setDirty, setEdges, setNodes, setSelectedEdgeId]);

  const renderedEdges = useMemo(
    () =>
      effectiveEdges.map((edge) => ({
        ...edge,
        data: {
          ...(edge.data ?? {}),
          onDeleteEdge: deleteEdge
        }
      })),
    [deleteEdge, effectiveEdges]
  );

  const duplicateNode = useCallback((node: FlowiseCanvasNode) => {
    setNodes((current) => [...current, duplicateFlowiseNode(node, current)]);
    setDirty(true);
  }, [setDirty, setNodes]);

  const deleteNode = useCallback((nodeId: string) => {
    setNodes((currentNodes) => {
      const result = deleteFlowiseNodeWithConnections(nodeId, currentNodes, edges);
      setEdges(result.edges);
      return result.nodes;
    });
    setSelectedNodeId(null);
    setDirty(true);
  }, [edges, setDirty, setEdges, setNodes, setSelectedNodeId]);

  const handleNodeClick = useCallback((event: MouseEvent, node: FlowiseCanvasNode) => {
    const actionElement = (event.target as Element | null)?.closest<HTMLButtonElement>('[data-flowise-node-action]');
    setSelectedNodeId(node.id);
    if (!actionElement) {
      setInspectorTab('details');
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    const action = actionElement.dataset.flowiseNodeAction;
    if (action === 'duplicate') {
      duplicateNode(node);
      return;
    }

    if (action === 'delete') {
      deleteNode(node.id);
      return;
    }

    if (action === 'info') {
      setInspectorTab('info');
    }
  }, [deleteNode, duplicateNode, setInspectorTab, setSelectedNodeId]);

  const handleNodeDoubleClick = useCallback((event: MouseEvent, node: FlowiseCanvasNode) => {
    if (!isWorkflowMode || node.data.stickyNote) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    setSelectedNodeId(node.id);
    setSelectedEdgeId(null);
    setInspectorTab('details');
    setAgentEditDialogOpen(true);
  }, [isWorkflowMode, setInspectorTab, setSelectedEdgeId, setSelectedNodeId]);

  const findNodeAtPoint = useCallback((clientX: number, clientY: number): FlowiseCanvasNode | null => {
    const nodeElements = Array.from(document.querySelectorAll<HTMLElement>('.flowise-canvas-stage .react-flow__node'));
    const matchedElement = nodeElements.find((element) => {
      const rect = element.getBoundingClientRect();
      return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom;
    });
    const nodeId = matchedElement?.getAttribute('data-id');
    return nodeId ? nodes.find((node) => node.id === nodeId) ?? null : null;
  }, [nodes]);

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
    setSelectedNodeId(node.id);
    setSelectedEdgeId(null);
    setInspectorTab('details');
    const now = window.performance.now();
    const lastClick = lastNodeClickRef.current;
    lastNodeClickRef.current = { nodeId: node.id, timestamp: now };
    if (lastClick?.nodeId === node.id && now - lastClick.timestamp <= 450) {
      handleNodeDoubleClick(event, node);
    }
  }, [findNodeAtPoint, handleNodeDoubleClick, setInspectorTab, setSelectedEdgeId, setSelectedNodeId]);

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

  if (resourceId && canvas.canvasQuery.isLoading) {
    return <PageLoading />;
  }

  return (
    <div className="flowise-canvas-page">
      <FlowCanvasShell
        bodyClassName="flowise-native-canvas-shell__body"
        className="flowise-native-canvas-shell"
        stageClassName="flowise-canvas-stage"
        chatPanel={
            <FlowiseChatTestPanel
              chatflow={canvas.chatflow ?? null}
              open={chatPanelOpen}
              resourceId={resourceId ?? ''}
              validation={canvas.validateMutation.data?.data ?? canvas.canvas?.validation ?? null}
              onBeforeRun={ensureCanvasValidBeforeRun}
              onWorkflowNodeStatusClear={clearWorkflowNodeStatus}
              onWorkflowNodeStatusUpdate={updateWorkflowNodeStatus}
              onClose={() => setChatPanelOpen(false)}
              onValidate={() => void validateCanvas()}
            />
        }
        header={
          <FlowiseCanvasHeader
            chatflowName={canvas.chatflow?.name ?? null}
            dirty={dirty}
            mode={mode}
            renaming={renameChatflowMutation.isPending}
            saving={canvas.saveMutation.isPending}
            title={flowType}
            upsertAvailable={upsertAvailable}
            onDuplicate={resourceId ? duplicateCurrentFlow : undefined}
            onOpenChat={openChatPanel}
            onOpenDialog={openHeaderDialog}
            onOpenValidation={() => void validateCanvas()}
            onRename={(name) => renameChatflowMutation.mutateAsync(name)}
            onRun={async () => {
              if (!resourceId) {
                message.error(translate(flowiseI18nKeys.messages.saveBeforeAction));
                return;
              }
              if (!(await ensureCanvasValidBeforeRun())) {
                return;
              }
              runMutation.mutate();
            }}
            onSave={() => void saveCanvas()}
          />
        }
        inspector={
          <FlowiseCanvasDialogs
            activeTab={inspectorTab === 'run' ? 'details' : inspectorTab}
            edges={renderedEdges}
            nodes={renderedNodes}
            selectedEdge={selectedEdge}
            selectedNode={selectedNode}
            validation={validationOpen ? canvas.validateMutation.data?.data ?? canvas.canvas?.validation : null}
            onNodeConfigChange={updateNodeConfig}
            onTabChange={setInspectorTab}
          />
        }
        palette={
          <FlowiseNodePalette
            catalog={catalogItems}
            loading={catalogQuery.isLoading}
            onAddNode={(item) => addCatalogNode(item)}
            onAddStickyNote={addStickyNote}
          />
        }
        stage={
          <div
            className="flowise-reactflow-interaction-layer reactflow-parent-wrapper"
            onClickCapture={handleCanvasClickCapture}
            onDoubleClickCapture={handleCanvasDoubleClickCapture}
          >
            <ReactFlow
              className="reactflow-wrapper"
              data-flowise-edge-count={renderedEdges.length}
              data-flowise-node-count={renderedNodes.length}
              deleteKeyCode={['Backspace', 'Delete']}
              edgeTypes={edgeTypes}
              edges={renderedEdges}
              fitView
              nodes={renderedNodes}
              nodeTypes={nodeTypes}
              proOptions={{ hideAttribution: true }}
              connectionLineComponent={workflowConnectionLine}
              snapGrid={[25, 25]}
              snapToGrid={isWorkflowMode && agentSnappingEnabled}
              onConnect={handleConnect}
              onDragOver={(event) => {
                event.preventDefault();
                event.dataTransfer.dropEffect = 'move';
              }}
              onDrop={(event) => {
                event.preventDefault();
                const raw = event.dataTransfer.getData('application/reactflow') || event.dataTransfer.getData('application/x-flowise-node');
                if (!raw) {
                  return;
                }

                const item = JSON.parse(raw) as FlowiseNodeCatalogItemDto;
                const position = reactFlowInstance?.screenToFlowPosition({ x: event.clientX - 100, y: event.clientY - 50 }) ?? { x: event.clientX - 100, y: event.clientY - 50 };
                addCatalogNode(item, position);
              }}
              onEdgesChange={(changes) => {
                onEdgesChange(changes);
              }}
              onEdgesDelete={(deletedEdges) => {
                deletedEdges.forEach((edge) => suppressedRestoredEdgeIdsRef.current.add(edge.id));
                setSelectedEdgeId(null);
                setDirty(true);
              }}
              onEdgeClick={(_, edge) => setSelectedEdgeId(edge.id)}
              onInit={setReactFlowInstance}
              onNodesChange={(changes) => {
                onNodesChange(changes);
              }}
              onNodesDelete={() => {
                setSelectedNodeId(null);
                setDirty(true);
              }}
              onNodeClick={handleNodeClick}
              onNodeDoubleClick={handleNodeDoubleClick}
              onNodeDragStop={() => setDirty(true)}
              onPaneClick={() => {
                setSelectedNodeId(null);
                setSelectedEdgeId(null);
              }}
            >
              <MiniMap pannable zoomable />
              <Controls
                style={
                  isWorkflowMode
                    ? {
                        display: 'flex',
                        flexDirection: 'row',
                        left: '50%',
                        transform: 'translate(-50%, -50%)'
                      }
                    : undefined
                }
              >
                {isWorkflowMode ? (
                  <>
                    <button
                      className="react-flow__controls-button react-flow__controls-interactive"
                      aria-label={translate(flowiseI18nKeys.canvas.toggleSnapping)}
                      title={translate(flowiseI18nKeys.canvas.toggleSnapping)}
                      type="button"
                      onClick={() => setAgentSnappingEnabled((current) => !current)}
                    >
                      {agentSnappingEnabled ? <IconMagnetFilled /> : <IconMagnetOff />}
                    </button>
                    <button
                      className="react-flow__controls-button react-flow__controls-interactive"
                      aria-label={translate(flowiseI18nKeys.canvas.toggleBackground)}
                      title={translate(flowiseI18nKeys.canvas.toggleBackground)}
                      type="button"
                      onClick={() => setAgentBackgroundEnabled((current) => !current)}
                    >
                      {agentBackgroundEnabled ? <IconArtboard /> : <IconArtboardOff />}
                    </button>
                  </>
                ) : null}
              </Controls>
              {(!isWorkflowMode || agentBackgroundEnabled) ? <Background color="#aaa" gap={16} /> : null}
            </ReactFlow>
            {isWorkflowMode ? (
              <>
                <WorkflowRuntimeFab
                  kind={workflowRuntimeKind}
                  translate={translate}
                  onOpenChat={openChatPanel}
                  onOpenSchedule={() => openHeaderDialog('schedule')}
                  onOpenWebhook={() => openHeaderDialog('webhook')}
                />
                {validationOpen && !agentRuntimePanelOpen ? (
                  <ValidationPopUp
                    validation={canvas.validateMutation.data?.data ?? canvas.canvas?.validation ?? null}
                    translate={translate}
                    onClose={() => setValidationOpen(false)}
                  />
                ) : null}
              </>
            ) : null}
          </div>
        }
      />
      {resourceId ? (
        <FlowiseCanvasHeaderDialogs
          activeDialog={activeHeaderDialog}
          flowData={currentCanvasRequest.flowData}
          flowType={flowType}
          chatflow={canvas.chatflow ?? null}
          configurationSaving={saveChatflowConfigMutation.isPending}
          mode={mode}
          resourceId={resourceId}
          title={flowType}
          upsertAvailable={upsertAvailable}
          onSaveConfiguration={(request) => saveChatflowConfigMutation.mutateAsync(request)}
          onClose={() => setActiveHeaderDialog(null)}
        />
      ) : null}
      <FlowiseSaveNewFlowDialog
        draft={newFlowDraft}
        open={saveNewDialogOpen}
        saving={createChatflowMutation.isPending}
        onChange={setNewFlowDraft}
        onClose={() => setSaveNewDialogOpen(false)}
        onSave={() => void createNewFlow()}
      />
      {isWorkflowMode && selectedNode ? (
        <WorkflowEditNodeDialog
          activeTab={inspectorTab === 'run' ? 'details' : inspectorTab}
          edges={renderedEdges}
          node={selectedNode}
          nodes={renderedNodes}
          open={agentEditDialogOpen}
          onClose={() => setAgentEditDialogOpen(false)}
          onNodeConfigChange={updateNodeConfig}
          onNodeLabelChange={updateNodeLabel}
          onTabChange={setInspectorTab}
        />
      ) : null}
    </div>
  );
}

function WorkflowEditNodeDialog({
  activeTab,
  edges,
  node,
  nodes,
  open,
  onClose,
  onNodeConfigChange,
  onNodeLabelChange,
  onTabChange
}: {
  activeTab: 'details' | 'additional' | 'info';
  edges: FlowiseCanvasEdge[];
  node: FlowiseCanvasNode;
  nodes: FlowiseCanvasNode[];
  open: boolean;
  onClose: () => void;
  onNodeConfigChange: (nodeId: string, name: string, value: unknown) => void;
  onNodeLabelChange: (nodeId: string, label: string) => void;
  onTabChange: (tab: 'details' | 'additional' | 'info') => void;
}) {
  const { translate } = useI18n();
  const [editingName, setEditingName] = useState(false);
  const [draftName, setDraftName] = useState(node.data.displayName);

  useEffect(() => {
    setDraftName(node.data.displayName);
    setEditingName(false);
  }, [node.id, node.data.displayName, open]);

  if (!open) {
    return null;
  }

  const saveName = () => {
    onNodeLabelChange(node.id, draftName);
    setEditingName(false);
  };

  return (
    <div className="flowise-native-dialog-backdrop flowise-agent-edit-dialog-backdrop">
      <div className="flowise-agent-edit-dialog" role="dialog" aria-modal="true" aria-label={node.data.displayName}>
        <header>
          {!editingName ? (
            <div className="flowise-agent-edit-dialog__title">
              <h3 title={node.data.displayName}>{node.data.displayName}</h3>
              <IconButton aria-label="Edit Name" size="small" title="Edit Name" onClick={() => setEditingName(true)}>
                <IconPencil size={16} />
              </IconButton>
            </div>
          ) : (
            <div className="flowise-agent-edit-dialog__title flowise-agent-edit-dialog__title--editing">
              <TextField
                autoFocus
                fullWidth
                size="small"
                value={draftName}
                onChange={(event) => setDraftName(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Enter') {
                    saveName();
                  }
                  if (event.key === 'Escape') {
                    setDraftName(node.data.displayName);
                    setEditingName(false);
                  }
                }}
              />
              <IconButton aria-label="Save Name" size="small" title="Save Name" onClick={saveName}>
                <IconCheck size={16} />
              </IconButton>
              <IconButton
                aria-label={translate(flowiseI18nKeys.common.cancel)}
                size="small"
                title={translate(flowiseI18nKeys.common.cancel)}
                onClick={() => {
                  setDraftName(node.data.displayName);
                  setEditingName(false);
                }}
              >
                <IconX size={16} />
              </IconButton>
            </div>
          )}
          <IconButton aria-label={translate(flowiseI18nKeys.actions.close)} size="small" title={translate(flowiseI18nKeys.actions.close)} onClick={onClose}>
            <IconX size={18} />
          </IconButton>
        </header>
        <EditNodeDialog
          activeTab={activeTab}
          edges={edges}
          node={node}
          nodes={nodes}
          onNodeConfigChange={onNodeConfigChange}
          onTabChange={onTabChange}
        />
      </div>
    </div>
  );
}

function normalizeWorkflowCatalogItem(item: FlowiseNodeCatalogItemDto): FlowiseNodeCatalogItemDto {
  return {
    ...item,
    category: normalizeWorkflowCatalogText(item.category),
    description: normalizeWorkflowCatalogText(item.description),
    displayName: normalizeWorkflowCatalogText(item.displayName),
    tags: item.tags?.map(normalizeWorkflowCatalogText)
  };
}

function normalizeWorkflowCatalogText(value: string): string {
  return value
    .replace(/Start\s+Agentflow/gi, 'Start')
    .replace(/Agentflow\s*V2/gi, 'Workflow')
    .replace(/Agentflow\s*v2/gi, 'Workflow')
    .replace(/\bAgentflow\b/gi, 'Workflow');
}

function FlowiseSaveNewFlowDialog({
  draft,
  open,
  saving,
  onChange,
  onClose,
  onSave
}: {
  draft: { name: string };
  open: boolean;
  saving: boolean;
  onChange: (draft: { name: string }) => void;
  onClose: () => void;
  onSave: () => void;
}) {
  const { translate } = useI18n();
  const canSave = draft.name.trim().length > 0 && !saving;
  if (!open) {
    return null;
  }

  return (
    <div className="flowise-native-dialog-backdrop">
      <div className="flowise-native-dialog">
        <header>
          <h2>{translate(flowiseI18nKeys.editor.saveChatflow)}</h2>
          <IconButton aria-label={translate(flowiseI18nKeys.actions.close)} size="small" title={translate(flowiseI18nKeys.actions.close)} onClick={onClose}>
            ×
          </IconButton>
        </header>
        <div className="flowise-native-dialog__field">
          <TextField
            autoFocus
            fullWidth
            placeholder="My New Chatflow"
            size="small"
            value={draft.name}
            onChange={(event) => onChange({ ...draft, name: event.target.value })}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && canSave) {
                onSave();
              }
            }}
          />
        </div>
        <footer>
          <Button variant="outlined" onClick={onClose}>{translate(flowiseI18nKeys.common.cancel)}</Button>
          <Button disabled={!canSave} variant="contained" onClick={onSave}>{translate(flowiseI18nKeys.common.save)}</Button>
        </footer>
      </div>
    </div>
  );
}

function canvasPathForMode(mode: FlowiseCanvasMode, id: string) {
  if (mode === 'agentflow-v2') {
    return `/flowise/v2/agentcanvas/${id}`;
  }

  if (mode === 'agentflow') {
    return `/flowise/workflows/${id}`;
  }

  return `/flowise/canvas/${id}`;
}

function buildChatflowRenameRequest(chatflow: FlowiseChatflowDto, name: string): FlowiseChatflowUpsertRequest {
  return {
    analytic: chatflow.analytic,
    apiConfig: chatflow.apiConfig,
    apikeyid: chatflow.apikeyid,
    category: chatflow.category,
    chatbotConfig: chatflow.chatbotConfig,
    deployed: chatflow.deployed,
    flowData: chatflow.flowData,
    followUpPrompts: chatflow.followUpPrompts,
    isPublic: chatflow.isPublic,
    mcpServerConfig: chatflow.mcpServerConfig,
    name,
    speechToText: chatflow.speechToText,
    textToSpeech: chatflow.textToSpeech,
    type: chatflow.type,
    workspaceId: chatflow.workspaceId
  };
}

function resolveWorkflowRuntimeKind(nodes: FlowiseCanvasNode[]): WorkflowRuntimeKind {
  const startNode = nodes.find((node) => {
    const data = node.data as { name?: string; nodeType?: string; type?: string };
    return [data.name, data.nodeType, data.type].some((value) => String(value ?? '').toLowerCase() === 'startagentflow');
  });

  const startInputType = readStartInputType(startNode);
  if (startInputType === 'scheduleInput') {
    return 'schedule';
  }

  if (startInputType === 'webhookTrigger') {
    return 'webhook';
  }

  return 'chat';
}

function readStartInputType(node: FlowiseCanvasNode | undefined): string {
  if (!node) {
    return '';
  }

  const data = node.data as { config?: Record<string, unknown>; inputs?: Record<string, unknown> };
  const inputs = data.inputs && typeof data.inputs === 'object' ? data.inputs : {};
  const config = data.config && typeof data.config === 'object' ? data.config : {};
  return String(inputs.startInputType ?? config.startInputType ?? '');
}

function hasUpsertTarget(nodes: FlowiseCanvasNode[]): boolean {
  return nodes.some((node) => {
    const data = node.data as { category?: string; inputs?: Record<string, unknown>; label?: string; name?: string; nodeType?: string; type?: string };
    const haystack = [data.name, data.nodeType, data.type, data.label, data.category].filter(Boolean).join(' ').toLowerCase();
    if (haystack.includes('document-store') || haystack.includes('document store') || haystack.includes('vectorstore') || haystack.includes('vector store')) {
      return true;
    }

    const inputs = data.inputs ?? {};
    return Object.keys(inputs).some((key) => key.toLowerCase().includes('storeid') || key.toLowerCase().includes('documentstore'));
  });
}
