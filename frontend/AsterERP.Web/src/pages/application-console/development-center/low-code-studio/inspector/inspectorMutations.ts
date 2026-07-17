import { expressionValueToGraph } from '../../../../../api/runtime/expressionValue';
import { validateBindingExpression } from '../binding/bindingTypes';
import { createBatchPatchCommand, createPatchResponsiveOverrideCommand } from '../commands/createDesignerCommands';
import type { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';
import { isExpressionValue, isResourceRef, toExpressionValue } from '../document/PropertyValue';
import { resolveResponsiveInheritedSections } from '../responsive/responsiveModel';
import type { ResponsiveBreakpoint } from '../responsive/responsiveModel';

import { isAcceptedInspectorSource, matchesInspectorCondition, validateInspectorValue, valuesEqual } from './inspectorSemantics';
import type { InspectorPropertyDefinition } from './inspectorTypes';

export type InspectorMutationProperty = Pick<InspectorPropertyDefinition, 'path' | 'valueType'> & Partial<Pick<InspectorPropertyDefinition, 'bindable' | 'acceptedSources' | 'bindingPolicy' | 'batchPolicy' | 'defaultValue' | 'enabledWhen' | 'resetPolicy' | 'responsive' | 'validation' | 'options'>>;

export function commitInspectorValue(
  document: DesignerDocument,
  nodeIds: readonly string[],
  field: InspectorMutationProperty,
  value: unknown,
  commandBus?: DesignerCommandBus,
  onDocumentChange?: (document: DesignerDocument) => void,
  selectedBreakpoint?: ResponsiveBreakpoint | null,
  breakpoints?: readonly ResponsiveBreakpoint[]
): DesignerDocument {
  if (!canMutateInspectorProperty(document, nodeIds, field) || !validateInspectorValue(value, { valueType: field.valueType, validation: field.validation ?? { valueType: field.valueType }, options: field.options }).valid) return document;
  if (selectedBreakpoint && field.responsive?.enabled && breakpoints) {
    return commitResponsiveValue(document, nodeIds, field, value, selectedBreakpoint, breakpoints, commandBus, onDocumentChange);
  }
  const patches = nodeIds.reduce<Record<string, Partial<DesignerDocumentNode>>>((result, nodeId) => {
    const node = document.elements[nodeId];
    if (node) result[nodeId] = { [scopeOf(field.path)]: set(readScope(node, scopeOf(field.path)), pathOf(field.path), value) };
    return result;
  }, {});
  return commitPatches(document, nodeIds, patches, commandBus, onDocumentChange, createInspectorMergeKey('value', nodeIds, field.path));
}

export function commitInspectorBinding(
  document: DesignerDocument,
  nodeIds: readonly string[],
  field: InspectorMutationProperty,
  expression: unknown,
  commandBus?: DesignerCommandBus,
  onDocumentChange?: (document: DesignerDocument) => void,
  selectedBreakpoint?: ResponsiveBreakpoint | null,
  breakpoints?: readonly ResponsiveBreakpoint[]
): DesignerDocument {
  if (!canMutateInspectorProperty(document, nodeIds, field)) return document;
  if (expression !== null && !isAcceptedInspectorBinding(expression, field)) return document;
  if (isBindingExpression(expression) && !isResourceRef(expression)) {
    const validation = validateBindingExpression(toTransientBindingExpression(expression), field.valueType);
    if (!validation.valid) return document;
  }
  if (selectedBreakpoint && field.responsive?.enabled && breakpoints && (field.path.startsWith('layout.') || field.path.startsWith('style.'))) {
    const nextValue = expression === null ? bindingFallback(readPath(readScope(document.elements[nodeIds[0]] ?? {} as DesignerDocumentNode, scopeOf(field.path)), pathOf(field.path))) : expression;
    return commitResponsiveValue(document, nodeIds, field, nextValue, selectedBreakpoint, breakpoints, commandBus, onDocumentChange);
  }
  const patches = nodeIds.reduce<Record<string, Partial<DesignerDocumentNode>>>((result, nodeId) => {
    const node = document.elements[nodeId];
    if (!node) return result;
    if (field.path.startsWith('bindings.')) {
      if (expression === null) {
        result[nodeId] = { bindings: deletePath(node.bindings ?? {}, field.path.replace(/^bindings\./, '')) };
      } else {
        result[nodeId] = { bindings: setPath(node.bindings ?? {}, field.path.replace(/^bindings\./, ''), expression) };
      }
      return result;
    }
    const nextValue = expression === null
      ? bindingFallback(readPath(readScope(node, scopeOf(field.path)), pathOf(field.path)))
      : isResourceRef(expression)
        ? expression
        : isExpressionValue(expression)
        ? expression
        : toExpressionValue(expression as Parameters<typeof toExpressionValue>[0], field.valueType);
    if (expression !== null && !nextValue) return result;
    result[nodeId] = { [scopeOf(field.path)]: set(readScope(node, scopeOf(field.path)), pathOf(field.path), nextValue) };
    return result;
  }, {});
  return commitPatches(document, nodeIds, patches, commandBus, onDocumentChange, createInspectorMergeKey('binding', nodeIds, field.path));
}

function setPath(source: Record<string, unknown>, path: string, value: unknown): Record<string, unknown> {
  const parts = path.split('.').filter(Boolean);
  const result = structuredClone(source);
  let cursor = result;
  parts.forEach((part, index) => {
    if (index === parts.length - 1) cursor[part] = value;
    else {
      const next = cursor[part];
      cursor[part] = next && typeof next === 'object' && !Array.isArray(next) ? next : {};
      cursor = cursor[part] as Record<string, unknown>;
    }
  });
  return result;
}

function deletePath(source: Record<string, unknown>, path: string): Record<string, unknown> {
  const parts = path.split('.').filter(Boolean);
  if (parts.length === 0) return structuredClone(source);
  const result = structuredClone(source);
  let cursor = result;
  parts.slice(0, -1).forEach((part) => {
    const next = cursor[part];
    if (!next || typeof next !== 'object' || Array.isArray(next)) return;
    cursor = next as Record<string, unknown>;
  });
  delete cursor[parts[parts.length - 1]];
  return result;
}

function commitPatches(
  document: DesignerDocument,
  nodeIds: readonly string[],
  patches: Record<string, Partial<DesignerDocumentNode>>,
  commandBus?: DesignerCommandBus,
  onDocumentChange?: (document: DesignerDocument) => void,
  commandMergeKey?: string
): DesignerDocument {
  if (nodeIds.length === 0) return document;
  if (!commandBus) return document;
  const result = commandBus.execute(createBatchPatchCommand(patches, commandMergeKey));
  if (result.changed) {
    onDocumentChange?.(result.document);
    return result.document;
  }
  return document;
}

function scopeOf(key: string): string { return key.split('.')[0] ?? key; }
function pathOf(key: string): string[] { return key.split('.').slice(1); }
function readScope(node: DesignerDocumentNode, scope: string): Record<string, unknown> {
  const value = node[scope as keyof DesignerDocumentNode];
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};
}
function readPath(source: Record<string, unknown>, path: string[]): unknown {
  return path.reduce<unknown>((current, part) => current && typeof current === 'object' && !Array.isArray(current)
    ? (current as Record<string, unknown>)[part]
    : undefined, source);
}
function set(source: Record<string, unknown>, path: string[], value: unknown): Record<string, unknown> {
  const [head, ...tail] = path;
  if (!head) return source;
  return { ...source, [head]: tail.length ? set(asRecord(source[head]), tail, value) : value };
}
function asRecord(value: unknown): Record<string, unknown> { return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {}; }

