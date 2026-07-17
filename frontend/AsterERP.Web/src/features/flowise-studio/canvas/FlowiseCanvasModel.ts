import type { Connection, Edge, Node, Viewport } from '@xyflow/react';

import {
  canConnectCanvasNodes,
  collectNodeAndDescendantIds,
  createUniqueCanvasNodeId,
  deleteCanvasEdge
} from '../../../shared/flow-canvas/flowCanvasGraph';
import type {
  FlowiseCanvasDto,
  FlowiseCanvasEdge,
  FlowiseCanvasEdgeData,
  FlowiseCanvasMode,
  FlowiseCanvasNode,
  FlowiseCanvasUpsertRequest,
  FlowiseFlowData
} from '../types/canvas.types';
import type { FlowiseNodeCatalogItemDto } from '../types/node.types';

const defaultViewport: Viewport = { x: 0, y: 0, zoom: 1 };
const flowiseCredentialInputKey = 'FLOWISE_CREDENTIAL_ID';
const defaultFlowiseNodeWidth = 230;
const defaultFlowiseNodeHeight = 140;
const agentflowIconColors: Record<string, string> = {
  conditionAgentAgentflow: '#7c3aed',
  conditionAgentflow: '#7c3aed',
  humanInputAgentflow: '#f97316'
};
const defaultAgentflowEdgeColor = '#2563eb';
const flowiseParamTypes = new Set([
  'asyncOptions',
  'asyncMultiOptions',
  'options',
  'multiOptions',
  'array',
  'datagrid',
  'grid',
  'string',
  'number',
  'boolean',
  'password',
  'json',
  'code',
  'date',
  'file',
  'folder',
  'tabs',
  'conditionFunction',
  'timePicker',
  'weekDaysPicker',
  'monthDaysPicker',
  'datePicker',
  'time',
  'credential'
]);

export function createFlowiseNodeId(nodeType: string, existingNodes: Pick<Node, 'id'>[] = []): string {
  return createUniqueCanvasNodeId(nodeType, existingNodes);
}

export function createFlowiseNodeLabel(catalogItem: FlowiseNodeCatalogItemDto, existingNodes: FlowiseCanvasNode[] = []): string {
  if (catalogItem.nodeType === 'startAgentflow' || catalogItem.nodeType === 'stickyNote') {
    return catalogItem.displayName;
  }

  let index = 0;
  let candidateId = `${catalogItem.nodeType}_${index}`;
  while (existingNodes.some((node) => node.id === candidateId)) {
    index += 1;
    candidateId = `${catalogItem.nodeType}_${index}`;
  }
  return `${catalogItem.displayName} ${index}`;
}

export function createNodeFromCatalog(
  catalogItem: FlowiseNodeCatalogItemDto,
  position: { x: number; y: number },
  existingNodes: FlowiseCanvasNode[]
): FlowiseCanvasNode {
  const id = createFlowiseNodeId(catalogItem.nodeType, existingNodes);
  const initialized = initializeCatalogNodeData(catalogItem, id, existingNodes);
  return {
    data: initialized,
    id,
    position,
    type: 'flowiseCanvasNode'
  };
}

export function placeWorkflowNode(
  node: FlowiseCanvasNode,
  existingNodes: FlowiseCanvasNode[],
  absolutePosition: { x: number; y: number }
): { node: FlowiseCanvasNode; reason?: 'humanInputInsideIteration' | 'nestedIteration' } {
  const parentNode = existingNodes.find((existingNode) => {
    if (!isIterationNode(existingNode)) {
      return false;
    }

    const width = existingNode.width ?? 300;
    const height = existingNode.height ?? 250;
    return (
      absolutePosition.x >= existingNode.position.x &&
      absolutePosition.x <= existingNode.position.x + width &&
      absolutePosition.y >= existingNode.position.y &&
      absolutePosition.y <= existingNode.position.y + height
    );
  });

  if (!parentNode) {
    return { node };
  }

  if (node.data.nodeType === 'iterationAgentflow') {
    return { node, reason: 'nestedIteration' };
  }

  if (node.data.nodeType === 'humanInputAgentflow') {
    return { node, reason: 'humanInputInsideIteration' };
  }

  return {
    node: {
      ...node,
      extent: 'parent',
      parentId: parentNode.id,
      ...({ parentNode: parentNode.id } as { parentNode: string }),
      position: {
        x: absolutePosition.x - parentNode.position.x,
        y: absolutePosition.y - parentNode.position.y
      }
    }
  };
}

