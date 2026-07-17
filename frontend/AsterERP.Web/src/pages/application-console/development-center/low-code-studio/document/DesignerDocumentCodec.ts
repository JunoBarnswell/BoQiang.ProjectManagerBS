import { normalizeResourceId, resourceTypeForId } from '../expression/expressionTypes';
import { defaultContainerLayout, defaultPlacement, normalizeLayoutProtocol, type Dimension, type LayoutMode, type LayoutProtocol } from '../layout/LayoutProtocol';
import { validateLayoutProtocol } from '../layout/LayoutProtocolValidator';
import { normalizeResponsiveOverrideMap, validateResponsiveOverrideMap } from '../responsive/responsiveModel';

import type { DesignerDocument, DesignerDocumentNode, DesignerNodeLayout } from './DesignerDocument';
import { canonicalizeDesignerDocument as canonicalizeHashDocument, computeDesignerDocumentHash, serializeDesignerDocument as serializeCanonicalDesignerDocument } from './DesignerDocumentHash';
import { DesignerDocumentParseError } from './DesignerDocumentParseError';
import { findDuplicateJsonKeys } from './duplicateJsonKeys';

export interface DesignerDocumentSeed {
  pageCode: string;
  pageName: string;
  pageType?: string;
}

export const DESIGNER_DOCUMENT_LIMITS = {
  actions: 200,
  children: 1000,
  depth: 64,
  draftBytes: 2 * 1024 * 1024,
  elements: 2000,
  valuePathSegments: 32
} as const;

const FORBIDDEN_KEYS = new Set(['__proto__', 'constructor', 'prototype']);
const SESSION_KEYS = ['tree', 'selectedElementId', 'selectedNodeIds', 'primaryNodeId', 'anchorNodeId', 'viewport', 'history', 'historyIndex', 'editorState', 'editorSession'];

export function parseDesignerDocument(source: string | null | undefined, seed: DesignerDocumentSeed): DesignerDocument {
  if (!source?.trim()) return createDefaultDesignerDocument(seed);

  let parsed: unknown;
  try {
    parsed = JSON.parse(source);
  } catch (error) {
    throw new DesignerDocumentParseError(source, [error instanceof Error ? error.message : 'Invalid JSON']);
  }

  if (!isRecord(parsed)) throw new DesignerDocumentParseError(source, ['document must be an object']);
  const diagnostics: string[] = [];
  diagnostics.push(...findDuplicateJsonKeys(source).map((path) => `duplicate JSON key at ${path}`));
  inspectForbiddenKeys(parsed, '$', diagnostics, new WeakSet<object>());
  if ('schemaVersion' in parsed) diagnostics.push('numeric schemaVersion is not accepted');
  for (const key of SESSION_KEYS) if (key in parsed) diagnostics.push(`${key} belongs to DesignerEditorSession, not DesignerDocument`);
  inspectLegacyNodeKeys(parsed, diagnostics);
  const document = normalizeDesignerDocument(parsed, seed, diagnostics);
  diagnostics.push(...validateDesignerDocument(document));
  validateSize(document, DESIGNER_DOCUMENT_LIMITS.draftBytes, 'draft', diagnostics);
  const errors = unique(diagnostics);
  if (errors.length > 0) throw new DesignerDocumentParseError(source, errors);
  return withHash(document);
}

export function serializeDesignerDocument(document: DesignerDocument): string {
  const errors: string[] = [];
  const normalizedDocument = normalizeDocumentLayoutProtocols(document, errors);
  const canonicalDocument = {
    ...normalizedDocument,
    elements: Object.fromEntries(Object.entries(normalizedDocument.elements).map(([id, node]) => [id, {
      ...node,
      layout: toPersistedLayoutProtocol(node.layout)
    }]))
  };
  errors.push(...validateDesignerDocument(canonicalDocument));
  validateSize(canonicalDocument, DESIGNER_DOCUMENT_LIMITS.draftBytes, 'draft', errors);
  if (errors.length > 0) throw new DesignerDocumentParseError(null, unique(errors));
  return serializeCanonicalDesignerDocument(canonicalDocument);
}

