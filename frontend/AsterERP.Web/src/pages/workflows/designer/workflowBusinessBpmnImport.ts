import type { XYPosition } from '@xyflow/react';

import type { TranslateFn } from './workflowBusinessI18n';
import {
  createBusinessNode,
  normalizeBusinessDesign,
  type BusinessNodeType,
  type WorkflowBusinessDesign,
  type WorkflowBusinessEdge,
  type WorkflowBusinessNode
} from './workflowBusinessModel';

export interface WorkflowBpmnUnsupportedElement {
  id: string;
  type: string;
  reason: string;
}

export interface WorkflowBpmnImportDiff {
  addedNodeIds: string[];
  removedNodeIds: string[];
  changedNodeIds: string[];
  addedEdgeIds: string[];
  removedEdgeIds: string[];
}

export interface WorkflowBpmnImportResult {
  design: WorkflowBusinessDesign | null;
  diff: WorkflowBpmnImportDiff;
  unsupportedElements: WorkflowBpmnUnsupportedElement[];
  warnings: string[];
  error?: string;
}

const supportedElementTypes: Record<string, BusinessNodeType> = {
  callActivity: 'subprocess',
  endEvent: 'end',
  exclusiveGateway: 'condition',
  intermediateCatchEvent: 'timeout',
  startEvent: 'start',
  userTask: 'approval'
};

export function importBpmnToBusinessDesign(
  xml: string,
  currentDesign: WorkflowBusinessDesign,
  translate: TranslateFn = (key) => key
): WorkflowBpmnImportResult {
  const emptyDiff = createEmptyDiff();
  if (!xml.trim()) {
    return { design: null, diff: emptyDiff, unsupportedElements: [], warnings: [], error: 'BPMN XML is empty.' };
  }

  if (typeof DOMParser === 'undefined') {
    return { design: null, diff: emptyDiff, unsupportedElements: [], warnings: [], error: 'BPMN XML parser is unavailable.' };
  }

  const document = new DOMParser().parseFromString(xml, 'application/xml');
  const parserError = Array.from(document.getElementsByTagName('*')).find((element) => element.localName === 'parsererror');
  if (parserError) {
    return { design: null, diff: emptyDiff, unsupportedElements: [], warnings: [], error: parserError.textContent?.trim() || 'Invalid BPMN XML.' };
  }

  const process = Array.from(document.getElementsByTagName('*')).find((element) => element.localName === 'process');
  if (!process) {
    return { design: null, diff: emptyDiff, unsupportedElements: [], warnings: [], error: 'BPMN process element is missing.' };
  }

  const unsupportedElements: WorkflowBpmnUnsupportedElement[] = [];
  const warnings: string[] = [];
  const positions = readDiagramPositions(document);
  const nodes: WorkflowBusinessNode[] = [];
  const nodeIds = new Set<string>();

  Array.from(process.children).forEach((element, index) => {
    const type = element.localName;
    if (type === 'sequenceFlow') {
      return;
    }

    const id = element.getAttribute('id')?.trim() || `${type}_${index + 1}`;
    const configuredType = supportedElementTypes[type];
    const businessType = configuredType === 'approval' && readNamespacedAttribute(element, 'category') === 'cc' ? 'cc' : configuredType;
    if (!businessType || (type === 'intermediateCatchEvent' && !hasTimerDefinition(element))) {
      unsupportedElements.push({
        id,
        type,
        reason: type === 'intermediateCatchEvent' ? 'Only timer intermediate catch events are supported.' : 'Element type is not representable by the business designer.'
      });
      return;
    }

    if (nodeIds.has(id)) {
      unsupportedElements.push({ id, type, reason: 'Duplicate BPMN element id.' });
      return;
    }

    const position = positions.get(id) ?? fallbackPosition(index);
    const node = createImportedNode(element, businessType, id, position, translate, warnings);
    nodes.push(node);
    nodeIds.add(id);
  });

  const edges: WorkflowBusinessEdge[] = [];
  Array.from(process.children).forEach((element, index) => {
    if (element.localName !== 'sequenceFlow') {
      return;
    }

    const id = element.getAttribute('id')?.trim() || `flow_${index + 1}`;
    const source = element.getAttribute('sourceRef')?.trim() ?? '';
    const target = element.getAttribute('targetRef')?.trim() ?? '';
    if (!nodeIds.has(source) || !nodeIds.has(target)) {
      unsupportedElements.push({ id, type: 'sequenceFlow', reason: 'Sequence flow references an unsupported or missing node.' });
      return;
    }

    edges.push({
      id,
      source,
      target,
      conditionExpression: readChildText(element, 'conditionExpression') || undefined,
      label: element.getAttribute('name') || undefined
    });
  });

  if (nodes.length === 0) {
    return { design: null, diff: emptyDiff, unsupportedElements, warnings, error: 'BPMN contains no supported business nodes.' };
  }

  const normalized = normalizeBusinessDesign({
    ...currentDesign,
    nodes,
    edges,
    selectedNodeId: nodes.some((node) => node.id === currentDesign.selectedNodeId) ? currentDesign.selectedNodeId : nodes[0].id
  }, translate);
  const design = edges.length > 0 ? normalized : { ...normalized, edges: [] };

  return {
    design,
    diff: diffWorkflowBusinessDesign(currentDesign, design),
    unsupportedElements,
    warnings
  };
}