export function hasStartAgentflowNode(nodes: FlowiseCanvasNode[]): boolean {
  return nodes.some((node) => node.data.nodeType === 'startAgentflow' || node.data.name === 'startAgentflow');
}

export function createStickyNote(
  position: { x: number; y: number },
  existingNodes: FlowiseCanvasNode[],
  displayName: string
): FlowiseCanvasNode {
  return {
    data: {
      config: { text: '' },
      displayName,
      nodeType: 'stickyNote',
      stickyNote: true
    },
    id: createFlowiseNodeId('stickyNote', existingNodes),
    position,
    type: 'flowiseStickyNote'
  };
}

export function duplicateFlowiseNode(node: FlowiseCanvasNode, existingNodes: FlowiseCanvasNode[], distance = 50): FlowiseCanvasNode {
  const newNodeId = createFlowiseNodeId(String(node.data.name ?? node.data.nodeType), existingNodes);
  const clonedData = structuredCloneWithoutFunctions(node.data);
  const nodeWithRuntimePosition = node as FlowiseCanvasNode & { positionAbsolute?: { x: number; y: number } };
  const duplicateOffset = (node.width ?? defaultFlowiseNodeWidth) + distance;
  const suffix = newNodeId.split('_').pop() ?? '0';
  replaceNodeHandleIds(clonedData.inputParams, node.id, newNodeId);
  replaceNodeHandleIds(clonedData.inputAnchors, node.id, newNodeId);
  replaceNodeHandleIds(clonedData.outputAnchors, node.id, newNodeId);
  clearConnectedInputValues(clonedData.inputs);

  return {
    ...node,
    data: {
      ...clonedData,
      id: newNodeId,
      label: `${String(clonedData.label ?? clonedData.displayName)} (${suffix})`,
      selected: false
    },
    id: newNodeId,
    position: {
      x: node.position.x + duplicateOffset,
      y: node.position.y
    },
    ...(nodeWithRuntimePosition.positionAbsolute
      ? {
          positionAbsolute: {
            x: nodeWithRuntimePosition.positionAbsolute.x + duplicateOffset,
            y: nodeWithRuntimePosition.positionAbsolute.y
          }
        }
      : {}),
    selected: false
  } as FlowiseCanvasNode;
}

export function normalizeFlowData(canvas?: FlowiseCanvasDto | null): FlowiseFlowData {
  if (!canvas) {
    return { edges: [], nodes: [], viewport: defaultViewport };
  }

  return parseFlowData(canvas.flowData);
}

export function parseFlowDataString(flowData?: string | null): FlowiseFlowData {
  return parseFlowData(flowData);
}

export function buildCanvasUpsertRequest(
  resourceId: string,
  flowType: string,
  nodes: FlowiseCanvasNode[],
  edges: FlowiseCanvasEdge[],
  viewport?: Viewport
): FlowiseCanvasUpsertRequest {
  const flowData: FlowiseFlowData = {
    edges: edges.map((edge) => ({ ...edge, data: edge.data ?? {} })),
    nodes: nodes.map(prepareFlowiseNodeForSave),
    viewport: viewport ?? defaultViewport
  };
  const serializedFlowData = JSON.stringify(flowData);
  return {
    flowData: serializedFlowData,
    flowType,
    resourceId
  };
}

export function canConnectFlowiseNodes(
  connection: Connection,
  nodes: FlowiseCanvasNode[],
  edges: FlowiseCanvasEdge[] = [],
  mode: FlowiseCanvasMode = 'chatflow'
): boolean {
  return canConnectCanvasNodes(connection, nodes, edges, {
    canConnectNodePair: (sourceNode, targetNode) => !sourceNode.data.stickyNote && !targetNode.data.stickyNote,
    preventCycles: isWorkflowMode(mode)
  });
}

export function createFlowiseEdge(connection: Connection, mode: FlowiseCanvasMode, nodes: FlowiseCanvasNode[] = []): FlowiseCanvasEdge | null {
  if (!connection.source || !connection.target) {
    return null;
  }

  const isWorkflow = isWorkflowMode(mode);
  const edgeId = isWorkflow
    ? `${connection.source}-${connection.sourceHandle ?? 'out'}-${connection.target}-${connection.targetHandle ?? 'in'}`
    : `edge-${connection.source}-${connection.sourceHandle ?? 'out'}-${connection.target}-${connection.targetHandle ?? 'in'}`;
  const agentflowData = isWorkflow ? resolveAgentflowEdgeData(connection, nodes) : null;
  const label = isWorkflow ? agentflowData?.edgeLabel : (connection.sourceHandle ?? 'connects');
  return {
    animated: isWorkflow,
    data: {
      label,
      ...(agentflowData ?? {})
    },
    id: edgeId,
    label,
    source: connection.source,
    sourceHandle: connection.sourceHandle,
    target: connection.target,
    targetHandle: connection.targetHandle,
    type: isWorkflow ? 'flowiseWorkflowEdge' : 'flowiseButtonEdge',
    ...(agentflowData?.isWithinIterationNode ? { zIndex: 9999 } : {})
  };
}

