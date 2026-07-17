import type { Connection, Edge, Node, XYPosition } from '@xyflow/react';

import type {
  MicroflowDefinition,
  MicroflowEdge,
  MicroflowNode,
  MicroflowNodeType,
  MicroflowValueExpression
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import type { FlowCanvasButtonEdgeData } from '../../../../shared/flow-canvas/FlowCanvasButtonEdge';
import {
  canConnectCanvasNodes,
  createUniqueCanvasNodeId,
  deleteCanvasEdge
} from '../../../../shared/flow-canvas/flowCanvasGraph';

import { createDefaultMicroflowNodeConfig, microflowNodeCatalog } from './microflowDefaults';
import {
  createGlobalVariableNodeSummary,
  isGlobalVariablesNode,
  readGlobalVariableNodeVariables,
  removeGlobalVariableNodeDefinitions,
  syncGlobalVariableDefinitions
} from './microflowGlobalVariableNode';
import {
  getNodeConfigSummary,
  listNodeInputReferenceOptions,
  readNodeOutputSchema,
  readReturnOutputSchema,
  validateReturnOutputSchema,
  type MicroflowNodeReferenceOption
} from './microflowNodeContext';
import { normalizeVariableValueType } from './microflowVariableSchema';

export interface MicroflowCanvasVariableTag {
  invalid?: boolean;
  label: string;
  title: string;
  valueType?: string;
}

export interface MicroflowCanvasNodeData extends Record<string, unknown> {
  configSummary?: string;
  description: string;
  inputTags: MicroflowCanvasVariableTag[];
  microflowNode: MicroflowNode;
  onDeleteNode?: (nodeId: string) => void;
  onDuplicateNode?: (nodeId: string) => void;
  onEditNodeConfig?: (nodeId: string) => void;
  outputTags: MicroflowCanvasVariableTag[];
  title: string;
}

export interface MicroflowCanvasEdgeData extends FlowCanvasButtonEdgeData {
  condition?: string | null;
  label?: string | null;
}

export type MicroflowCanvasNode = Node<MicroflowCanvasNodeData>;
export type MicroflowCanvasEdge = Edge<MicroflowCanvasEdgeData>;

export const microflowCanvasNodeSize = {
  height: 88,
  width: 224
} as const;
const defaultNodeOffset = { x: 120, y: 96 };
const defaultNodeGrid = {
  columns: 4,
  gapX: 280,
  gapY: 156,
  margin: 24
} as const;

export function createMicroflowCanvasNodes(definition: MicroflowDefinition, selectedNodeId: string | null): MicroflowCanvasNode[] {
  return definition.nodes.map((node, index) => {
    const catalogItem = findMicroflowCatalogItem(node.type);
    return {
      data: {
        configSummary: isGlobalVariablesNode(node) ? createGlobalVariableNodeSummary(node) : getNodeConfigSummary(definition, node),
        description: catalogItem?.description ?? node.type,
        inputTags: createNodeInputTags(definition, node),
        microflowNode: node,
        outputTags: createNodeOutputTags(definition, node),
        title: catalogItem?.title ?? node.name
      },
      id: node.id,
      position: {
        x: readPosition(node.x, defaultNodeOffset.x + index * (microflowCanvasNodeSize.width + 32)),
        y: readPosition(node.y, defaultNodeOffset.y + index * 18)
      },
      selected: selectedNodeId === node.id,
      type: 'microflowCanvasNode',
      width: microflowCanvasNodeSize.width,
      height: microflowCanvasNodeSize.height
    };
  });
}

function createNodeInputTags(definition: MicroflowDefinition, node: MicroflowNode): MicroflowCanvasVariableTag[] {
  if (node.type === 'start' || isGlobalVariablesNode(node)) {
    return [];
  }

  const references = listNodeInputReferenceOptions(definition, node.id);
  const expressions = collectExpressions(node.config);
  const deduped = dedupeExpressions(expressions);
  return deduped.map((expression) => createInputTag(expression, references));
}

function createInputTag(
  expression: MicroflowValueExpression,
  references: MicroflowNodeReferenceOption[]
): MicroflowCanvasVariableTag {
  const matched = references.find((option) => expressionMatches(option.expression, expression));
  if (matched) {
    return {
      label: shortReferenceLabel(matched.label),
      title: `${matched.label} · ${matched.description}`,
      valueType: matched.valueType
    };
  }

  const reference = expression.ref;
  const fallbackLabel = reference
    ? `${reference.outputKey || reference.variableId}${reference.fieldPath.length ? `.${reference.fieldPath.join('.')}` : ''}`
    : expression.kind === 'function'
      ? expression.functionId || '函数'
      : expression.kind || '表达式';
  return {
    invalid: true,
    label: fallbackLabel,
    title: fallbackLabel,
    valueType: normalizeVariableValueType(expression.dataType)
  };
}

function createNodeOutputTags(definition: MicroflowDefinition, node: MicroflowNode): MicroflowCanvasVariableTag[] {
  if (isGlobalVariablesNode(node)) {
    return readGlobalVariableNodeVariables(node)
      .filter((variable) => variable.variableCode.trim())
      .map((variable) => ({
        label: variable.variableCode.trim(),
        title: `${variable.variableName || variable.variableCode} / ${normalizeVariableValueType(variable.valueType)}`,
        valueType: normalizeVariableValueType(variable.valueType)
      }));
  }

  if (node.type === 'end') {
    return [];
  }

  if (node.type === 'start') {
    return definition.inputs
      .filter((input) => input.variableCode.trim())
      .map((input) => ({
        label: input.variableCode.trim(),
        title: `${input.variableName || input.variableCode} / ${normalizeVariableValueType(input.valueType)}`,
        valueType: normalizeVariableValueType(input.valueType)
      }));
  }

  const schema = node.type === 'return'
    ? readReturnOutputSchema(definition, node)
    : readNodeOutputSchema(definition, node);
  if (!schema?.variableCode) {
    if (node.type === 'return') {
      return [{
        invalid: true,
        label: translateCurrentLiteral("未配置结构"),
        title: 'Return 节点缺少 outputSchema 返回结构'
      }];
    }

    return [];
  }

  const fields = schema.fields.filter((field) => field.fieldCode.trim());
  if (fields.length === 0) {
    return [{
      invalid: node.type === 'return',
      label: schema.variableCode,
      title: node.type === 'return'
        ? `${schema.variableName || schema.variableCode} / 配置错误: 未配置返回字段`
        : `${schema.variableName || schema.variableCode} / ${normalizeVariableValueType(schema.valueType)}`,
      valueType: normalizeVariableValueType(schema.valueType)
    }];
  }

  const returnIssues = node.type === 'return' ? validateReturnOutputSchema(definition, node) : [];
  return fields.map((field, index) => {
    const fieldCode = field.fieldCode.trim();
    const invalid = returnIssues.some((issue) => issue.severity === 'error' && issue.path.startsWith(`outputSchema.fields[${index}]`));
    return {
      invalid,
      label: fieldCode,
      title: invalid
        ? `${schema.variableCode}.${fieldCode} · 配置错误`
        : `${schema.variableCode}.${fieldCode} · ${field.fieldName || fieldCode}`,
      valueType: normalizeVariableValueType(field.dataType)
    };
  });
}

function collectExpressions(value: unknown, expressions: MicroflowValueExpression[] = []): MicroflowValueExpression[] {
  if (isExpression(value)) {
    if (value.kind === 'ref') {
      expressions.push(value);
    }
    (value.args ?? []).forEach((item) => collectExpressions(item, expressions));
    (value.items ?? []).forEach((item) => collectExpressions(item, expressions));
    Object.values(value.properties ?? {}).forEach((item) => collectExpressions(item, expressions));
    return expressions;
  }

  if (Array.isArray(value)) {
    value.forEach((item) => collectExpressions(item, expressions));
    return expressions;
  }

  if (value && typeof value === 'object') {
    Object.entries(value as Record<string, unknown>)
      .forEach(([, item]) => collectExpressions(item, expressions));
  }

  return expressions;
}

function dedupeExpressions(expressions: MicroflowValueExpression[]): MicroflowValueExpression[] {
  const seen = new Set<string>();
  return expressions.filter((expression) => {
    const key = expression.ref
      ? `${expression.kind}:${expression.ref.sourceType}:${expression.ref.variableId}:${expression.ref.outputKey ?? ''}:${expression.ref.fieldPath.join('.')}:${expression.dataType}`
      : `${expression.kind}:${expression.functionId ?? ''}:${JSON.stringify(expression.value)}:${expression.dataType}`;
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });
}

function expressionMatches(reference: MicroflowValueExpression, expression: MicroflowValueExpression): boolean {
  if (reference.kind !== 'ref' || expression.kind !== 'ref' || !reference.ref || !expression.ref) {
    return false;
  }

  return reference.ref.sourceType === expression.ref.sourceType
    && reference.ref.variableId === expression.ref.variableId
    && (reference.ref.outputKey ?? '') === (expression.ref.outputKey ?? '')
    && reference.ref.fieldPath.join('.') === expression.ref.fieldPath.join('.');
}

function isExpression(value: unknown): value is MicroflowValueExpression {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value) && 'kind' in value);
}