function toPersistedLayoutProtocol(layout: DesignerNodeLayout): DesignerNodeLayout {
  const source = resolveLayoutProtocolSource(layout);
  if (!source?.value) return layout;
  return {
    protocol: {
      container: source.value.container,
      placement: source.value.placement,
      size: source.value.size,
      ...(isRecord(source.value.anchor) ? { anchor: source.value.anchor } : {}),
      ...(isRecord(source.value.migration) ? { migration: source.value.migration } : {})
    } as unknown as LayoutProtocol
  };
}

export function validateDesignerDocumentBoundary(document: DesignerDocument): string[] {
  return SESSION_KEYS
    .filter((key) => Object.prototype.hasOwnProperty.call(document, key))
    .map((key) => `${key} belongs to DesignerEditorSession, not DesignerDocument`);
}

export function canonicalizeDesignerDocument(document: DesignerDocument): string {
  return canonicalizeHashDocument(document);
}

export function createDefaultDesignerDocument(seed: DesignerDocumentSeed): DesignerDocument {
  const pageCode = normalizeCode(seed.pageCode || 'designer_page');
  const rootId = `${pageCode}_root`;
  return withHash({
    actions: [], apiBindings: [], dataSources: [], documentId: pageCode,
    elements: { [rootId]: createDefaultNode(rootId, seed.pageName || 'Page') },
    metadata: { source: 'latest-designer' }, modals: [], pageMicroflows: [], pageParameters: [],
    pages: [{ id: pageCode, name: seed.pageName || 'Page', rootElementId: rootId }], pageType: seed.pageType ?? 'standard',
    permissions: {}, runtimeContext: { pageCode, pageName: seed.pageName }, revision: 1,
    styleTokens: {}, variables: [], workflowBindings: []
  });
}

function normalizeDesignerDocument(raw: Record<string, unknown>, seed: DesignerDocumentSeed, diagnostics: string[]): DesignerDocument {
  const fallback = createDefaultDesignerDocument(seed);
  const elements = isRecord(raw.elements)
    ? Object.fromEntries(Object.entries(raw.elements).map(([id, value]) => [id, normalizeNode(id, value, diagnostics)]))
    : fallback.elements;
  const pages = readRecords(raw.pages).map((page) => ({
    id: readString(page.id) ?? normalizeCode(seed.pageCode),
    name: readString(page.name) ?? seed.pageName,
    rootElementId: readString(page.rootElementId) ?? `${normalizeCode(seed.pageCode)}_root`
  }));
  const normalizedPages = pages.length > 0 ? pages : fallback.pages;
  return {
    actions: normalizeRecordList(raw.actions), apiBindings: normalizeRecordList(raw.apiBindings), dataSources: normalizeRecordList(raw.dataSources),
    documentId: readString(raw.documentId) ?? normalizeCode(seed.pageCode),
    elements: normalizePageRootLayouts(elements, normalizedPages), metadata: normalizeResourceReferences(isRecord(raw.metadata) ? raw.metadata : {}) as Record<string, unknown>,
    modals: readRecords(raw.modals).map((item) => ({ id: readString(item.id) ?? '', name: readString(item.name) ?? '', rootElementId: readString(item.rootElementId) ?? '', type: readString(item.type) ?? 'dialog' })),
    pageMicroflows: normalizeRecordList(raw.pageMicroflows), pageParameters: normalizeRecordList(raw.pageParameters),
    pages: normalizedPages, pageType: readString(raw.pageType) ?? seed.pageType ?? fallback.pageType ?? 'standard', permissions: isRecord(raw.permissions) ? raw.permissions : {},
    runtimeContext: normalizeResourceReferences(isRecord(raw.runtimeContext) ? raw.runtimeContext : fallback.runtimeContext) as Record<string, unknown>,
    revision: readPositiveInteger(raw.revision) ?? 1, styleTokens: isRecord(raw.styleTokens) ? raw.styleTokens : {},
    variables: normalizeRecordList(raw.variables), workflowBindings: normalizeRecordList(raw.workflowBindings)
  };
}