export function syncFlowiseNodesWithCatalog(
  nodes: FlowiseCanvasNode[],
  edges: FlowiseCanvasEdge[],
  catalogItems: FlowiseNodeCatalogItemDto[]
): { changed: boolean; edges: FlowiseCanvasEdge[]; nodes: FlowiseCanvasNode[] } {
  const catalogByType = new Map(catalogItems.map((item) => [item.nodeType, item]));
  let changed = false;
  const syncedNodes = nodes.map((node) => {
    const nodeType = String(node.data.nodeType ?? node.data.name ?? '');
    const catalogItem = catalogByType.get(nodeType);
    if (!catalogItem || !shouldSyncNodeWithCatalog(node, catalogItem)) {
      return node;
    }

    changed = true;
    return syncNodeWithCatalog(node, catalogItem, nodes);
  });
  const syncedEdges = edges.filter((edge) => isEdgeCompatibleWithNodes(edge, syncedNodes));

  return {
    changed: changed || syncedEdges.length !== edges.length,
    edges: syncedEdges,
    nodes: syncedNodes
  };
}

export function prepareFlowiseNodeForSave(node: FlowiseCanvasNode): FlowiseCanvasNode {
  const nodeData = structuredCloneWithoutFunctions(node.data);
  const inputs = isRecord(nodeData.inputs) ? { ...nodeData.inputs } : {};
  if (Object.prototype.hasOwnProperty.call(inputs, flowiseCredentialInputKey)) {
    nodeData.credential = inputs[flowiseCredentialInputKey];
    delete inputs[flowiseCredentialInputKey];
    nodeData.inputs = inputs;
  }

  return {
    ...node,
    data: {
      ...nodeData,
      selected: false,
      status: undefined
    },
    selected: false
  };
}

function shouldSyncNodeWithCatalog(node: FlowiseCanvasNode, catalogItem: FlowiseNodeCatalogItemDto): boolean {
  const currentVersion = Number(node.data.version ?? 0);
  const nextVersion = Number(catalogItem.version ?? currentVersion);
  if (nextVersion > currentVersion) {
    return true;
  }

  const currentParamNames = new Set((node.data.inputParams ?? []).map((item) => item.name));
  const currentAnchorNames = new Set((node.data.inputAnchors ?? []).map((item) => item.name));
  const currentOutputNames = new Set((node.data.outputAnchors ?? []).map((item) => item.name));
  const catalogParams = catalogItem.inputParams ?? [];
  const catalogAnchors = catalogItem.inputAnchors ?? [];
  const catalogOutputs = catalogItem.outputAnchors ?? [];

  return catalogParams.some((item) => flowiseParamTypes.has(item.type) && !currentParamNames.has(item.name))
    || catalogParams.some((item) => !flowiseParamTypes.has(item.type) && !currentAnchorNames.has(item.name))
    || catalogAnchors.some((item) => !currentAnchorNames.has(item.name))
    || catalogOutputs.some((item) => !currentOutputNames.has(item.name));
}

function syncNodeWithCatalog(
  node: FlowiseCanvasNode,
  catalogItem: FlowiseNodeCatalogItemDto,
  existingNodes: FlowiseCanvasNode[]
): FlowiseCanvasNode {
  const initializedData = initializeCatalogNodeData(catalogItem, node.id, existingNodes);
  const previousInputs = isRecord(node.data.inputs) ? node.data.inputs : {};
  const previousConfig = isRecord(node.data.config) ? node.data.config : {};
  const inputs = { ...initializedData.inputs, ...previousConfig, ...previousInputs };
  const inputParams = showHideInputParams({
    inputParams: initializedData.inputParams,
    inputs
  });

  return {
    ...node,
    data: {
      ...node.data,
      ...initializedData,
      config: inputs,
      inputs,
      inputParams,
      label: node.data.label ?? initializedData.label,
      selected: node.data.selected,
      status: node.data.status
    }
  };
}