export function diffWorkflowBusinessDesign(current: WorkflowBusinessDesign, next: WorkflowBusinessDesign): WorkflowBpmnImportDiff {
  const currentNodes = new Map(current.nodes.map((node) => [node.id, node]));
  const nextNodes = new Map(next.nodes.map((node) => [node.id, node]));
  const currentEdges = new Map(current.edges.map((edge) => [edge.id, edge]));
  const nextEdges = new Map(next.edges.map((edge) => [edge.id, edge]));

  return {
    addedNodeIds: [...nextNodes.keys()].filter((id) => !currentNodes.has(id)),
    removedNodeIds: [...currentNodes.keys()].filter((id) => !nextNodes.has(id)),
    changedNodeIds: [...nextNodes.keys()].filter((id) => {
      const previous = currentNodes.get(id);
      const nextNode = nextNodes.get(id);
      return Boolean(previous && nextNode && (previous.type !== nextNode.type || previous.label !== nextNode.label || previous.position.x !== nextNode.position.x || previous.position.y !== nextNode.position.y));
    }),
    addedEdgeIds: [...nextEdges.keys()].filter((id) => !currentEdges.has(id)),
    removedEdgeIds: [...currentEdges.keys()].filter((id) => !nextEdges.has(id))
  };
}

function createImportedNode(
  element: Element,
  type: BusinessNodeType,
  id: string,
  position: XYPosition,
  translate: TranslateFn,
  warnings: string[]
): WorkflowBusinessNode {
  const node = createBusinessNode(type, id, element.getAttribute('name')?.trim() || id, position, translate);
  const config = readNodeConfig(element, id, warnings);
  const configWithLegacyMappings = config as Partial<WorkflowBusinessNode> & { variableMappings?: WorkflowBusinessNode['variableMappingRows'] };
  const importedNode: WorkflowBusinessNode = {
    ...node,
    ...config,
    id,
    label: element.getAttribute('name')?.trim() || id,
    position,
    participantIds: config.participantIds ?? node.participantIds,
    participantNames: config.participantNames ?? node.participantNames,
    participantExpression: config.participantExpression ?? node.participantExpression,
    participantFieldKey: config.participantFieldKey ?? node.participantFieldKey,
    subProcessConfig: {
      ...node.subProcessConfig,
      ...(config.subProcessConfig ?? {}),
      calledElement: element.getAttribute('calledElement')?.trim() || config.subProcessConfig?.calledElement || ''
    },
    timeoutPolicy: {
      ...node.timeoutPolicy,
      ...(config.timeoutPolicy ?? {}),
      hours: readTimeoutHours(element, config.timeoutPolicy?.hours ?? node.timeoutPolicy.hours)
    },
    nativeExtensions: typeof config.nativeExtensions === 'string' ? config.nativeExtensions : JSON.stringify(config.nativeExtensions ?? {}),
    fieldPermissions: config.fieldPermissions?.length ? config.fieldPermissions : stableFieldPermissions(node),
    notificationRules: config.notificationRules?.length ? config.notificationRules : stableNotificationRules(node),
    variableMappingRows: configWithLegacyMappings.variableMappingRows?.length
      ? configWithLegacyMappings.variableMappingRows
      : configWithLegacyMappings.variableMappings?.length
        ? configWithLegacyMappings.variableMappings
        : stableVariableMappings(node)
  };

  applyParticipantAttributes(importedNode, element);
  if (type === 'cc') {
    importedNode.participantType = 'dynamic';
  }
  if (type === 'condition') {
    importedNode.conditionExpression = importedNode.conditionExpression || '';
  }
  return importedNode;
}