function normalizeNode(id: string, value: unknown, diagnostics: string[]): DesignerDocumentNode {
  const raw = isRecord(value) ? value : {};
  const bindings = normalizeBindings(raw);
  if (isRecord(raw.props) && isRecord(raw.props.permission)) {
    diagnostics.push(`elements.${id}.props.permission is ignored; use node.permission.code`);
  }
  const node: DesignerDocumentNode = {
    bindings, children: readStrings(raw.children),
    events: normalizeRecordList(raw.events), id: readString(raw.id) ?? id,
    layout: normalizeNodeLayout(raw.layout, id, diagnostics), name: readString(raw.name) ?? readString(raw.type) ?? id,
    parentId: readString(raw.parentId), permission: normalizePermission(raw.permission),
    props: normalizeResourceReferences(isRecord(raw.props) ? raw.props : {}) as Record<string, unknown>,
    style: normalizeResourceReferences(isRecord(raw.style) ? raw.style : {}) as Record<string, unknown>, validation: readRecords(raw.validation), type: readString(raw.type) ?? 'unknown'
  };
  if (raw.responsiveOverrides !== undefined) {
    const normalized = normalizeResponsiveOverrideMap(raw.responsiveOverrides, `elements.${id}.responsiveOverrides`);
    diagnostics.push(...normalized.errors);
    if (normalized.errors.length === 0 && Object.keys(normalized.overrides).length > 0) node.responsiveOverrides = normalized.overrides as Record<string, Record<string, unknown>>;
  }
  return node;
}

const LAYOUT_PROTOCOL_KEYS = ['container', 'placement', 'size'] as const;
const PLACEMENT_PAYLOAD_KEYS = ['absolute', 'flexItem', 'gridItem', 'constrained'] as const;

function normalizeNodeLayout(value: unknown, nodeId: string, diagnostics: string[]): DesignerNodeLayout {
  const normalizedValue = normalizeResourceReferences(isRecord(value) ? value : {});
  const layout = isRecord(normalizedValue) ? normalizedValue as DesignerNodeLayout : {};
  const source = resolveLayoutProtocolSource(layout);
  if (!source) return layout;

  const errors = validateLayoutProtocolFields(layout, `elements.${nodeId}.layout`);
  diagnostics.push(...errors);
  if (errors.length > 0 || !source.value) return layout;

  const safe = createSafeLayoutProtocol(source.value);
  const normalized = normalizeLayoutProtocol(safe);
  const mode = normalized.container.mode;
  const payloadKey = mode === 'free' ? 'absolute' : mode === 'flex' ? 'flexItem' : mode === 'grid' ? 'gridItem' : 'constrained';
  const normalizedPlacement = normalized.placement as unknown as Record<string, unknown>;
  const sourcePayload = safe.placement[payloadKey];
  const canonicalPlacement = {
    ...normalizedPlacement,
    [payloadKey]: {
      ...(isRecord(normalizedPlacement[payloadKey]) ? normalizedPlacement[payloadKey] : {}),
      ...(isRecord(sourcePayload) ? sourcePayload : {})
    }
  };
  return {
    container: normalized.container,
    placement: canonicalPlacement,
    size: normalized.size,
    ...(normalized.anchor ? { anchor: normalized.anchor } : {}),
    ...(normalized.migration ? { migration: normalized.migration } : {})
  } as unknown as DesignerNodeLayout;
}

function normalizeDocumentLayoutProtocols(document: DesignerDocument, diagnostics: string[]): DesignerDocument {
  const elements = Object.fromEntries(Object.entries(document.elements).map(([id, node]) => [id, {
    ...node,
    layout: normalizeNodeLayout(node.layout, id, diagnostics)
  }]));
  return { ...document, elements: normalizePageRootLayouts(elements, document.pages) };
}

function resolveLayoutProtocolSource(layout: Record<string, unknown>): { key: 'protocol' | 'layoutProtocol' | null; value: Record<string, unknown> | null } | null {
  if (Object.prototype.hasOwnProperty.call(layout, 'protocol')) return { key: 'protocol', value: isRecord(layout.protocol) ? layout.protocol : null };
  if (Object.prototype.hasOwnProperty.call(layout, 'layoutProtocol')) return { key: 'layoutProtocol', value: isRecord(layout.layoutProtocol) ? layout.layoutProtocol : null };
  if (LAYOUT_PROTOCOL_KEYS.some((key) => Object.prototype.hasOwnProperty.call(layout, key))) return { key: null, value: layout };
  return null;
}