function shortReferenceLabel(label: string): string {
  const parts = label.split('.');
  return parts.length > 1 ? parts.slice(-2).join('.') : label;
}

export function createMicroflowCanvasEdges(definition: MicroflowDefinition, selectedEdgeId: string | null): MicroflowCanvasEdge[] {
  return definition.edges.map((edge) => ({
    animated: Boolean(edge.condition),
    data: {
      condition: edge.condition,
      label: edge.condition ?? null
    },
    id: edge.id,
    label: edge.condition ?? undefined,
    selected: selectedEdgeId === edge.id,
    source: edge.sourceNodeId,
    target: edge.targetNodeId,
    type: 'microflowButtonEdge'
  }));
}

export function applyMicroflowCanvasNodePositions(definition: MicroflowDefinition, nodes: MicroflowCanvasNode[]): MicroflowDefinition {
  const positions = new Map(nodes.map((node) => [node.id, node.position]));
  return {
    ...definition,
    nodes: definition.nodes.map((node) => {
      const position = positions.get(node.id);
      return position ? { ...node, x: roundPosition(position.x), y: roundPosition(position.y) } : node;
    })
  };
}

export function canConnectMicroflowNodes(connection: Connection, definition: MicroflowDefinition): boolean {
  const sourceNode = definition.nodes.find((node) => node.id === connection.source);
  const targetNode = definition.nodes.find((node) => node.id === connection.target);
  if (isGlobalVariablesNode(sourceNode) || isGlobalVariablesNode(targetNode)) {
    return false;
  }

  return canConnectCanvasNodes(
    connection,
    definition.nodes.map((node) => ({ id: node.id })),
    definition.edges.map(toCanvasEdgeLike),
    {
      preventDuplicate: true,
      sameEdge: (edge, candidate) => edge.source === candidate.source && edge.target === candidate.target
    }
  );
}

