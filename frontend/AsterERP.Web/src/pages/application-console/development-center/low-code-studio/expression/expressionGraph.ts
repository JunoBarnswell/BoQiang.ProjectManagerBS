import { createConversionPipeline } from '../binding/conversionPipeline';

import { getExpressionFunction } from './expressionFunctionCatalog';
import type { DesignerConversionStep, DesignerValueType } from './expressionTypes';

export type ExpressionNode = LiteralNode | ResourceRefNode | FunctionCallNode | ConversionNode | ConditionNode | LogicNode | ObjectNode | ArrayNode | TemplateNode | DefaultValueNode;
export interface LiteralNode { kind: 'literal'; value: unknown; valueType: DesignerValueType }
export interface ResourceRefNode { kind: 'resourceRef'; resourceId: string; valueType: DesignerValueType }
export interface FunctionCallNode { kind: 'functionCall'; functionId: string; args: ExpressionNode[]; valueType: DesignerValueType }
export interface ConversionNode { kind: 'conversion'; input: ExpressionNode; pipeline: DesignerConversionStep[]; valueType: DesignerValueType }
export interface ConditionNode { kind: 'condition'; when: ExpressionNode; then: ExpressionNode; otherwise: ExpressionNode; valueType: DesignerValueType }
export interface LogicNode { kind: 'logic'; operator: 'and' | 'or' | 'not'; args: ExpressionNode[]; valueType: 'boolean' }
export interface ObjectNode { kind: 'object'; properties: Record<string, ExpressionNode>; valueType: 'object' | 'json' }
export interface ArrayNode { kind: 'array'; items: ExpressionNode[]; valueType: 'array' }
export interface TemplateNode { kind: 'template'; items: ExpressionNode[]; valueType: 'string' }
export interface DefaultValueNode { kind: 'defaultValue'; input: ExpressionNode; fallback: unknown; valueType: DesignerValueType }
export interface ExpressionGraph { root: ExpressionNode | null }

export interface ExpressionDiagnostic { code: 'empty' | 'depth' | 'cycle' | 'node-limit' | 'type' | 'conversion' | 'arity' | 'unknown-function' | 'argument-type' | 'invalid-node' | 'unknown-resource'; message: string; path: string; severity: 'error' | 'warning'; suggestions?: string[] }

const MAX_EXPRESSION_DEPTH = 64;
const MAX_EXPRESSION_NODES = 256;

export function diagnoseExpressionGraph(graph: ExpressionGraph, expectedType: DesignerValueType): ExpressionDiagnostic[] {
  if (!graph.root) return [{ code: 'empty', message: 'Expression root is empty.', path: 'root', severity: 'error' }];
  const state = { count: 0 };
  return validateNode(graph.root, expectedType, 'root', 0, new WeakSet<object>(), state);
}

export function validateExpressionGraph(graph: ExpressionGraph, expectedType: DesignerValueType): string[] { return diagnoseExpressionGraph(graph, expectedType).map((diagnostic) => `${diagnostic.path}: ${diagnostic.message}`); }
export function serializeExpressionGraph(graph: ExpressionGraph): string { return JSON.stringify(canonicalize(graph)); }

export function parseExpressionGraph(value: unknown): ExpressionGraph | null {
  if (!isRecord(value) || !('root' in value)) return null;
  if (value.root !== null && !isExpressionNode(value.root)) return null;
  return value as unknown as ExpressionGraph;
}