function validateLayoutProtocolFields(layout: Record<string, unknown>, path: string): string[] {
  const source = resolveLayoutProtocolSource(layout);
  if (!source) return [];
  const errors: string[] = [];
  if (!source.value) {
    errors.push(`${path}.${source.key ?? 'protocol'} must be an object`);
    return errors;
  }
  const raw = source.value;
  for (const key of LAYOUT_PROTOCOL_KEYS) if (!isRecord(raw[key])) errors.push(`${path}.${source.key ? `${source.key}.` : ''}${key} must be an object`);
  const rawContainer = isRecord(raw.container) ? raw.container : {};
  if (!isLayoutMode(rawContainer.mode)) errors.push(`${path}.${source.key ? `${source.key}.` : ''}container.mode is invalid`);
  const rawSize = isRecord(raw.size) ? raw.size : {};
  if (!Object.prototype.hasOwnProperty.call(rawSize, 'width')) errors.push(`${path}.${source.key ? `${source.key}.` : ''}size.width is required`);
  if (!Object.prototype.hasOwnProperty.call(rawSize, 'height')) errors.push(`${path}.${source.key ? `${source.key}.` : ''}size.height is required`);

  const safe = createSafeLayoutProtocol(raw);
  errors.push(...validateLayoutProtocol(safe).map((diagnostic) => `${path}.${source.key ? `${source.key}.` : ''}${diagnostic.path} is invalid (${diagnostic.code})`));
  return unique(errors);
}

function createSafeLayoutProtocol(raw: Record<string, unknown>): LayoutProtocol {
  const rawContainer = isRecord(raw.container) ? raw.container : {};
  const mode: LayoutMode = isLayoutMode(rawContainer.mode) ? rawContainer.mode : 'free';
  const container: Record<string, unknown> = { mode };
  if (isRecord(rawContainer.flex)) container.flex = rawContainer.flex;
  if (isRecord(rawContainer.grid)) container.grid = { ...rawContainer.grid, columns: Array.isArray(rawContainer.grid.columns) ? rawContainer.grid.columns : [], rows: Array.isArray(rawContainer.grid.rows) ? rawContainer.grid.rows : [] };
  if (isRecord(rawContainer.constraints)) container.constraints = rawContainer.constraints;

  const rawPlacement = isRecord(raw.placement) ? raw.placement : {};
  const expectedKind = mode === 'free' ? 'absolute' : mode === 'flex' ? 'flex-item' : mode === 'grid' ? 'grid-item' : 'constrained';
  const placement: Record<string, unknown> = { kind: typeof rawPlacement.kind === 'string' ? rawPlacement.kind : expectedKind };
  for (const key of PLACEMENT_PAYLOAD_KEYS) if (isRecord(rawPlacement[key])) placement[key] = rawPlacement[key];
  const size = isRecord(raw.size) ? raw.size : {};
  return {
    container: container as unknown as LayoutProtocol['container'],
    placement: placement as unknown as LayoutProtocol['placement'],
    size: size as unknown as LayoutProtocol['size'],
    ...(isRecord(raw.anchor) ? { anchor: raw.anchor as unknown as LayoutProtocol['anchor'] } : {}),
    ...(isRecord(raw.migration) ? { migration: raw.migration as unknown as LayoutProtocol['migration'] } : {})
  };
}

function isLayoutMode(value: unknown): value is LayoutMode {
  return value === 'free' || value === 'flex' || value === 'grid' || value === 'constraints';
}

function normalizeBindings(raw: Record<string, unknown>): Record<string, unknown> {
  return normalizeResourceReferences(isRecord(raw.bindings) ? raw.bindings : {}) as Record<string, unknown>;
}

function normalizePermission(value: unknown): Record<string, unknown> {
  if (!isRecord(value)) return {};
  const code = readString(value.code);
  return {
    ...(code ? { code } : {}),
    ...(value.visibleWhen !== undefined ? { visibleWhen: normalizeResourceReferences(value.visibleWhen) } : {})
  };
}

function normalizeRecordList(value: unknown): Array<Record<string, unknown>> {
  return readRecords(normalizeResourceReferences(value));
}

/** Canonicalizes persisted ResourceRefs at the API read boundary so rename only changes labels. */
function normalizeResourceReferences(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(normalizeResourceReferences);
  if (!isRecord(value)) return value;

  const rawResourceId = readString(value.resourceId);
  const looksLikeBinding = Boolean(rawResourceId && (value.resourceType !== undefined || value.valueType !== undefined || value.expectedType !== undefined || value.displayName !== undefined));
  const normalized = Object.fromEntries(Object.entries(value).map(([key, child]) => [key, normalizeResourceReferences(child)]));

  if (looksLikeBinding && rawResourceId) {
    const resourceType = readString(normalized.resourceType) ?? resourceTypeForId(rawResourceId);
    normalized.resourceType = resourceType;
    normalized.resourceId = normalizeResourceId(rawResourceId, resourceType);
    normalized.expectedType ??= normalized.valueType ?? 'json';
    normalized.conversionPipeline ??= [];
    return normalized;
  }
  return normalized;
}