function applyParticipantAttributes(node: WorkflowBusinessNode, element: Element): void {
  const assignee = element.getAttribute('assignee') ?? readNamespacedAttribute(element, 'assignee');
  const candidateGroups = element.getAttribute('candidateGroups') ?? readNamespacedAttribute(element, 'candidateGroups');
  const candidateUsers = element.getAttribute('candidateUsers') ?? readNamespacedAttribute(element, 'candidateUsers');
  const participant = assignee || candidateGroups || candidateUsers;
  if (!participant) {
    return;
  }

  const expressionMatch = participant.match(/^\$\{(.+)\}$/);
  if (expressionMatch) {
    const expression = expressionMatch[1];
    if (expression === 'starterUserId') node.participantType = 'starter';
    else if (expression === 'starterManagerUserId') node.participantType = 'starterManager';
    else if (expression === 'previousApproverUserId') node.participantType = 'previousApprover';
    else {
      node.participantType = 'formField';
      node.participantFieldKey = expression;
    }
    node.participantExpression = participant;
    return;
  }

  if (candidateGroups?.startsWith('role:')) {
    node.participantType = 'role';
    node.participantId = candidateGroups.slice('role:'.length);
  } else if (candidateGroups?.startsWith('dept:')) {
    node.participantType = 'department';
    node.participantId = candidateGroups.slice('dept:'.length);
  } else if (candidateGroups?.startsWith('position:')) {
    node.participantType = 'position';
    node.participantId = candidateGroups.slice('position:'.length);
  } else if (candidateGroups) {
    node.groupKey = candidateGroups;
  } else {
    node.participantType = 'user';
    node.participantId = participant;
  }
}

function readNodeConfig(element: Element, id: string, warnings: string[]): Partial<WorkflowBusinessNode> {
  const configElement = Array.from(element.getElementsByTagName('*')).find((child) => child.localName === 'nodeConfig');
  const raw = configElement?.textContent?.trim();
  if (!raw) {
    return {};
  }

  try {
    return JSON.parse(raw) as Partial<WorkflowBusinessNode>;
  } catch {
    warnings.push(`Node ${id} contains an invalid astererp:nodeConfig JSON value.`);
    return { nativeExtensions: JSON.stringify({ raw }) };
  }
}

function readDiagramPositions(document: Document): Map<string, XYPosition> {
  const positions = new Map<string, XYPosition>();
  Array.from(document.getElementsByTagName('*'))
    .filter((element) => element.localName === 'BPMNShape' || element.localName === 'bpmnShape')
    .forEach((shape) => {
      const bpmnElement = shape.getAttribute('bpmnElement');
      const bounds = Array.from(shape.getElementsByTagName('*')).find((element) => element.localName === 'Bounds' || element.localName === 'bounds');
      const x = Number(bounds?.getAttribute('x'));
      const y = Number(bounds?.getAttribute('y'));
      if (bpmnElement && Number.isFinite(x) && Number.isFinite(y)) {
        positions.set(bpmnElement, { x, y });
      }
    });
  return positions;
}

function hasTimerDefinition(element: Element): boolean {
  return Array.from(element.getElementsByTagName('*')).some((child) => child.localName === 'timerEventDefinition');
}

function readTimeoutHours(element: Element, fallback: number): number {
  const duration = readChildText(element, 'timeDuration');
  const match = duration.match(/^PT(\d+(?:\.\d+)?)H$/i);
  return match ? Number(match[1]) : fallback;
}

function readChildText(element: Element, localName: string): string {
  return Array.from(element.children).find((child) => child.localName === localName)?.textContent?.trim() ?? '';
}

function readNamespacedAttribute(element: Element, localName: string): string | null {
  return Array.from(element.attributes).find((attribute) => attribute.localName === localName)?.value ?? null;
}

function fallbackPosition(index: number): XYPosition {
  return { x: 40 + index * 220, y: 160 };
}

function stableFieldPermissions(node: WorkflowBusinessNode) {
  return node.fieldPermissions.map((permission) => ({ ...permission, id: `field_${node.id}` }));
}

function stableNotificationRules(node: WorkflowBusinessNode) {
  return node.notificationRules.map((rule) => ({ ...rule, id: `notice_${node.id}` }));
}

function stableVariableMappings(node: WorkflowBusinessNode) {
  return node.variableMappingRows.map((mapping) => ({ ...mapping, id: `var_${node.id}` }));
}

function createEmptyDiff(): WorkflowBpmnImportDiff {
  return { addedNodeIds: [], removedNodeIds: [], changedNodeIds: [], addedEdgeIds: [], removedEdgeIds: [] };
}