function isEdgeCompatibleWithNodes(edge: FlowiseCanvasEdge, nodes: FlowiseCanvasNode[]): boolean {
  const sourceNode = nodes.find((node) => node.id === edge.source);
  const targetNode = nodes.find((node) => node.id === edge.target);
  if (!sourceNode || !targetNode) {
    return false;
  }

  return isHandleCompatible(edge.sourceHandle, sourceNode.data.outputAnchors ?? [])
    && isHandleCompatible(edge.targetHandle, [...(targetNode.data.inputAnchors ?? []), ...(targetNode.data.inputParams ?? [])]);
}

function isHandleCompatible(handleId: string | null | undefined, anchors: Array<{ id?: string | null; name: string }>): boolean {
  if (!handleId) {
    return true;
  }

  const handleInputName = resolveHandleInputName(handleId);
  if (anchors.some((anchor) => anchor.id === handleId || anchor.name === handleInputName)) {
    return true;
  }

  // Flowise legacy canvases often store generic "input"/"output" handle ids.
  // Keep those edges when the synced node definition has exactly one possible anchor.
  return anchors.length === 1 && (handleId.includes('-input-') || handleId.includes('-output-'));
}

function resolveHandleInputName(handleId: string): string {
  const parts = handleId.split('-');
  return parts.length >= 3 ? parts[2] : handleId;
}

function initializeCatalogNodeData(
  catalogItem: FlowiseNodeCatalogItemDto,
  id: string,
  existingNodes: FlowiseCanvasNode[]
): FlowiseCanvasNode['data'] {
  const rawInputs = catalogItem.inputParams ?? [];
  const inputAnchors = [
    ...(normalizeInputAnchors(catalogItem.inputAnchors, id) ?? []),
    ...rawInputs
      .filter((input) => !flowiseParamTypes.has(input.type))
      .map((input) => ({
        description: input.description,
        id: `${id}-input-${input.name}-${input.type}`,
        label: input.label,
        name: input.name,
        type: input.type
      }))
  ];
  const inputParams = rawInputs
    .filter((input) => flowiseParamTypes.has(input.type))
    .map((input) => ({
      ...input,
      id: `${id}-input-${input.name}-${input.type}`
    }));
  const defaultInputs = initializeDefaultInputValues(rawInputs);
  const outputAnchors = initializeOutputAnchors(catalogItem, id);
  const outputs = initializeDefaultOutputValues(outputAnchors);
  const initializedParams = showHideInputParams({
    inputParams,
    inputs: defaultInputs
  });

  return {
    baseClasses: catalogItem.baseClasses ?? [],
    category: catalogItem.category,
    config: { ...defaultInputs },
    description: catalogItem.description,
    displayName: catalogItem.displayName,
    icon: catalogItem.icon,
    id,
    inputAnchors,
    inputParams: initializedParams,
    inputs: defaultInputs,
    label: createFlowiseNodeLabel(catalogItem, existingNodes),
    name: catalogItem.nodeType,
    nodeType: catalogItem.nodeType,
    outputAnchors,
    outputs,
    tags: catalogItem.tags ?? [],
    type: catalogItem.displayName,
    version: catalogItem.version
  };
}

function initializeDefaultInputValues(inputs: NonNullable<FlowiseNodeCatalogItemDto['inputParams']>): Record<string, unknown> {
  return inputs.reduce<Record<string, unknown>>((acc, input) => {
    acc[input.name] = resolveInputDefaultValue(input);
    return acc;
  }, {});
}

function normalizeInputAnchors(anchors: FlowiseCanvasNode['data']['inputAnchors'], nodeId: string): FlowiseCanvasNode['data']['inputAnchors'] {
  return anchors?.map((anchor) => ({
    ...anchor,
    id: anchor.id ?? `${nodeId}-input-${anchor.name}`
  }));
}

function initializeDefaultOutputValues(outputs: FlowiseCanvasNode['data']['outputAnchors']): Record<string, unknown> {
  return (outputs ?? []).reduce<Record<string, unknown>>((acc, output) => {
    acc[output.name] = output.name;
    return acc;
  }, {});
}

function initializeOutputAnchors(catalogItem: FlowiseNodeCatalogItemDto, id: string): NonNullable<FlowiseCanvasNode['data']['outputAnchors']> {
  const outputAnchors = catalogItem.outputAnchors ?? [];
  if (outputAnchors.length > 0) {
    return outputAnchors.map((output, index) => ({
      ...output,
      id: output.id ?? `${id}-output-${output.name || index}`
    }));
  }

  return [
    {
      id: `${id}-output-${catalogItem.nodeType}`,
      label: catalogItem.displayName,
      name: catalogItem.nodeType,
      type: catalogItem.baseClasses?.join(' | ') || catalogItem.displayName
    }
  ];
}