function createDefaultNode(id: string, name: string): DesignerDocumentNode {
  return {
    bindings: {}, children: [], events: [], id,
    layout: {
      container: defaultContainerLayout('free'),
      placement: defaultPlacement('free'),
      size: { height: 720, minHeight: 720, width: 1280 }
    },
    name, parentId: null, permission: {}, props: { title: name }, style: { backgroundColor: '#ffffff', padding: 16 }, type: 'layout.page', validation: []
  };
}

/** A page root is an artboard, not a flex container; rows and grids are explicit child components. */
function normalizePageRootLayouts(elements: Record<string, DesignerDocumentNode>, pages: readonly { rootElementId: string }[]): Record<string, DesignerDocumentNode> {
  const roots = new Set(pages.map((page) => page.rootElementId));
  return Object.fromEntries(Object.entries(elements).map(([id, node]) => {
    if (!roots.has(id) || node.type !== 'layout.page') return [id, node];
    // normalizeNodeLayout already returned the canonical value. Never reinterpret it as legacy.
    if (resolveLayoutProtocolSource(node.layout)) return [id, node];
    return [id, { ...node, layout: createCanonicalLegacyPageRootLayout(node.layout) }];
  }));
}

function createCanonicalLegacyPageRootLayout(layout: DesignerNodeLayout): DesignerNodeLayout {
  const mode = isLayoutMode(layout.layoutMode) ? layout.layoutMode : 'free';
  const size: LayoutProtocol['size'] = {
    height: readDimension(layout.height) ?? readDimension(layout.minHeight) ?? 720,
    width: readDimension(layout.width) ?? 1280
  };
  const minWidth = readDimension(layout.minWidth);
  const maxWidth = readDimension(layout.maxWidth);
  const minHeight = readDimension(layout.minHeight);
  const maxHeight = readDimension(layout.maxHeight);
  const aspectRatio = readPositiveNumber(layout.aspectRatio);
  if (minWidth !== undefined) size.minWidth = minWidth;
  if (maxWidth !== undefined) size.maxWidth = maxWidth;
  if (minHeight !== undefined) size.minHeight = minHeight;
  if (maxHeight !== undefined) size.maxHeight = maxHeight;
  if (aspectRatio !== undefined) size.aspectRatio = aspectRatio;

  const protocol: LayoutProtocol = {
    container: createLegacyPageRootContainer(layout, mode),
    placement: defaultPlacement(mode),
    size
  };
  return normalizeLayoutProtocol(protocol) as DesignerNodeLayout;
}

function createLegacyPageRootContainer(layout: DesignerNodeLayout, mode: LayoutMode): LayoutProtocol['container'] {
  const container = defaultContainerLayout(mode);
  if (mode === 'flex') {
    const flex = isRecord(layout.flex) ? layout.flex : {};
    container.flex = {
      ...container.flex!,
      alignItems: readEnum(flex.alignItems ?? layout.alignItems, ['start', 'center', 'end', 'stretch', 'baseline'], container.flex!.alignItems),
      direction: readEnum(flex.direction ?? layout.flexDirection, ['row', 'row-reverse', 'column', 'column-reverse'], container.flex!.direction),
      gap: readNonNegativeNumber(flex.gap ?? layout.gap) ?? container.flex!.gap,
      justifyContent: readEnum(flex.justifyContent ?? layout.justifyContent, ['start', 'center', 'end', 'space-between', 'space-around', 'space-evenly'], container.flex!.justifyContent),
      wrap: readEnum(flex.wrap ?? layout.flexWrap, ['nowrap', 'wrap', 'wrap-reverse'], container.flex!.wrap)
    };
  }
  if (mode === 'grid') {
    const grid = isRecord(layout.grid) ? layout.grid : {};
    container.grid = {
      ...container.grid!,
      autoFlow: readEnum(grid.autoFlow ?? layout.gridAutoFlow, ['row', 'column', 'dense', 'row-dense', 'column-dense'], container.grid!.autoFlow),
      columnGap: readNonNegativeNumber(grid.columnGap ?? layout.columnGap ?? layout.gap) ?? container.grid!.columnGap,
      columns: readTrackList(grid.columns ?? layout.columns ?? layout.gridTemplateColumns, container.grid!.columns),
      rowGap: readNonNegativeNumber(grid.rowGap ?? layout.rowGap ?? layout.gap) ?? container.grid!.rowGap,
      rows: readTrackList(grid.rows ?? layout.rows ?? layout.gridTemplateRows, container.grid!.rows)
    };
  }
  if (mode === 'constraints') {
    container.constraints = { coordinateSpace: 'parent-padding-box' };
  }
  return container;
}

