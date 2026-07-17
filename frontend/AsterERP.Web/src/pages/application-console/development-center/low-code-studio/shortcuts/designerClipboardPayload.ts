import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import type { ClipboardTree } from './clipboardTree';

export interface DesignerClipboardPayload {
  trees: ClipboardTree<DesignerDocumentNode>[];
  actions: Record<string, unknown>[];
  apiBindings: Record<string, unknown>[];
  dataSources: Record<string, unknown>[];
  pageParameters: Record<string, unknown>[];
  variables: Record<string, unknown>[];
  workflowBindings: Record<string, unknown>[];
  missingResources: string[];
  manifestTypes: string[];
  manifestVersions: Record<string, string>;
}

type ResourceCollection = keyof Pick<DesignerClipboardPayload, 'apiBindings' | 'dataSources' | 'pageParameters' | 'variables' | 'workflowBindings'>;

export function createDesignerClipboardPayload(document: DesignerDocument, nodeIds: readonly string[]): DesignerClipboardPayload {
  const selected = new Set(nodeIds);
  const roots = nodeIds.filter((id) => {
    const node = document.elements[id];
    return Boolean(node) && (!node.parentId || !selected.has(node.parentId));
  });
  const trees = roots.map((id) => buildTree(document.elements, id));
  const references = collectReferences(trees);
  const collections = {
    apiBindings: collectRecords(document.apiBindings, references.resources, 'api'),
    dataSources: collectRecords(document.dataSources, references.resources, 'dataSource'),
    pageParameters: collectRecords(document.pageParameters, references.resources, 'page'),
    variables: collectRecords(document.variables, references.resources, 'variables'),
    workflowBindings: collectRecords(document.workflowBindings, references.resources, 'workflow')
  } satisfies Record<ResourceCollection, Record<string, unknown>[]>;
  const actions = document.actions.filter((item) => references.actions.has(readId(item))).map(cloneValue);
  const knownResources = new Set([
    ...collections.apiBindings.map((item) => `api:${readId(item)}`),
    ...collections.dataSources.map((item) => `dataSource:${readId(item)}`),
    ...collections.pageParameters.map((item) => `page:${readId(item)}`),
    ...collections.variables.map((item) => `variables:${readId(item)}`),
    ...collections.workflowBindings.map((item) => `workflow:${readId(item)}`)
  ]);
  const manifestTypes = [...new Set(trees.flatMap((tree) => flatten(tree).map((node) => node.type)))].sort();
  const manifestVersionMap = isRecord(document.metadata.manifestVersions) ? document.metadata.manifestVersions : {};
  const manifestVersions = Object.fromEntries(manifestTypes.flatMap((type) => typeof manifestVersionMap[type] === 'string' ? [[type, manifestVersionMap[type] as string]] : []));

  return {
    trees,
    ...collections,
    actions,
    missingResources: [...references.resources].filter((resourceId) => !knownResources.has(resourceId)).sort(),
    manifestTypes,
    manifestVersions
  };
}

export function cloneDesignerClipboardPayload(payload: DesignerClipboardPayload): DesignerClipboardPayload {
  return cloneValue(payload);
}

export function rewriteClipboardValue(value: unknown, resourceRemap: ReadonlyMap<string, string>, actionRemap: ReadonlyMap<string, string>): unknown {
  if (Array.isArray(value)) return value.map((item) => rewriteClipboardValue(item, resourceRemap, actionRemap));
  if (!isRecord(value)) return value;
  const result: Record<string, unknown> = {};
  for (const [key, child] of Object.entries(value)) {
    if (key === 'resourceId' && typeof child === 'string') {
      result[key] = resourceRemap.get(child) ?? child;
    } else if ((key === 'actionId' || key === 'action') && typeof child === 'string') {
      result[key] = actionRemap.get(child) ?? child;
    } else if (key === 'actionIds' && Array.isArray(child)) {
      result[key] = child.map((item) => typeof item === 'string' ? actionRemap.get(item) ?? item : rewriteClipboardValue(item, resourceRemap, actionRemap));
    } else {
      result[key] = rewriteClipboardValue(child, resourceRemap, actionRemap);
    }
  }
  return result;
}

export function resourceKeyFor(collection: string, id: string): string {
  return `${collection === 'apiBindings' ? 'api' : collection === 'dataSources' ? 'dataSource' : collection === 'pageParameters' ? 'page' : collection}:${id}`;
}

export function readId(value: Record<string, unknown>): string {
  return typeof value.id === 'string' ? value.id : '';
}

function buildTree(elements: Record<string, DesignerDocumentNode>, id: string): ClipboardTree<DesignerDocumentNode> {
  const source = elements[id];
  if (!source) throw new Error(`Cannot copy missing node ${id}`);
  const children = source.children.filter((childId) => Boolean(elements[childId])).map((childId) => buildTree(elements, childId));
  return { root: cloneValue({ ...source, children: [...source.children] }), children };
}

function collectReferences(trees: readonly ClipboardTree<DesignerDocumentNode>[]): { resources: Set<string>; actions: Set<string> } {
  const resources = new Set<string>();
  const actions = new Set<string>();
  for (const tree of trees) collectValueReferences(tree.root, resources, actions);
  return { resources, actions };
}

function collectValueReferences(value: unknown, resources: Set<string>, actions: Set<string>, key = ''): void {
  if (Array.isArray(value)) {
    value.forEach((item) => collectValueReferences(item, resources, actions, key));
    return;
  }
  if (!isRecord(value)) return;
  for (const [childKey, child] of Object.entries(value)) {
    if (childKey === 'resourceId' && typeof child === 'string' && child.trim()) resources.add(normalizeResourceKey(child));
    if ((childKey === 'actionId' || childKey === 'action') && typeof child === 'string' && child.trim()) actions.add(child);
    if (childKey === 'actionIds' && Array.isArray(child)) child.filter((item): item is string => typeof item === 'string').forEach((item) => actions.add(item));
    collectValueReferences(child, resources, actions, childKey);
  }
}

function collectRecords(records: readonly Record<string, unknown>[], resources: ReadonlySet<string>, prefix: string): Record<string, unknown>[] {
  return records.filter((record) => {
    const id = readId(record);
    return id && resources.has(`${prefix}:${id}`);
  }).map(cloneValue);
}

function normalizeResourceKey(resourceId: string): string {
  const [prefix, ...rest] = resourceId.split(':');
  const normalizedPrefix = prefix === 'apiBindings' ? 'api' : prefix === 'datasource' || prefix === 'dataSources' ? 'dataSource' : prefix;
  return `${normalizedPrefix}:${rest.join(':')}`;
}

function flatten(tree: ClipboardTree<DesignerDocumentNode>): DesignerDocumentNode[] {
  return [tree.root, ...tree.children.flatMap(flatten)];
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function cloneValue<T>(value: T): T {
  return structuredClone(value);
}
