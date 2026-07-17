import type { DesignerDocument } from '../document/DesignerDocument';

export interface PropertyElement {
  id: string;
  [key: string]: unknown;
}

export interface PropertyTransaction {
  id: string;
  label: string;
  patches: Array<{ elementId: string; key: string; value: unknown }>;
}

export interface PropertyFieldDescriptor {
  key: string;
  label: string;
}

export function createPropertyTransaction(label: string, patches: PropertyTransaction['patches']): PropertyTransaction {
  return { id: `property:${Date.now().toString(36)}:${Math.random().toString(36).slice(2, 8)}`, label, patches };
}

export type LatestDesignerDocument = DesignerDocument;

export function applyPropertyTransaction<TDocument extends DesignerDocument | { elements: Record<string, { id: string }> }>(document: TDocument, transaction: PropertyTransaction): TDocument {
  if (transaction.patches.length === 0) return document;

  const elements = { ...document.elements };
  for (const patch of transaction.patches) {
    const element = elements[patch.elementId];
    if (!element) continue;
    const [scope, ...path] = patch.key.split('.');
    if (!scope || path.length === 0) continue;
    const currentScope = readElementScope(element, scope);
    elements[patch.elementId] = { ...element, [scope]: setNestedValue(currentScope, path, patch.value) };
  }

  return { ...document, elements };
}

export function readPropertyValue(element: { id: string }, key: string): unknown {
  return key.split('.').reduce<unknown>((current, part) => current && typeof current === 'object' ? (current as Record<string, unknown>)[part] : undefined, element);
}

export function createBatchPropertyTransaction(elements: ReadonlyArray<{ id: string }>, field: PropertyFieldDescriptor, value: unknown): PropertyTransaction {
  return createPropertyTransaction(`批量修改 ${field.label}`, elements.map((element) => ({ elementId: element.id, key: field.key, value })));
}

export function isMixedPropertyValue(elements: ReadonlyArray<{ id: string }>, key: string): boolean {
  if (elements.length < 2) return false;
  const values = elements.map((element) => JSON.stringify(readPropertyValue(element, key)));
  return values.some((value) => value !== values[0]);
}

function readElementScope(element: { id: string }, scope: string): Record<string, unknown> {
  const value = (element as unknown as Record<string, unknown>)[scope];
  return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : {};
}

function setNestedValue(source: Record<string, unknown>, path: string[], value: unknown): Record<string, unknown> {
  const [head, ...tail] = path;
  if (!head) return source;
  if (tail.length === 0) return { ...source, [head]: value };
  const current = source[head];
  const currentRecord = current && typeof current === 'object' && !Array.isArray(current) ? current as Record<string, unknown> : {};
  return { ...source, [head]: setNestedValue(currentRecord, tail, value) };
}