function readDimension(value: unknown): Dimension | undefined {
  if (typeof value === 'number') return Number.isFinite(value) && value >= 0 ? value : undefined;
  if (typeof value !== 'string') return undefined;
  const normalized = value.trim();
  if (/^\d+(?:\.\d+)?$/.test(normalized)) return Number(normalized);
  if (/^\d+(?:\.\d+)?(?:%|px)$/.test(normalized) || ['auto', 'min-content', 'max-content', 'fit-content'].includes(normalized)) return normalized as Dimension;
  return undefined;
}

function readNonNegativeNumber(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : undefined;
}

function readPositiveNumber(value: unknown): number | undefined {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : undefined;
}

function readEnum<T extends string>(value: unknown, allowed: readonly T[], fallback: T): T {
  return typeof value === 'string' && allowed.includes(value as T) ? value as T : fallback;
}

function readTrackList(value: unknown, fallback: readonly string[]): string[] {
  if (Array.isArray(value)) {
    const tracks = value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0).map((item) => item.trim());
    if (tracks.length > 0) return tracks;
  }
  if (typeof value === 'number' && Number.isInteger(value) && value > 0) return Array.from({ length: value }, () => '1fr');
  if (typeof value === 'string' && value.trim().length > 0) return value.trim().split(/\s+/);
  return [...fallback];
}

export function validateDesignerDocument(document: DesignerDocument): string[] {
  const errors: string[] = validateDesignerDocumentBoundary(document);
  if (!document.documentId.trim()) errors.push('documentId is required');
  if (!document.pageType?.trim()) errors.push('pageType is required');
  if (!Number.isInteger(document.revision) || document.revision < 1) errors.push('revision must be a positive integer');
  if (document.pages.length === 0) errors.push('pages must be a non-empty array');
  const elements = document.elements;
  const roots = document.pages.map((page) => page.rootElementId);
  if (Object.keys(elements).length > DESIGNER_DOCUMENT_LIMITS.elements) errors.push(`elements exceeds ${DESIGNER_DOCUMENT_LIMITS.elements}`);
  for (const page of document.pages) if (!elements[page.rootElementId]) errors.push(`root element is missing: ${page.rootElementId}`);
  for (const [elementId, node] of Object.entries(elements)) {
    if (node.id !== elementId) errors.push(`element map key does not match id: ${elementId}`);
    if (node.children.length > DESIGNER_DOCUMENT_LIMITS.children) errors.push(`element ${node.id} exceeds children limit`);
    if (new Set(node.children).size !== node.children.length) errors.push(`element ${node.id} has duplicate children`);
    for (const childId of node.children) {
      const child = elements[childId];
      if (!child) errors.push(`element ${node.id} references missing child ${childId}`);
      else if (child.parentId !== node.id) errors.push(`parent/children mismatch for ${node.id}/${childId}`);
    }
    if (node.parentId && (!elements[node.parentId] || !elements[node.parentId].children.includes(node.id))) errors.push(`children/parent mismatch for ${node.id}`);
    errors.push(...validateLatestPropertyValues(node.props, `elements.${elementId}.props`));
    errors.push(...validateLatestPropertyValues(node.layout, `elements.${elementId}.layout`));
    errors.push(...validateLayoutProtocolFields(node.layout, `elements.${elementId}.layout`));
    errors.push(...validateLatestPropertyValues(node.style, `elements.${elementId}.style`));
    errors.push(...validateLatestBindings(node.bindings, `elements.${elementId}.bindings`));
    errors.push(...validateLatestPropertyValues(node.responsiveOverrides, `elements.${elementId}.responsiveOverrides`));
    errors.push(...validateResponsiveOverrideMap(node.responsiveOverrides, `elements.${elementId}.responsiveOverrides`));
  }
  const visiting = new Set<string>();
  const reachable = new Set<string>();
  const visit = (id: string, depth: number) => {
    if (visiting.has(id)) { errors.push(`element graph contains a cycle at ${id}`); return; }
    if (reachable.has(id)) return;
    if (depth > DESIGNER_DOCUMENT_LIMITS.depth) { errors.push(`element graph exceeds depth ${DESIGNER_DOCUMENT_LIMITS.depth}`); return; }
    const node = elements[id];
    if (!node) return;
    visiting.add(id); reachable.add(id); node.children.forEach((childId) => visit(childId, depth + 1)); visiting.delete(id);
  };
  roots.forEach((root) => visit(root, 1));
  Object.keys(elements).filter((id) => !reachable.has(id)).forEach((id) => errors.push(`element is unreachable: ${id}`));
  return errors;
}