function validateNode(node: ExpressionNode, expectedType: DesignerValueType, path: string, depth: number, visiting: WeakSet<object>, state: { count: number }): ExpressionDiagnostic[] {
  state.count += 1;
  if (state.count > MAX_EXPRESSION_NODES) return [{ code: 'node-limit', message: `Expression node count exceeds ${MAX_EXPRESSION_NODES}.`, path, severity: 'error' }];
  if (depth > MAX_EXPRESSION_DEPTH) return [{ code: 'depth', message: `Expression nesting exceeds ${MAX_EXPRESSION_DEPTH}.`, path, severity: 'error' }];
  if (visiting.has(node)) return [{ code: 'cycle', message: 'Expression graph contains a cycle.', path, severity: 'error' }];
  visiting.add(node);
  const errors: ExpressionDiagnostic[] = [];
  if (node.kind === 'resourceRef' && !node.resourceId.trim()) errors.push({ code: 'invalid-node', message: 'Resource id is required.', path, severity: 'error' });
  if (node.kind === 'conversion') {
    const pipeline = node.pipeline;
    const expected = createConversionPipeline(node.input.valueType, node.valueType);
    if (!pipeline.length || !expected.valid || pipeline[0]?.name !== expected.steps[0]?.name) errors.push({ code: 'conversion', message: `Conversion pipeline is not valid for ${node.input.valueType} to ${node.valueType}.`, path, severity: 'error', suggestions: expected.steps.map((step) => step.name) });
    errors.push(...validateNode(node.input, node.input.valueType, `${path}.input`, depth + 1, visiting, state));
  } else if (node.kind === 'condition') {
    errors.push(...validateNode(node.when, 'boolean', `${path}.when`, depth + 1, visiting, state), ...validateNode(node.then, node.valueType, `${path}.then`, depth + 1, visiting, state), ...validateNode(node.otherwise, node.valueType, `${path}.otherwise`, depth + 1, visiting, state));
  } else if (node.kind === 'logic') {
    const validArity = node.operator === 'not' ? node.args.length === 1 : node.args.length >= 2;
    if (!validArity) errors.push({ code: 'arity', message: `${node.operator} has an invalid argument count.`, path, severity: 'error' });
    node.args.forEach((argument, index) => errors.push(...validateNode(argument, 'boolean', `${path}.args[${index}]`, depth + 1, visiting, state)));
  } else if (node.kind === 'functionCall') {
    const definition = getExpressionFunction(node.functionId);
    if (!definition) errors.push({ code: 'unknown-function', message: `Function ${node.functionId} is not registered.`, path, severity: 'error', suggestions: ['coalesce', 'concat', 'toString'] });
    else {
      if (node.args.length < definition.minArgs || (definition.maxArgs !== null && node.args.length > definition.maxArgs)) errors.push({ code: 'arity', message: `${definition.label} has an invalid argument count.`, path, severity: 'error' });
      node.args.forEach((argument, index) => {
        const declared = definition.argumentTypes[Math.min(index, definition.argumentTypes.length - 1)] ?? 'any';
        const argumentType = declared === 'sameAsFirst' ? node.args[0]?.valueType ?? 'json' : declared;
        if (argumentType !== 'any' && argument.valueType !== argumentType && !(argumentType === 'string' && argument.valueType === 'array' && node.functionId === 'length')) errors.push({ code: 'argument-type', message: `Argument ${index + 1} must be ${argumentType}, received ${argument.valueType}.`, path: `${path}.args[${index}]`, severity: 'error' });
        errors.push(...validateNode(argument, argument.valueType, `${path}.args[${index}]`, depth + 1, visiting, state));
      });
    }
  } else if (node.kind === 'object') Object.entries(node.properties).forEach(([key, value]) => errors.push(...validateNode(value, value.valueType, `${path}.properties.${key}`, depth + 1, visiting, state)));
  else if (node.kind === 'array' || node.kind === 'template') node.items.forEach((item, index) => errors.push(...validateNode(item, item.valueType, `${path}.items[${index}]`, depth + 1, visiting, state)));
  else if (node.kind === 'defaultValue') errors.push(...validateNode(node.input, node.valueType, `${path}.input`, depth + 1, visiting, state));
  if (node.valueType !== expectedType && expectedType !== 'json') errors.push({ code: 'type', message: `Expected ${expectedType}, received ${node.valueType}.`, path, severity: 'error', suggestions: createConversionPipeline(node.valueType, expectedType).steps.map((step) => step.name) });
  visiting.delete(node);
  return errors;
}

function isExpressionNode(value: unknown): value is ExpressionNode {
  if (!isRecord(value) || typeof value.kind !== 'string' || typeof value.valueType !== 'string') return false;
  switch (value.kind) {
    case 'literal': return true;
    case 'resourceRef': return typeof value.resourceId === 'string';
    case 'functionCall': return typeof value.functionId === 'string' && Array.isArray(value.args) && value.args.every(isExpressionNode);
    case 'conversion': return isExpressionNode(value.input) && Array.isArray(value.pipeline) && value.pipeline.every(isRecord);
    case 'condition': return isExpressionNode(value.when) && isExpressionNode(value.then) && isExpressionNode(value.otherwise);
    case 'logic': return (value.operator === 'and' || value.operator === 'or' || value.operator === 'not') && Array.isArray(value.args) && value.args.every(isExpressionNode);
    case 'object': return isRecord(value.properties) && Object.values(value.properties).every(isExpressionNode);
    case 'array':
    case 'template': return Array.isArray(value.items) && value.items.every(isExpressionNode);
    case 'defaultValue': return isExpressionNode(value.input);
    default: return false;
  }
}

function canonicalize(value: unknown): unknown { if (Array.isArray(value)) return value.map(canonicalize); if (!isRecord(value)) return value; return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonicalize(value[key])])); }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