export function connectMicroflowNodes(definition: MicroflowDefinition, connection: Connection): MicroflowDefinition {
  if (!canConnectMicroflowNodes(connection, definition) || !connection.source || !connection.target) {
    return definition;
  }

  const edge: MicroflowEdge = {
    id: createMicroflowEdgeId(connection.source, connection.target, definition.edges),
    sourceNodeId: connection.source,
    targetNodeId: connection.target
  };
  return {
    ...definition,
    edges: [...definition.edges, edge]
  };
}

export function addMicroflowCanvasNode(
  definition: MicroflowDefinition,
  type: MicroflowNodeType | string,
  title: string,
  position?: XYPosition
): { definition: MicroflowDefinition; nodeId: string } {
  const id = createUniqueCanvasNodeId(type, definition.nodes);
  const nodePosition = position ?? findNextCanvasNodePosition(definition);
  const node: MicroflowNode = {
    config: createDefaultMicroflowNodeConfig(type),
    id,
    name: title,
    type,
    x: roundPosition(nodePosition.x),
    y: roundPosition(nodePosition.y)
  };
  if (isGlobalVariablesNode(node)) {
    node.config = {
      ...node.config,
      variables: []
    };
  }

  return {
    definition: {
      ...definition,
      nodes: [...definition.nodes, node]
    },
    nodeId: id
  };
}

function findNextCanvasNodePosition(definition: MicroflowDefinition): XYPosition {
  for (let index = 0; index < 500; index += 1) {
    const candidate = createGridPosition(index);
    if (!definition.nodes.some((node) => nodeOverlapsGridCandidate(node, candidate))) {
      return candidate;
    }
  }

  return createGridPosition(definition.nodes.length);
}

function createGridPosition(index: number): XYPosition {
  return {
    x: defaultNodeOffset.x + (index % defaultNodeGrid.columns) * defaultNodeGrid.gapX,
    y: defaultNodeOffset.y + Math.floor(index / defaultNodeGrid.columns) * defaultNodeGrid.gapY
  };
}

function nodeOverlapsGridCandidate(node: MicroflowNode, candidate: XYPosition): boolean {
  return Math.abs(node.x - candidate.x) < microflowCanvasNodeSize.width + defaultNodeGrid.margin
    && Math.abs(node.y - candidate.y) < microflowCanvasNodeSize.height + defaultNodeGrid.margin;
}