function isIterationNode(node: FlowiseCanvasNode): boolean {
  return node.data.nodeType === 'iterationAgentflow' || node.data.name === 'iterationAgentflow' || node.type === 'flowiseIterationNode';
}

function clearConnectedInputsForNode(nodeId: string, nodes: FlowiseCanvasNode[], edges: FlowiseCanvasEdge[]): FlowiseCanvasNode[] {
  return edges
    .filter((edge) => edge.source === nodeId)
    .reduce((currentNodes, edge) => clearConnectedInputForEdge(edge, currentNodes), nodes);
}

function clearConnectedInputForEdge(edge: FlowiseCanvasEdge, nodes: FlowiseCanvasNode[]): FlowiseCanvasNode[] {
  if (!edge.targetHandle) {
    return nodes;
  }

  const targetInput = edge.targetHandle.split('-')[2] ?? edge.targetHandle;
  return nodes.map((node) => {
    if (node.id !== edge.target) {
      return node;
    }

    const inputs = isRecord(node.data.inputs) ? { ...node.data.inputs } : {};
    const inputAnchor = node.data.inputAnchors?.find((anchor) => anchor.name === targetInput);
    const inputParam = node.data.inputParams?.find((param) => param.name === targetInput);
    inputs[targetInput] = resolveClearedInputValue(inputs[targetInput], edge.source, Boolean(inputAnchor?.list), Boolean(inputParam?.acceptVariable));
    return {
      ...node,
      data: {
        ...node.data,
        config: isRecord(node.data.config) ? { ...node.data.config, [targetInput]: inputs[targetInput] } : node.data.config,
        inputs
      }
    };
  });
}

function resolveClearedInputValue(value: unknown, sourceNodeId: string, isListAnchor: boolean, acceptsVariable: boolean): unknown {
  if (isListAnchor) {
    const values = Array.isArray(value) ? value : [];
    return values.filter((item) => !(typeof item === 'string' && item.includes(sourceNodeId)));
  }

  if (acceptsVariable && typeof value === 'string') {
    return clearVariableReferences(value, sourceNodeId);
  }

  return '';
}

