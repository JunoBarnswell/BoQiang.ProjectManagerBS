import { getExpressionFunction, inferExpressionFunctionType } from './expressionFunctionCatalog';
import type { ExpressionGraph, ExpressionNode } from './expressionGraph';
import type { DesignerConversionStep, DesignerValueType } from './expressionTypes';

export type ExpressionPathSegment = 'root' | 'input' | 'when' | 'then' | 'otherwise' | 'properties' | 'items' | { args: number } | { property: string };
export type ExpressionPath = readonly ExpressionPathSegment[];

export function expressionNodeAt(graph: ExpressionGraph, path: ExpressionPath): ExpressionNode | null {
  if (path.length === 0 || path[0] !== 'root') return null;
  let current = graph.root;
  for (const segment of path.slice(1)) {
    if (!current) return null;
    if (segment === 'input' && 'input' in current) current = current.input;
    else if (segment === 'when' && current.kind === 'condition') current = current.when;
    else if (segment === 'then' && current.kind === 'condition') current = current.then;
    else if (segment === 'otherwise' && current.kind === 'condition') current = current.otherwise;
    else if (segment === 'items' && ('items' in current)) current = current.items[0] ?? null;
    else if (typeof segment === 'object' && 'args' in segment && ('args' in current)) current = current.args[segment.args] ?? null;
    else if (typeof segment === 'object' && 'property' in segment && current.kind === 'object') current = current.properties[segment.property] ?? null;
    else return null;
  }
  return current;
}

export function replaceExpressionNode(graph: ExpressionGraph, path: ExpressionPath, replacement: ExpressionNode | null): ExpressionGraph {
  if (path.length === 1 && path[0] === 'root') return { root: replacement };
  if (path.length < 2 || path[0] !== 'root' || !graph.root) return graph;
  return { root: replaceChild(graph.root, path.slice(1), replacement) };
}

export function deleteExpressionNode(graph: ExpressionGraph, path: ExpressionPath): ExpressionGraph {
  if (path.length === 1) return { root: null };
  const parentPath = path.slice(0, -1);
  const parent = expressionNodeAt(graph, parentPath);
  const segment = path[path.length - 1];
  if (parent && typeof segment === 'object' && 'args' in segment && 'args' in parent) return replaceExpressionNode(graph, parentPath, { ...parent, args: parent.args.filter((_, index) => index !== segment.args) } as ExpressionNode);
  if (parent && segment === 'input') return replaceExpressionNode(graph, parentPath, createExpressionNode('literal', parent.valueType));
  if (parent && parent.kind === 'condition' && (segment === 'when' || segment === 'then' || segment === 'otherwise')) return replaceExpressionNode(graph, parentPath, { ...parent, [segment]: createExpressionNode('literal', segment === 'when' ? 'boolean' : parent.valueType) } as ExpressionNode);
  return graph;
}

export function moveExpressionArgument(graph: ExpressionGraph, path: ExpressionPath, index: number, delta: -1 | 1): ExpressionGraph {
  const node = expressionNodeAt(graph, path);
  if (!node || !('args' in node)) return graph;
  const target = index + delta;
  if (index < 0 || target < 0 || index >= node.args.length || target >= node.args.length) return graph;
  const args = [...node.args];
  [args[index], args[target]] = [args[target], args[index]];
  return replaceExpressionNode(graph, path, { ...node, args } as ExpressionNode);
}

export function appendExpressionArgument(graph: ExpressionGraph, path: ExpressionPath): ExpressionGraph {
  const node = expressionNodeAt(graph, path);
  if (!node || !('args' in node)) return graph;
  const definition = node.kind === 'functionCall' ? getExpressionFunction(node.functionId) : undefined;
  if (definition && definition.maxArgs !== null && node.args.length >= definition.maxArgs) return graph;
  const declared = definition?.argumentTypes[Math.min(node.args.length, definition.argumentTypes.length - 1)] ?? (node.kind === 'logic' ? 'boolean' : node.valueType);
  const valueType: DesignerValueType = declared === 'sameAsFirst' || declared === 'any' ? node.args[0]?.valueType ?? 'string' : declared;
  return replaceExpressionNode(graph, path, { ...node, args: [...node.args, createExpressionNode('literal', valueType)] } as ExpressionNode);
}

export function createExpressionNode(kind: ExpressionNode['kind'], valueType: DesignerValueType): ExpressionNode {
  switch (kind) {
    case 'literal': return { kind, value: defaultValue(valueType), valueType };
    case 'resourceRef': return { kind, resourceId: '', valueType };
    case 'conversion': return { kind, input: createExpressionNode('literal', valueType), pipeline: [] as DesignerConversionStep[], valueType };
    case 'condition': return { kind, when: createExpressionNode('literal', 'boolean'), then: createExpressionNode('literal', valueType), otherwise: createExpressionNode('literal', valueType), valueType };
    case 'logic': return { kind, operator: 'and', args: [createExpressionNode('literal', 'boolean'), createExpressionNode('literal', 'boolean')], valueType: 'boolean' };
    case 'object': return { kind, properties: {}, valueType: valueType === 'object' ? 'object' : 'json' };
    case 'array': return { kind, items: [], valueType: 'array' };
    case 'template': return { kind, items: [], valueType: 'string' };
    case 'defaultValue': return { kind, input: createExpressionNode('literal', valueType), fallback: defaultValue(valueType), valueType };
    case 'functionCall': { const definition = getExpressionFunction('coalesce')!; const args = Array.from({ length: definition.minArgs }, () => createExpressionNode('literal', valueType)); return { kind, functionId: definition.name, args, valueType: inferExpressionFunctionType(definition, args, valueType) }; }
  }
}

function replaceChild(node: ExpressionNode, path: readonly ExpressionPathSegment[], replacement: ExpressionNode | null): ExpressionNode {
  if (path.length === 0) return replacement ?? createExpressionNode('literal', node.valueType);
  const [segment, ...rest] = path;
  if (segment === 'input' && 'input' in node) return { ...node, input: replaceChild(node.input, rest, replacement) } as ExpressionNode;
  if (node.kind === 'condition' && (segment === 'when' || segment === 'then' || segment === 'otherwise')) return { ...node, [segment]: replaceChild(node[segment], rest, replacement) } as ExpressionNode;
  if (segment === 'items' && 'items' in node && rest[0] && typeof rest[0] === 'object' && 'args' in rest[0]) return node;
  if (typeof segment === 'object' && 'args' in segment && 'args' in node) { const args = [...node.args]; if (rest.length === 0) args[segment.args] = replacement ?? createExpressionNode('literal', args[segment.args]?.valueType ?? node.valueType); else if (args[segment.args]) args[segment.args] = replaceChild(args[segment.args], rest, replacement); return { ...node, args } as ExpressionNode; }
  if (typeof segment === 'object' && 'property' in segment && node.kind === 'object') return { ...node, properties: { ...node.properties, [segment.property]: replacement ?? createExpressionNode('literal', 'string') } };
  return node;
}

function defaultValue(valueType: DesignerValueType): unknown { if (valueType === 'boolean') return false; if (valueType === 'number') return 0; if (valueType === 'array') return []; if (valueType === 'object' || valueType === 'json') return {}; return ''; }