export function duplicateMicroflowCanvasNode(
  definition: MicroflowDefinition,
  nodeId: string
): { definition: MicroflowDefinition; nodeId: string | null } {
  const source = definition.nodes.find((node) => node.id === nodeId);
  if (!source) {
    return { definition, nodeId: null };
  }

  const nextId = createUniqueCanvasNodeId(source.type, definition.nodes);
  const duplicate: MicroflowNode = {
    ...source,
    config: cloneRecord(source.config),
    id: nextId,
    name: `${source.name} Copy`,
    x: source.x + microflowCanvasNodeSize.width + 40,
    y: source.y + 20
  };
  if (isGlobalVariablesNode(duplicate)) {
    const variables = readGlobalVariableNodeVariables(source).map((variable, index) => ({
      ...variable,
      sourceNodeId: nextId,
      variableCode: `${variable.variableCode || 'global'}_copy_${index + 1}`,
      variableName: `${variable.variableName || variable.variableCode || '全局变量'} Copy`
    }));
    duplicate.config = {
      ...duplicate.config,
      variables
    };
  }

  return {
    definition: syncGlobalVariableDefinitions({
      ...definition,
      nodes: [...definition.nodes, duplicate]
    }),
    nodeId: nextId
  };
}

export function deleteMicroflowCanvasNode(definition: MicroflowDefinition, nodeId: string): MicroflowDefinition {
  return deleteMicroflowCanvasNodes(definition, [nodeId]);
}

export function deleteMicroflowCanvasNodes(definition: MicroflowDefinition, nodeIds: string[]): MicroflowDefinition {
  const removedNodeIds = new Set(nodeIds);
  return removeGlobalVariableNodeDefinitions({
    ...definition,
    edges: definition.edges.filter((edge) => !removedNodeIds.has(edge.sourceNodeId) && !removedNodeIds.has(edge.targetNodeId)),
    nodes: definition.nodes.filter((node) => !removedNodeIds.has(node.id))
  }, nodeIds);
}

export function deleteMicroflowCanvasEdge(definition: MicroflowDefinition, edgeId: string): MicroflowDefinition {
  const nextEdges = deleteCanvasEdge(edgeId, definition.edges.map(toCanvasEdgeLike));
  const edgeIds = new Set(nextEdges.map((edge) => edge.id));
  return {
    ...definition,
    edges: definition.edges.filter((edge) => edgeIds.has(edge.id))
  };
}

export function updateMicroflowEdgeCondition(definition: MicroflowDefinition, edgeId: string, condition: string): MicroflowDefinition {
  const trimmed = condition.trim();
  return {
    ...definition,
    edges: definition.edges.map((edge) => edge.id === edgeId
      ? { ...edge, condition: trimmed || null }
      : edge)
  };
}

export function findMicroflowNode(definition: MicroflowDefinition, nodeId: string | null): MicroflowNode | null {
  return nodeId ? definition.nodes.find((node) => node.id === nodeId) ?? null : null;
}

export function findMicroflowEdge(definition: MicroflowDefinition, edgeId: string | null): MicroflowEdge | null {
  return edgeId ? definition.edges.find((edge) => edge.id === edgeId) ?? null : null;
}

function createMicroflowEdgeId(sourceNodeId: string, targetNodeId: string, edges: MicroflowEdge[]): string {
  const baseId = `flow_${sourceNodeId}_${targetNodeId}`;
  const used = new Set(edges.map((edge) => edge.id));
  if (!used.has(baseId)) {
    return baseId;
  }

  let index = 1;
  let candidate = `${baseId}_${index}`;
  while (used.has(candidate)) {
    index += 1;
    candidate = `${baseId}_${index}`;
  }

  return candidate;
}

function toCanvasEdgeLike(edge: MicroflowEdge) {
  return {
    id: edge.id,
    source: edge.sourceNodeId,
    target: edge.targetNodeId
  };
}

function findMicroflowCatalogItem(type: string) {
  return microflowNodeCatalog.find((item) => item.type === type);
}

function roundPosition(value: number): number {
  return Math.round(value);
}

function readPosition(value: number, fallback: number): number {
  return Number.isFinite(value) ? value : fallback;
}

function cloneRecord(value: Record<string, unknown>): Record<string, unknown> {
  return JSON.parse(JSON.stringify(value)) as Record<string, unknown>;
}
