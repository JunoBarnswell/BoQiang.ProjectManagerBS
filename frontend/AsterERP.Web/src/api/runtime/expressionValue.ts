import type { ExpressionNode, ExpressionGraph } from '../../pages/application-console/development-center/low-code-studio/expression/expressionGraph';
import { evaluateExpressionNode } from '../../pages/application-console/development-center/low-code-studio/expression/expressionGraphEvaluator';

import type { ExpressionDataType, ExpressionValue } from './expression-value.latest';

export function expressionValueFromGraph(graph: ExpressionGraph, expectedType: ExpressionDataType, fallback?: unknown): ExpressionValue {
  if (!graph.root) throw new Error('Expression root is required.');
  const expression = expressionNodeToValue(graph.root);
  if (expression.dataType !== expectedType) {
    throw new Error(`Expression result type ${expression.dataType} does not match ${expectedType}.`);
  }
  if (fallback !== undefined) expression.fallback = fallback;
  expression.dependencies = [...new Set(collectResourceIds(graph.root))].sort();
  return expression;
}

export function expressionValueCanonicalJson(expression: ExpressionValue): string {
  return JSON.stringify(sortObject(expression));
}

/**
 * A transient editor projection.  Documents and runtime payloads always keep
 * the canonical ExpressionValue; the graph is reconstructed only while an
 * author is editing that value.
 */
export function expressionValueToGraph(expression: ExpressionValue): ExpressionGraph {
  return { root: expressionValueToNode(expression) };
}

export function isCanonicalExpressionValue(value: unknown): value is ExpressionValue {
  return Boolean(value)
    && typeof value === 'object'
    && !Array.isArray(value)
    && (value as { version?: unknown }).version === 'latest'
    && typeof (value as { kind?: unknown }).kind === 'string'
    && typeof (value as { dataType?: unknown }).dataType === 'string';
}

/** Executes the canonical AST without accepting source/path/helper/template legacy forms. */
export function evaluateExpressionValue(expression: ExpressionValue, resolveResource: (resourceId: string) => unknown): unknown {
  return evaluateExpressionNode(expressionValueToGraph(expression).root, { resolveResource });
}

function expressionNodeToValue(node: ExpressionNode): ExpressionValue {
  switch (node.kind) {
    case 'literal': return { version: 'latest', kind: node.kind, dataType: node.valueType, value: node.value };
    case 'resourceRef': return { version: 'latest', kind: node.kind, dataType: node.valueType, resourceId: node.resourceId };
    case 'functionCall': return { version: 'latest', kind: node.kind, dataType: node.valueType, functionId: node.functionId, args: node.args.map(expressionNodeToValue) };
    case 'conversion': return { version: 'latest', kind: node.kind, dataType: node.valueType, input: expressionNodeToValue(node.input), pipeline: node.pipeline };
    case 'condition': return { version: 'latest', kind: node.kind, dataType: node.valueType, when: expressionNodeToValue(node.when), then: expressionNodeToValue(node.then), otherwise: expressionNodeToValue(node.otherwise) };
    case 'logic': return { version: 'latest', kind: node.kind, dataType: node.valueType, operator: node.operator, args: node.args.map(expressionNodeToValue) };
    case 'object': return { version: 'latest', kind: node.kind, dataType: node.valueType, properties: Object.fromEntries(Object.entries(node.properties).sort(([left], [right]) => left.localeCompare(right)).map(([key, value]) => [key, expressionNodeToValue(value)])) };
    case 'array':
    case 'template': return { version: 'latest', kind: node.kind, dataType: node.valueType, items: node.items.map(expressionNodeToValue) };
    case 'defaultValue': return { version: 'latest', kind: node.kind, dataType: node.valueType, input: expressionNodeToValue(node.input), fallback: node.fallback };
  }
}

function expressionValueToNode(expression: ExpressionValue): ExpressionNode {
  const valueType = expression.dataType;
  switch (expression.kind) {
    case 'literal': return { kind: 'literal', value: expression.value, valueType };
    case 'resourceRef': return { kind: 'resourceRef', resourceId: requireString(expression.resourceId, 'resourceId'), valueType };
    case 'functionCall': return { kind: 'functionCall', functionId: requireString(expression.functionId, 'functionId'), args: (expression.args ?? []).map(expressionValueToNode), valueType };
    case 'conversion': return { kind: 'conversion', input: expressionValueToNode(requireValue(expression.input, 'input')), pipeline: expression.pipeline ?? [], valueType };
    case 'condition': return {
      kind: 'condition',
      when: expressionValueToNode(requireValue(expression.when, 'when')),
      then: expressionValueToNode(requireValue(expression.then, 'then')),
      otherwise: expressionValueToNode(requireValue(expression.otherwise, 'otherwise')),
      valueType
    };
    case 'logic': return { kind: 'logic', operator: expression.operator ?? 'and', args: (expression.args ?? []).map(expressionValueToNode), valueType: 'boolean' };
    case 'object': return { kind: 'object', properties: Object.fromEntries(Object.entries(expression.properties ?? {}).map(([key, value]) => [key, expressionValueToNode(value)])), valueType: valueType === 'json' ? 'json' : 'object' };
    case 'array': return { kind: 'array', items: (expression.items ?? []).map(expressionValueToNode), valueType: 'array' };
    case 'template': return { kind: 'template', items: (expression.items ?? []).map(expressionValueToNode), valueType: 'string' };
    case 'defaultValue': return { kind: 'defaultValue', input: expressionValueToNode(requireValue(expression.input, 'input')), fallback: expression.fallback, valueType };
  }
}

function requireString(value: string | undefined, name: string): string {
  if (!value?.trim()) throw new Error(`Expression ${name} is required.`);
  return value;
}

function requireValue(value: ExpressionValue | undefined, name: string): ExpressionValue {
  if (!value) throw new Error(`Expression ${name} is required.`);
  return value;
}

function collectResourceIds(node: ExpressionNode): string[] {
  if (node.kind === 'resourceRef') return [node.resourceId.trim()];
  if (node.kind === 'functionCall' || node.kind === 'logic') return node.args.flatMap(collectResourceIds);
  if (node.kind === 'conversion' || node.kind === 'defaultValue') return collectResourceIds(node.input);
  if (node.kind === 'condition') return [node.when, node.then, node.otherwise].flatMap(collectResourceIds);
  if (node.kind === 'object') return Object.values(node.properties).flatMap(collectResourceIds);
  if (node.kind === 'array' || node.kind === 'template') return node.items.flatMap(collectResourceIds);
  return [];
}

function sortObject(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(sortObject);
  if (!value || typeof value !== 'object') return value;
  return Object.fromEntries(Object.entries(value as Record<string, unknown>).sort(([left], [right]) => left.localeCompare(right)).map(([key, item]) => [key, sortObject(item)]));
}