function clearVariableReferences(value: string, sourceNodeId: string): string {
  const legacyReference = `{{${sourceNodeId}.data.instance}}`;
  const runtimeReferencePattern = new RegExp(`\\{\\{\\s*\\$${escapeRegExp(sourceNodeId)}\\.output\\.[^}]+\\}\\}`, 'g');
  const clearedValue = value.replace(legacyReference, '').replace(runtimeReferencePattern, '');
  return clearedValue.replace(/[ \t]{2,}/g, ' ') || '';
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function replaceNodeHandleIds(items: unknown, oldNodeId: string, newNodeId: string): void {
  if (!Array.isArray(items)) {
    return;
  }

  items.forEach((item) => {
    if (!isRecord(item)) {
      return;
    }

    if (typeof item.id === 'string') {
      item.id = item.id.replace(oldNodeId, newNodeId);
    }

    if (Array.isArray(item.options)) {
      replaceNodeHandleIds(item.options, oldNodeId, newNodeId);
    }
  });
}

function clearConnectedInputValues(inputs: unknown): void {
  if (!isRecord(inputs)) {
    return;
  }

  Object.keys(inputs).forEach((inputName) => {
    const value = inputs[inputName];
    if (typeof value === 'string' && value.startsWith('{{') && value.endsWith('}}')) {
      inputs[inputName] = '';
      return;
    }

    if (Array.isArray(value)) {
      inputs[inputName] = value.filter((item) => !(typeof item === 'string' && item.startsWith('{{') && item.endsWith('}}')));
    }
  });
}

export function applyConnectionToTargetInputs(nodes: FlowiseCanvasNode[], edge: Edge): FlowiseCanvasNode[] {
  return nodes.map((node) => {
    if (node.id !== edge.target || !edge.targetHandle) {
      return node;
    }

    const config = { ...(node.data.config ?? {}) };
    config[edge.targetHandle] = edge.sourceHandle ? `${edge.source}.${edge.sourceHandle}` : edge.source;
    return {
      ...node,
      data: {
        ...node.data,
        config
      }
    };
  });
}

export function deleteFlowiseNodeWithConnections(
  nodeId: string,
  nodes: FlowiseCanvasNode[],
  edges: FlowiseCanvasEdge[]
): { edges: FlowiseCanvasEdge[]; nodes: FlowiseCanvasNode[]; removedNodeIds: string[] } {
  const idsToRemove = collectNodeAndDescendantIds(
    nodeId,
    nodes,
    (node) => node.parentId ?? (node as FlowiseCanvasNode & { parentNode?: string }).parentNode
  );
  const cleanedNodes = Array.from(idsToRemove).reduce<FlowiseCanvasNode[]>(
    (currentNodes, id) => clearConnectedInputsForNode(id, currentNodes, edges),
    nodes
  );
  return {
    edges: edges.filter((edge) => !idsToRemove.has(edge.source) && !idsToRemove.has(edge.target)),
    nodes: cleanedNodes.filter((node) => !idsToRemove.has(node.id)),
    removedNodeIds: Array.from(idsToRemove)
  };
}

export function deleteFlowiseEdgeWithInputCleanup(
  edgeId: string,
  nodes: FlowiseCanvasNode[],
  edges: FlowiseCanvasEdge[]
): { edges: FlowiseCanvasEdge[]; nodes: FlowiseCanvasNode[] } {
  const edge = edges.find((candidate) => candidate.id === edgeId);
  return {
    edges: deleteCanvasEdge(edgeId, edges),
    nodes: edge ? clearConnectedInputForEdge(edge, nodes) : nodes
  };
}

export function applyNodeInputChange(node: FlowiseCanvasNode, name: string, value: unknown): FlowiseCanvasNode {
  const linkedInputs = buildLinkedNodeInputs(name, value);
  const previousInputs = isRecord(node.data.inputs) ? node.data.inputs : {};
  const previousConfig = isRecord(node.data.config) ? node.data.config : {};
  const nextInputs = applyVisibleInputDefaults(node.data.inputParams ?? [], {
    ...previousInputs,
    [name]: value,
    ...linkedInputs
  });
  const nextParams = showHideInputParams({
    ...node.data,
    inputs: nextInputs
  });

  Object.keys(nextInputs).forEach((inputName) => {
    const inputParam = nextParams.find((param) => param.name === inputName);
    if (inputParam?.display === false) {
      delete nextInputs[inputName];
    }
  });

  return {
    ...node,
    data: {
      ...node.data,
      config: {
        ...previousConfig,
        ...nextInputs,
        ...linkedInputs
      },
      inputParams: nextParams,
      inputs: nextInputs
    }
  };
}

function buildLinkedNodeInputs(name: string, value: unknown): Record<string, unknown> {
  if (name === 'llmModel') {
    return { llmModelConfigId: value };
  }

  if (name === 'agentModel') {
    return { agentModelConfigId: value };
  }

  if (name === 'llmModelConfigId') {
    return { llmModel: value };
  }

  if (name === 'agentModelConfigId') {
    return { agentModel: value };
  }

  return {};
}

export function resolveCanvasMode(pathname: string): FlowiseCanvasMode {
  if (pathname.includes('/v2/marketplace')) {
    return 'marketplace-template';
  }

  if (pathname.includes('/v2/agentcanvas')) {
    return 'agentflow-v2';
  }

  if (pathname.includes('/agentcanvas') || pathname.includes('/workflows/')) {
    return 'agentflow';
  }

  if (pathname.includes('/marketplace')) {
    return 'marketplace';
  }

  return 'chatflow';
}

export function flowTypeFromMode(mode: FlowiseCanvasMode): string {
  if (mode.includes('agentflow')) {
    return 'Workflow';
  }

  return mode === 'marketplace-template' || mode === 'marketplace' ? 'Marketplace' : 'Chatflow';
}

function parseFlowData(jsonValue?: string | null): FlowiseFlowData {
  const parsed = safeParseRecord(jsonValue);
  const nodes = Array.isArray(parsed.nodes) ? (parsed.nodes as FlowiseCanvasNode[]).map(normalizeNodeInputState) : [];
  const edges = Array.isArray(parsed.edges) ? (parsed.edges as FlowiseCanvasEdge[]).map(normalizeEdgeType) : [];
  const viewport = isViewport(parsed.viewport) ? parsed.viewport : defaultViewport;
  return { edges, nodes, viewport };
}

function normalizeNodeInputState(node: FlowiseCanvasNode): FlowiseCanvasNode {
  const inputs = isRecord(node.data.inputs) ? node.data.inputs : isRecord(node.data.config) ? node.data.config : {};
  const category = normalizeAgentflowText(node.data.category);
  const description = normalizeAgentflowText(node.data.description);
  const displayName = normalizeAgentflowText(node.data.displayName);
  const label = normalizeAgentflowText(node.data.label);
  const type = normalizeAgentflowText(node.data.type);
  const inputParams = showHideInputParams({
    ...node.data,
    inputs
  });
  const inputAnchors = normalizeInputAnchors(node.data.inputAnchors, node.id);

  return {
    ...node,
    type: normalizeNodeRendererType(node),
    width: node.width ?? defaultFlowiseNodeWidth,
    height: node.height ?? defaultFlowiseNodeHeight,
    data: {
      ...node.data,
      category,
      description,
      displayName: displayName ?? node.data.displayName,
      inputAnchors,
      inputParams,
      inputs,
      label: label ?? node.data.label,
      type: type ?? node.data.type
    }
  };
}

function normalizeNodeRendererType(node: FlowiseCanvasNode): string | undefined {
  if (node.data.stickyNote || node.type === 'flowiseStickyNote') {
    return 'flowiseStickyNote';
  }

  const nodeType = String(node.data.nodeType ?? node.data.name ?? node.type ?? '').toLowerCase();
  if (nodeType.includes('iteration')) {
    return 'flowiseIterationNode';
  }

  if (node.type === 'flowiseAgentFlowNode' || node.type === 'agentFlowNode' || nodeType.includes('agentflow')) {
    return 'flowiseWorkflowNode';
  }

  return node.type ?? 'flowiseCanvasNode';
}

function normalizeEdgeType(edge: FlowiseCanvasEdge): FlowiseCanvasEdge {
  if (edge.type === 'buttonedge' || edge.type === 'agentFlowEdge' || edge.type === 'flowiseAgentFlowEdge') {
    return {
      ...edge,
      type: 'flowiseWorkflowEdge'
    };
  }

  return {
    ...edge,
    type: edge.type ?? 'flowiseButtonEdge'
  };
}

function normalizeAgentflowText(value: unknown): string | undefined {
  return typeof value === 'string'
    ? value
        .replace(/Start\s+Agentflow/gi, 'Start')
        .replace(/Agentflow\s*V2/gi, 'Workflow')
        .replace(/Agentflow\s*v2/gi, 'Workflow')
        .replace(/\bAgentflow\b/gi, 'Workflow')
    : undefined;
}

function structuredCloneWithoutFunctions<T extends Record<string, unknown>>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function safeParseRecord(jsonValue?: string | null): Record<string, unknown> {
  if (!jsonValue) {
    return {};
  }

  try {
    const parsed = JSON.parse(jsonValue) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? (parsed as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

function isViewport(value: unknown): value is Viewport {
  return Boolean(value && typeof value === 'object' && 'x' in value && 'y' in value && 'zoom' in value);
}

function applyVisibleInputDefaults(params: FlowiseCanvasNode['data']['inputParams'], inputs: Record<string, unknown>): Record<string, unknown> {
  const result = { ...inputs };
  const evaluatedParams = showHideInputParams({ inputs: result, inputParams: params ?? [] });
  evaluatedParams.forEach((param) => {
    const defaultValue = resolveInputDefaultValue(param);
    if (defaultValue === undefined || param.display === false || result[param.name] !== undefined) {
      return;
    }

    result[param.name] = defaultValue;
  });
  return result;
}

function showHideInputParams(nodeData: Pick<FlowiseCanvasNode['data'], 'inputParams'> & { inputs?: Record<string, unknown> }) {
  const params = (nodeData.inputParams ?? []).map((param) => ({
    ...param,
    options: param.options?.map((option) => ({ ...option }))
  }));
  const effectiveInputs = withDeclaredDefaults(params, nodeData.inputs ?? {});

  return params.map((param) => {
    const nextParam = { ...param, display: true };
    if (nextParam.show) {
      applyDisplayOperation(effectiveInputs, nextParam, 'show');
    }
    if (nextParam.hide) {
      applyDisplayOperation(effectiveInputs, nextParam, 'hide');
    }
    if (nextParam.type === 'options' && nextParam.options) {
      nextParam.options = nextParam.options.filter((option) => {
        if (!option.show && !option.hide) {
          return true;
        }

        const synthetic = { display: true, hide: option.hide, show: option.show };
        if (option.show) {
          applyDisplayOperation(effectiveInputs, synthetic, 'show');
        }
        if (option.hide) {
          applyDisplayOperation(effectiveInputs, synthetic, 'hide');
        }
        return synthetic.display !== false;
      });
    }
    return nextParam;
  });
}

function withDeclaredDefaults(params: NonNullable<FlowiseCanvasNode['data']['inputParams']>, inputs: Record<string, unknown>) {
  const merged = { ...inputs };
  params.forEach((param) => {
    const defaultValue = resolveInputDefaultValue(param);
    if (defaultValue !== undefined && merged[param.name] === undefined) {
      merged[param.name] = defaultValue;
    }
  });
  return merged;
}

function resolveInputDefaultValue(param: { default?: unknown; defaultJson?: string | null }): unknown {
  if (param.default !== undefined) {
    return param.default;
  }

  if (param.defaultJson === undefined || param.defaultJson === null || param.defaultJson === 'null') {
    return '';
  }

  try {
    const parsed = JSON.parse(param.defaultJson) as unknown;
    return parsed ?? '';
  } catch {
    return param.defaultJson;
  }
}

function applyDisplayOperation(
  inputs: Record<string, unknown>,
  target: { display?: boolean; hide?: Record<string, unknown>; show?: Record<string, unknown> },
  displayType: 'hide' | 'show'
) {
  const conditions = target[displayType] ?? {};
  Object.entries(conditions).forEach(([path, comparisonValue]) => {
    const actualValue = readInputPath(inputs, path);
    const matched = matchesDisplayValue(actualValue, comparisonValue);
    if ((displayType === 'show' && !matched) || (displayType === 'hide' && matched)) {
      target.display = false;
    }
  });
}

function readInputPath(inputs: Record<string, unknown>, path: string): unknown {
  return path.split('.').reduce<unknown>((current, segment) => {
    if (!isRecord(current)) {
      return undefined;
    }
    return current[segment];
  }, inputs);
}

function matchesDisplayValue(actualValue: unknown, comparisonValue: unknown): boolean {
  if (Array.isArray(actualValue)) {
    if (Array.isArray(comparisonValue)) {
      return comparisonValue.some((item) => actualValue.includes(item));
    }
    return actualValue.some((item) => matchesDisplayValue(item, comparisonValue));
  }

  if (Array.isArray(comparisonValue)) {
    return comparisonValue.includes(actualValue);
  }

  if (typeof comparisonValue === 'string') {
    return String(actualValue ?? '') === comparisonValue || new RegExp(comparisonValue).test(String(actualValue ?? ''));
  }

  return JSON.stringify(actualValue) === JSON.stringify(comparisonValue);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}

function isWorkflowMode(mode: FlowiseCanvasMode): boolean {
  return mode.includes('agentflow') || mode === 'marketplace-template';
}

function resolveAgentflowEdgeData(connection: Connection, nodes: FlowiseCanvasNode[]): FlowiseCanvasEdgeData {
  const sourceNodeName = resolveHandleNodeName(connection.sourceHandle);
  const targetNodeName = resolveHandleNodeName(connection.targetHandle);
  const edgeLabel = resolveAgentflowEdgeLabel(connection.sourceHandle, sourceNodeName);
  const isHumanInput = sourceNodeName === 'humanInputAgentflow';
  const sourceNode = nodes.find((node) => node.id === connection.source);
  const targetNode = nodes.find((node) => node.id === connection.target);
  const isWithinIterationNode = Boolean(sourceNode?.parentId && targetNode?.parentId && sourceNode.parentId === targetNode.parentId);

  return {
    conditionLabel: sourceNodeName === 'conditionAgentflow' || sourceNodeName === 'conditionAgentAgentflow' ? edgeLabel : null,
    edgeLabel,
    humanInputLabel: isHumanInput ? edgeLabel : null,
    isHumanInput,
    isWithinIterationNode,
    sourceColor: agentflowIconColors[sourceNodeName] ?? defaultAgentflowEdgeColor,
    targetColor: agentflowIconColors[targetNodeName] ?? defaultAgentflowEdgeColor
  };
}

function resolveHandleNodeName(handleId?: string | null): string {
  return (handleId ?? '').split('_')[0] ?? '';
}

function resolveAgentflowEdgeLabel(sourceHandle?: string | null, sourceNodeName = resolveHandleNodeName(sourceHandle)): string | null {
  if (!sourceHandle) {
    return null;
  }

  if (sourceNodeName === 'conditionAgentflow' || sourceNodeName === 'conditionAgentAgentflow') {
    const edgeLabel = sourceHandle.split('-').pop();
    return Number.isNaN(Number(edgeLabel)) ? '0' : String(edgeLabel);
  }

  if (sourceNodeName === 'humanInputAgentflow') {
    const edgeLabel = sourceHandle.split('-').pop();
    return edgeLabel === '0' ? 'proceed' : 'reject';
  }

  return null;
}
