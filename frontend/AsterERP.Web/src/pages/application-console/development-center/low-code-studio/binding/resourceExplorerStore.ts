import { buildResourceOptions, resourceOptionToExpression } from '../expression/expressionCatalog';
import { normalizeResourceId } from '../expression/expressionTypes';
import type { BindingDocument } from '../expression/expressionTypes';

import { resourceIdFor, type StableResourceReference } from './bindingTypes';

const RECENT_KEY = 'astererp.low-code.binding.recent';
const FAVORITES_KEY = 'astererp.low-code.binding.favorites';

export function listStableResources(document: BindingDocument | null | undefined): StableResourceReference[] {
  return buildResourceOptions(document).map((option) => ({
    expression: resourceOptionToExpression(option),
    id: resourceIdFor(String(option.source), option.path, option.modelCode),
    label: option.label,
    path: option.path,
    resourceType: String(option.source),
    source: String(option.source),
    valueType: option.valueType,
    writable: option.writable
  }));
}

export function searchStableResources(resources: StableResourceReference[], query: string): StableResourceReference[] {
  const keyword = query.trim().toLowerCase();
  return keyword ? resources.filter((resource) => `${resource.label} ${resource.source} ${resource.path} ${resource.id}`.toLowerCase().includes(keyword)) : resources;
}

export interface StableResourceUsage {
  path: string;
  resourceId: string;
}

export function findResourceUsages(document: BindingDocument | null | undefined, resourceId: string): StableResourceUsage[] {
  const target = normalizeResourceId(resourceId);
  if (!target || !document) return [];

  const usages: StableResourceUsage[] = [];
  const visited = new WeakSet<object>();
  visit(document, '$', target, usages, visited);
  return usages;
}

export function findUnresolvedResourceUsages(document: BindingDocument | null | undefined): StableResourceUsage[] {
  if (!document) return [];
  const known = new Set(listStableResources(document).map((resource) => normalizeResourceId(resource.id, resource.resourceType)));
  const usages: StableResourceUsage[] = [];
  collectUsages(document, '$', usages, new WeakSet<object>());
  return usages.filter((usage) => !hasStableResource(known, usage.resourceId));
}

/**
 * Runtime-scoped roots expose dynamic properties that are only known after a
 * table row is selected. The catalog can therefore only register the root
 * (`currentRow:*`), while `currentRow:id` and other row fields remain valid.
 */
export function hasStableResource(known: ReadonlySet<string>, resourceId: string): boolean {
  if (known.has(resourceId)) return true;
  const source = resourceId.split(':', 1)[0]?.trim().toLowerCase();
  return source === 'currentrow'
    ? known.has('currentRow:*')
    : source === 'tablerow' && known.has('tableRow:*');
}

export function readResourceHistory(storage: Storage = window.localStorage): { recent: string[]; favorites: string[] } {
  return { recent: readIds(storage, RECENT_KEY), favorites: readIds(storage, FAVORITES_KEY) };
}

export function rememberResource(resourceId: string, storage: Storage = window.localStorage): void {
  storage.setItem(RECENT_KEY, JSON.stringify([resourceId, ...readIds(storage, RECENT_KEY).filter((id) => id !== resourceId)].slice(0, 12)));
}

export function toggleFavoriteResource(resourceId: string, storage: Storage = window.localStorage): boolean {
  const current = readIds(storage, FAVORITES_KEY);
  const next = current.includes(resourceId) ? current.filter((id) => id !== resourceId) : [resourceId, ...current];
  storage.setItem(FAVORITES_KEY, JSON.stringify(next));
  return next.includes(resourceId);
}

function readIds(storage: Storage, key: string): string[] {
  try {
    const value: unknown = JSON.parse(storage.getItem(key) ?? '[]');
    return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];
  } catch {
    return [];
  }
}

function visit(value: unknown, path: string, target: string, usages: StableResourceUsage[], visited: WeakSet<object>): void {
  if (!value || typeof value !== 'object') return;
  if (visited.has(value)) return;
  visited.add(value);

  if (Array.isArray(value)) {
    value.forEach((item, index) => visit(item, `${path}[${index}]`, target, usages, visited));
    return;
  }

  const record = value as Record<string, unknown>;
  const rawResourceId = typeof record.resourceId === 'string' ? record.resourceId : undefined;
  const resourceType = typeof record.resourceType === 'string' ? record.resourceType : undefined;
  const canonicalId = rawResourceId ? normalizeResourceId(rawResourceId, resourceType) : '';
  if (canonicalId === target) usages.push({ path, resourceId: canonicalId });

  Object.entries(record).forEach(([key, child]) => visit(child, `${path}.${key}`, target, usages, visited));
}

function collectUsages(value: unknown, path: string, usages: StableResourceUsage[], visited: WeakSet<object>): void {
  if (!value || typeof value !== 'object' || visited.has(value)) return;
  visited.add(value);
  if (Array.isArray(value)) {
    value.forEach((item, index) => collectUsages(item, `${path}[${index}]`, usages, visited));
    return;
  }
  const record = value as Record<string, unknown>;
  const resourceType = typeof record.resourceType === 'string' ? record.resourceType : undefined;
  const rawResourceId = typeof record.resourceId === 'string' ? record.resourceId : undefined;
  const resourceId = rawResourceId ? normalizeResourceId(rawResourceId, resourceType) : '';
  if (resourceId && (typeof record.valueType === 'string' || typeof record.expectedType === 'string' || typeof record.displayName === 'string')) {
    usages.push({ path, resourceId });
  }
  Object.entries(record).forEach(([key, child]) => collectUsages(child, `${path}.${key}`, usages, visited));
}