function isBindingExpression(value: unknown): boolean {
  if (isExpressionValue(value)) return true;
  if (!value || typeof value !== 'object') return false;
  const expression = value as { graph?: unknown; resourceId?: unknown; resourceType?: unknown; source?: unknown; conversionPipeline?: unknown };
  return expression.graph !== undefined || typeof expression.resourceId === 'string' || typeof expression.resourceType === 'string' || typeof expression.source === 'string' || expression.conversionPipeline !== undefined;
}

export function commitInspectorReset(
  document: DesignerDocument,
  nodeIds: readonly string[],
  field: InspectorMutationProperty,
  commandBus?: DesignerCommandBus,
  onDocumentChange?: (document: DesignerDocument) => void,
  selectedBreakpoint?: ResponsiveBreakpoint | null,
  breakpoints?: readonly ResponsiveBreakpoint[]
): DesignerDocument {
  if (field.resetPolicy === 'none' || !canMutateInspectorProperty(document, nodeIds, field)) return document;
  if (selectedBreakpoint && field.responsive?.enabled && breakpoints) {
    const commands = nodeIds.filter((nodeId) => hasDirectResponsiveOverride(document.elements[nodeId], field.path, selectedBreakpoint.id)).map((nodeId) => createPatchResponsiveOverrideCommand(nodeId, selectedBreakpoint.id, createResetPatch(field.path)));
    if (commands.length === 0 || !commandBus) return document;
    const result = commandBus.executeTransaction(commands);
    if (result.changed) onDocumentChange?.(result.document);
    return result.changed ? result.document : document;
  }
  if (field.resetPolicy === 'default') return commitInspectorValue(document, nodeIds, field, field.defaultValue, commandBus, onDocumentChange);
  const patches = nodeIds.reduce<Record<string, Partial<DesignerDocumentNode>>>((result, nodeId) => {
    const node = document.elements[nodeId];
    if (node) result[nodeId] = { [scopeOf(field.path)]: deletePath(readScope(node, scopeOf(field.path)), pathOf(field.path).join('.')) };
    return result;
  }, {});
  return commitPatches(document, nodeIds, patches, commandBus, onDocumentChange, createInspectorMergeKey('reset', nodeIds, field.path));
}