function withHash(document: DesignerDocument): DesignerDocument { return { ...document, documentHash: computeDesignerDocumentHash(document) }; }

function validateLatestBindings(value: unknown, path: string): string[] {
  if (!isRecord(value)) return [];
  const errors: string[] = [];
  for (const [key, child] of Object.entries(value)) {
    if (key === 'props' || key.startsWith('props.')) errors.push(`${path}.${key} is a legacy property-binding location; use props/layout/style`);
    errors.push(...validateLatestPropertyValues(child, `${path}.${key}`));
  }
  return errors;
}

function validateLatestPropertyValues(value: unknown, path: string): string[] {
  if (Array.isArray(value)) return value.flatMap((item, index) => validateLatestPropertyValues(item, `${path}[${index}]`));
  if (!isRecord(value)) return [];
  const errors: string[] = [];
  const isMicroflowExpression = path.endsWith('.sourceExpression') || path.endsWith('.valueExpression');
  if (!isMicroflowExpression && (Object.prototype.hasOwnProperty.call(value, 'source') || Object.prototype.hasOwnProperty.call(value, 'path'))) {
    errors.push(`${path} uses legacy source/path binding fields; migrate it before parsing DesignerDocument`);
  }
  Object.entries(value).forEach(([key, child]) => errors.push(...validateLatestPropertyValues(child, `${path}.${key}`)));
  return errors;
}

function inspectForbiddenKeys(value: unknown, path: string, errors: string[], seen: WeakSet<object>): void {
  if (Array.isArray(value)) { value.forEach((item, index) => inspectForbiddenKeys(item, `${path}[${index}]`, errors, seen)); return; }
  if (!isRecord(value) || seen.has(value)) return;
  seen.add(value);
  Object.entries(value).forEach(([key, child]) => { if (FORBIDDEN_KEYS.has(key)) errors.push(`${path}.${key} is forbidden`); inspectForbiddenKeys(child, `${path}.${key}`, errors, seen); });
}
function inspectLegacyNodeKeys(value: Record<string, unknown>, errors: string[]): void {
  if (!isRecord(value.elements)) return;
  for (const [id, node] of Object.entries(value.elements)) {
    if (isRecord(node) && Object.prototype.hasOwnProperty.call(node, 'dataBinding')) {
      errors.push(`elements.${id}.dataBinding is a legacy binding entry; migrate it before parsing DesignerDocument`);
    }
  }
}
function validateSize(value: unknown, maxBytes: number, label: string, errors: string[]): void { if (new TextEncoder().encode(JSON.stringify(value)).byteLength > maxBytes) errors.push(`${label} size exceeds ${maxBytes} bytes`); }
function readRecords(value: unknown): Record<string, unknown>[] { return Array.isArray(value) ? value.filter(isRecord) : []; }
function readStrings(value: unknown): string[] { return Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string' && item.trim().length > 0) : []; }
function readString(value: unknown): string | null { return typeof value === 'string' && value.trim() ? value.trim() : null; }
function readPositiveInteger(value: unknown): number | null { return typeof value === 'number' && Number.isInteger(value) && value > 0 ? value : null; }
function normalizeCode(value: string): string { return value.trim().replace(/[^A-Za-z0-9_]+/g, '_').replace(/^_+|_+$/g, '').toLowerCase() || 'designer_page'; }
function unique(values: readonly string[]): string[] { return [...new Set(values)]; }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