function commitResponsiveValue(document: DesignerDocument, nodeIds: readonly string[], field: InspectorMutationProperty, value: unknown, selectedBreakpoint: ResponsiveBreakpoint, breakpoints: readonly ResponsiveBreakpoint[], commandBus?: DesignerCommandBus, onDocumentChange?: (document: DesignerDocument) => void): DesignerDocument {
  if (!commandBus) return document;
  const commands = nodeIds.flatMap((nodeId) => {
    const node = document.elements[nodeId];
    if (!node) return [];
    const base = { layout: node.layout, props: node.props, style: node.style ?? {} };
    const inherited = resolveResponsiveInheritedSections({ base, responsiveOverrides: node.responsiveOverrides ?? {} }, selectedBreakpoint, breakpoints);
    const inheritedValue = readPath(inherited[field.path.split('.')[0] as 'layout' | 'props' | 'style'] ?? {}, pathOf(field.path));
    const directValue = readPath(node.responsiveOverrides?.[selectedBreakpoint.id]?.[field.path.split('.')[0] as 'layout' | 'props' | 'style'] ?? {}, pathOf(field.path));
    if (valuesEqual(value, inheritedValue)) return directValue === undefined ? [] : [createPatchResponsiveOverrideCommand(nodeId, selectedBreakpoint.id, createResetPatch(field.path))];
    return [createPatchResponsiveOverrideCommand(nodeId, selectedBreakpoint.id, { [scopeOf(field.path)]: set({}, pathOf(field.path), value) })];
  });
  if (commands.length === 0) return document;
  const result = commandBus.executeTransaction(commands);
  if (result.changed) onDocumentChange?.(result.document);
  return result.changed ? result.document : document;
}

function canMutateInspectorProperty(document: DesignerDocument, nodeIds: readonly string[], field: InspectorMutationProperty): boolean {
  if (nodeIds.length > 1 && field.batchPolicy && field.batchPolicy !== 'editable') return false;
  return nodeIds.every((nodeId) => {
    const node = document.elements[nodeId];
    return Boolean(node) && matchesInspectorCondition(node!, field.enabledWhen);
  });
}

function hasDirectResponsiveOverride(node: DesignerDocumentNode | undefined, path: string, breakpointId: string): boolean {
  if (!node) return false;
  const scope = path.split('.')[0] as 'layout' | 'props' | 'style';
  return readPath(node.responsiveOverrides?.[breakpointId]?.[scope] ?? {}, pathOf(path)) !== undefined;
}

function isAcceptedInspectorBinding(expression: unknown, field: InspectorMutationProperty): boolean {
  if (expression === null) return true;
  if (field.acceptedSources === undefined && field.bindingPolicy === undefined) return field.bindable !== false;
  const record = expression && typeof expression === 'object' ? expression as Record<string, unknown> : {};
  const resourceId = typeof record.resourceId === 'string' ? record.resourceId : undefined;
  const source = typeof record.resourceType === 'string' ? record.resourceType : typeof record.source === 'string' ? record.source : resourceId?.split(':')[0];
  return isAcceptedInspectorSource(source, { bindable: field.bindable ?? false, acceptedSources: field.acceptedSources ?? [], bindingPolicy: field.bindingPolicy ?? { enabled: field.bindable ?? false, acceptedSources: field.acceptedSources ?? [] } });
}

function createResetPatch(path: string): Record<string, Record<string, unknown>> {
  const [scope, ...parts] = path.split('.');
  return scope && parts.length > 0 ? { [scope]: set({}, parts, undefined) } : {};
}

/**
 * The inspector edits a transient graph, but callers that already hold the
 * persisted latest AST must not be forced through an intermediate wrapper first.
 */
function toTransientBindingExpression(value: unknown): Parameters<typeof validateBindingExpression>[0] {
  if (isExpressionValue(value)) {
    return {
      expectedType: value.dataType,
      fallback: value.fallback,
      graph: expressionValueToGraph(value),
      helpers: []
    };
  }
  return value as Parameters<typeof validateBindingExpression>[0];
}

function bindingFallback(value: unknown): unknown {
  if (isResourceRef(value)) return value.fallback?.value ?? null;
  if (isExpressionValue(value)) return value.fallback ?? null;
  return value;
}

function createInspectorMergeKey(kind: 'binding' | 'value' | 'reset', nodeIds: readonly string[], fieldKey: string): string {
  return `inspector:${kind}:${fieldKey}:${[...nodeIds].sort().join(',')}`;
}
