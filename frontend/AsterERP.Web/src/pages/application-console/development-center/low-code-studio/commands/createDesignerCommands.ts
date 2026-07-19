import { applyWorkflowBindingDraft } from '../binding/workflowBindingModel';
import type { WorkflowBindingDraft, WorkflowPageContext } from '../binding/workflowBindingTypes';
import type { DesignerDocument, DesignerDocumentNode, DesignerNodeLayout } from '../document/DesignerDocument';
import type { ResourceRef } from '../document/ResourceRef';
import type { TypedValue } from '../document/TypedValue';
import { calculateLayoutChanges, createLayoutOperationChange, resolveLayoutMode, type LayoutContainer, type LayoutOperation, type LayoutNode } from '../layout/layoutOperations';
import { defaultContainerLayout, defaultPlacement, type ConstraintPlacement, type Dimension, type LayoutMigrationAnchor, type LayoutProtocol } from '../layout/LayoutProtocol';
import { mergeResponsiveOverride, normalizeResponsiveOverrideMap, type ResponsiveOverridePatch } from '../responsive/responsiveModel';

import type { DesignerCommand, DesignerCommandResult } from './DesignerCommand';
import { createInverseDesignerCommand } from './DesignerDocumentPatch';
import type { MoveNodesRequest } from './moveNodesContract';

export function createPatchNodeCommand(nodeId: string, patch: Partial<DesignerDocumentNode>, mergeKey?: string): DesignerCommand {
  return createNodeCommand('PatchNode', `Patch node ${nodeId}`, nodeId, (document, node) => ({ ...document, elements: { ...document.elements, [nodeId]: { ...node, ...patch } } }), mergeKey);
}

export function createPatchResponsiveOverrideCommand(nodeId: string, breakpointId: string, override: ResponsiveOverridePatch | Partial<DesignerDocumentNode>): DesignerCommand {
  return createNodeCommand('PatchResponsiveOverride', `Patch responsive override ${nodeId}`, nodeId, (document, node) => {
    const normalized = normalizeResponsiveOverrideMap(node.responsiveOverrides).overrides;
    const nextOverride = mergeResponsiveOverride(normalized[breakpointId], toResponsiveOverridePatch(override));
    const responsiveOverrides = { ...normalized };
    if (nextOverride) responsiveOverrides[breakpointId] = nextOverride;
    else delete responsiveOverrides[breakpointId];
    return { ...document, elements: { ...document.elements, [nodeId]: { ...node, responsiveOverrides: responsiveOverrides as Record<string, Record<string, unknown>> } } };
  });
}

function toResponsiveOverridePatch(value: ResponsiveOverridePatch | Partial<DesignerDocumentNode>): ResponsiveOverridePatch {
  const patch = value as Record<string, unknown>;
  return {
    layout: patch.layout === null ? null : isRecord(patch.layout) ? patch.layout : undefined,
    props: patch.props === null ? null : isRecord(patch.props) ? patch.props : undefined,
    style: patch.style === null ? null : isRecord(patch.style) ? patch.style : undefined
  };
}

export function createBindValueCommand(nodeId: string, property: string, resource: ResourceRef): DesignerCommand {
  const path = property.startsWith('props.') || property.startsWith('layout.') || property.startsWith('style.') || property.startsWith('bindings.') ? property : `props.${property}`;
  if (path === 'bindings.props' || path.startsWith('bindings.props.')) {
    throw new Error('Property bindings must be stored in props/layout/style, not bindings.props.');
  }
  return createNodeCommand('BindValue', `Bind ${property} on ${nodeId}`, nodeId, (document, node) => ({
    ...document,
    elements: { ...document.elements, [nodeId]: setNodePath(node, path, resource) }
  }));
}

export function createSetLayoutModeCommand(containerId: string, container: LayoutContainer, mergeKey?: string): DesignerCommand {
  return createDocumentCommand('SetLayoutMode', `Set layout mode ${containerId}`, (document) => migrateLayoutMode(document, containerId, container), mergeKey);
}

interface LayoutGeometry {
  height: number;
  width: number;
  x: number;
  y: number;
}

interface LayoutMigrationSnapshot {
  geometry: LayoutGeometry;
  originalLayout: DesignerDocumentNode['layout'];
  protocol: LayoutProtocol;
}

const CHILD_PLACEMENT_FIELDS = [
  'alignSelf',
  'constraints',
  'flex',
  'flexBasis',
  'flexGrow',
  'flexShrink',
  'gridArea',
  'gridColumn',
  'gridColumnSpan',
  'gridRow',
  'gridRowSpan',
  'justifySelf',
  'order',
  'position',
  'x',
  'y'
] as const;

function migrateLayoutMode(document: DesignerDocument, containerId: string, target: LayoutContainer): DesignerCommandResult {
  const node = document.elements[containerId];
  if (!node) return failure(document, [`Node not found: ${containerId}`]);
  if (!isLayoutMode(target.mode)) return failure(document, [`Unsupported layout mode: ${String(target.mode)}`]);

  const childIds = [...node.children];
  if (new Set(childIds).size !== childIds.length) return failure(document, [`Container contains duplicate children: ${containerId}`]);
  const children: DesignerDocumentNode[] = [];
  for (const childId of childIds) {
    const child = document.elements[childId];
    if (!child) return failure(document, [`Child not found: ${childId}`]);
    children.push(child);
  }

  try {
    const sourceMode = resolveLayoutMode(node.layout);
    const snapshots = children.map((child, index) => createLayoutMigrationSnapshot(child, sourceMode, index, children, node.layout));
    const columns = positiveInteger(target.columns, positiveInteger(node.layout.columns, 1));
    const rows = positiveInteger(target.rows, positiveInteger(node.layout.rows, columns));
    const nextContainerLayout = createCanonicalContainerLayout(node.layout, { ...target, ...(target.mode === 'grid' ? { columns, rows } : {}) }, columns, rows);
    const elements = { ...document.elements, [containerId]: { ...node, layout: nextContainerLayout } };

    children.forEach((child, index) => {
      const snapshot = snapshots[index];
      elements[child.id] = {
        ...child,
        layout: applyTargetPlacement(snapshot, target.mode, index, columns)
      };
    });
    return success({ ...document, elements });
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown layout migration failure';
    return failure(document, [`Layout migration failed: ${message}`]);
  }
}

function createLayoutMigrationSnapshot(
  node: DesignerDocumentNode,
  sourceMode: LayoutContainer['mode'],
  index: number,
  siblings: readonly DesignerDocumentNode[],
  parentLayout: DesignerDocumentNode['layout']
): LayoutMigrationSnapshot {
  const geometry = readExistingGeometry(node, sourceMode, index, siblings, parentLayout);
  const protocol: LayoutProtocol = {
    container: readProtocolContainer(parentLayout, sourceMode),
    placement: readProtocolPlacement(node.layout, sourceMode, geometry, index, parentLayout),
    size: readProtocolSize(node.layout),
    ...(readCanonicalProtocol(node.layout)?.anchor ? { anchor: readCanonicalProtocol(node.layout)!.anchor } : {})
  };
  return { geometry, originalLayout: node.layout, protocol };
}

function readProtocolContainer(layout: DesignerDocumentNode['layout'], mode: LayoutContainer['mode']): LayoutProtocol['container'] {
  const canonical = readCanonicalProtocol(layout);
  if (canonical) return canonical.container;
  const defaults = defaultContainerLayout(mode);
  if (mode === 'flex') return { ...defaults, flex: { ...defaults.flex!, direction: layout.flexDirection === 'column' ? 'column' : 'row', gap: nonNegativeNumber(layout.gap) } };
  if (mode === 'grid') return { ...defaults, grid: { ...defaults.grid!, columns: [String(positiveInteger(layout.columns, 1))], rows: [String(positiveInteger(layout.rows, 1))], columnGap: nonNegativeNumber(layout.gap), rowGap: nonNegativeNumber(layout.gap) } };
  return defaults;
}

function readProtocolPlacement(
  layout: DesignerDocumentNode['layout'],
  mode: LayoutContainer['mode'],
  geometry: LayoutGeometry,
  index: number,
  parentLayout: DesignerDocumentNode['layout']
): LayoutProtocol['placement'] {
  const canonical = readCanonicalProtocol(layout);
  if (canonical) return canonical.placement;
  if (mode === 'flex') {
    const flex = defaultPlacement('flex').flexItem!;
    return { kind: 'flex-item', flexItem: { ...flex, order: integer(layout.order, index), grow: nonNegativeNumber(layout.flexGrow, readFlexShorthand(layout.flex, 0)), shrink: nonNegativeNumber(layout.flexShrink, readFlexShorthand(layout.flex, 1)), basis: dimension(layout.flexBasis ?? readFlexBasis(layout.flex)), ...(layout.alignSelf === undefined ? {} : { alignSelf: readFlexAlignment(layout.alignSelf) }) } };
  }
  if (mode === 'grid') {
    const grid = defaultPlacement('grid').gridItem!;
    const columns = positiveInteger(parentLayout.columns, 1);
    return { kind: 'grid-item', gridItem: { ...grid, rowStart: positiveInteger(readGridLine(layout.gridRow), Math.floor(index / columns) + 1), columnStart: positiveInteger(readGridLine(layout.gridColumn), index % columns + 1), rowSpan: positiveInteger(layout.gridRowSpan, 1), columnSpan: positiveInteger(layout.gridColumnSpan, 1), ...(layout.alignSelf === undefined ? {} : { alignSelf: readGridAlignment(layout.alignSelf) }), ...(layout.justifySelf === undefined ? {} : { justifySelf: readGridAlignment(layout.justifySelf) }) } };
  }
  if (mode === 'constraints') return { kind: 'constrained', constrained: readConstraintPlacement(layout.constraints, geometry) };
  return { kind: 'absolute', absolute: { x: geometry.x, y: geometry.y, ...(finiteNumber(layout.zIndex) === undefined ? {} : { zIndex: finiteNumber(layout.zIndex) }) } };
}

function readProtocolSize(layout: DesignerDocumentNode['layout']): LayoutProtocol['size'] {
  const canonical = readCanonicalProtocol(layout);
  if (canonical) return canonical.size;
  return {
    width: dimension(layout.width),
    height: dimension(layout.height),
    ...(layout.minWidth === undefined ? {} : { minWidth: dimension(layout.minWidth) }),
    ...(layout.maxWidth === undefined ? {} : { maxWidth: dimension(layout.maxWidth) }),
    ...(layout.minHeight === undefined ? {} : { minHeight: dimension(layout.minHeight) }),
    ...(layout.maxHeight === undefined ? {} : { maxHeight: dimension(layout.maxHeight) }),
    ...(finiteNumber(layout.aspectRatio) === undefined ? {} : { aspectRatio: finiteNumber(layout.aspectRatio) })
  };
}

function readExistingGeometry(
  node: DesignerDocumentNode,
  mode: LayoutContainer['mode'],
  index: number,
  siblings: readonly DesignerDocumentNode[],
  parentLayout: DesignerDocumentNode['layout']
): LayoutGeometry {
  const canonical = readCanonicalProtocol(node.layout);
  const width = positiveNumber(canonical?.size.width ?? canonical?.anchor?.rect.width ?? node.layout.width, 160);
  const height = positiveNumber(canonical?.size.height ?? canonical?.anchor?.rect.height ?? node.layout.height, 48);
  const explicitX = finiteNumber(canonical?.placement.absolute?.x ?? canonical?.anchor?.rect.x ?? node.layout.x);
  const explicitY = finiteNumber(canonical?.placement.absolute?.y ?? canonical?.anchor?.rect.y ?? node.layout.y);
  if (mode === 'constraints') {
    const constraints = readConstraintPlacement(node.layout.constraints, { width, height, x: explicitX ?? 0, y: explicitY ?? 0 });
    return { width, height, x: explicitX ?? resolveConstraintPosition(constraints, parentLayout, siblings, index, 'x', width), y: explicitY ?? resolveConstraintPosition(constraints, parentLayout, siblings, index, 'y', height) };
  }
  if (mode === 'flex') return { width, height, x: explicitX ?? flowOffset(siblings, index, parentLayout, 'x'), y: explicitY ?? flowOffset(siblings, index, parentLayout, 'y') };
  if (mode === 'grid') {
    const columns = positiveInteger(parentLayout.columns, 1);
    const column = positiveInteger(readGridLine(node.layout.gridColumn), index % columns + 1) - 1;
    const row = positiveInteger(readGridLine(node.layout.gridRow), Math.floor(index / columns) + 1) - 1;
    const gap = nonNegativeNumber(parentLayout.gap);
    const parentWidth = positiveNumber(layoutDimension(parentLayout, 'width'), width * columns + gap * Math.max(0, columns - 1));
    const cellWidth = Math.max(1, (parentWidth - gap * Math.max(0, columns - 1)) / columns);
    return { width, height, x: explicitX ?? column * (cellWidth + gap), y: explicitY ?? row * (height + gap) };
  }
  return { width, height, x: explicitX ?? 0, y: explicitY ?? 0 };
}

function applyTargetPlacement(
  snapshot: LayoutMigrationSnapshot,
  targetMode: LayoutContainer['mode'],
  index: number,
  columns: number
): DesignerDocumentNode['layout'] {
  // A canonical child layout describes the child itself. Switching its parent
  // container must not overwrite that child-owned protocol. Earlier children
  // still use the computed migration below so the boundary is upgraded once.
  if (readCanonicalProtocol(snapshot.originalLayout)) return snapshot.originalLayout;
  const sourceContainer = defaultContainerLayout(targetMode);
  const size = snapshot.protocol.size;
  if (targetMode === 'free') return canonicalLayout(sourceContainer, defaultPlacement('free', snapshot.geometry.x, snapshot.geometry.y), size);
  const anchor: LayoutMigrationAnchor = {
    coordinateSpace: 'parent-padding-box',
    rect: { x: snapshot.geometry.x, y: snapshot.geometry.y, width: Math.max(1, snapshot.geometry.width), height: Math.max(1, snapshot.geometry.height) },
    sequence: index
  };
  if (targetMode === 'flex') {
    const source = snapshot.protocol.placement.flexItem ?? defaultPlacement('flex').flexItem!;
    return { ...canonicalLayout(sourceContainer, { kind: 'flex-item', flexItem: { ...source } }, size), anchor };
  }
  if (targetMode === 'constraints') return { ...canonicalLayout(sourceContainer, { kind: 'constrained', constrained: { ...toConstraintPlacement(snapshot) } }, size), anchor };
  const source = snapshot.protocol.placement.gridItem ?? defaultPlacement('grid').gridItem!;
  return { ...canonicalLayout(sourceContainer, { kind: 'grid-item', gridItem: { ...source, rowStart: source.rowStart === 'auto' ? Math.floor(index / columns) + 1 : source.rowStart, columnStart: source.columnStart === 'auto' ? index % columns + 1 : source.columnStart } }, size), anchor };
}

function createCanonicalContainerLayout(layout: DesignerDocumentNode['layout'], target: LayoutContainer, columns: number, rows: number): DesignerNodeLayout {
  const size = readProtocolSize(layout);
  const container = defaultContainerLayout(target.mode);
  const existing = readCanonicalProtocol(layout);
  const sourceContainer = existing?.container ?? readProtocolContainer(layout, resolveLayoutMode(layout));
  const previousContainers = existing?.migration?.previousContainers ?? {};
  const previousPlacements = existing?.migration?.previousPlacements ?? {};
  const rememberedTarget = previousContainers[target.mode];
  const restoringPreviousMode = sourceContainer.mode !== target.mode && rememberedTarget !== undefined;
  if (target.mode === 'flex') {
    const existingFlex = sourceContainer.mode === 'flex' ? sourceContainer.flex : rememberedTarget?.mode === 'flex' ? rememberedTarget.flex : undefined;
    container.flex = {
      ...container.flex!,
      direction: target.flexDirection ?? existingFlex?.direction ?? container.flex!.direction,
      gap: target.gap === undefined ? existingFlex?.gap ?? container.flex!.gap : nonNegativeNumber(target.gap),
      alignItems: target.align === 'center' ? 'center' : target.align === 'end' ? 'end' : target.align === 'start' ? 'start' : existingFlex?.alignItems ?? container.flex!.alignItems,
      justifyContent: target.justify === 'center' ? 'center' : target.justify === 'end' ? 'end' : target.justify === 'space-between' ? 'space-between' : existingFlex?.justifyContent ?? container.flex!.justifyContent
    };
  }
  if (target.mode === 'grid') {
    container.grid = {
      ...container.grid!,
      columns: Array.from({ length: Math.max(1, columns) }, () => '1fr'),
      rows: Array.from({ length: Math.max(1, rows) }, () => '1fr'),
      columnGap: nonNegativeNumber(target.gap),
      rowGap: nonNegativeNumber(target.gap)
    };
  }
  const nextPlacement = sourceContainer.mode === target.mode
    ? existing?.placement ?? defaultPlacement(target.mode)
    : previousPlacements[target.mode] ?? defaultPlacement(target.mode);
  const nextMigration = sourceContainer.mode === target.mode
    ? existing?.migration
    : restoringPreviousMode
      ? undefined
    : {
      previousContainers: { ...previousContainers, [sourceContainer.mode]: sourceContainer },
      previousPlacements: { ...previousPlacements, [sourceContainer.mode]: existing?.placement ?? defaultPlacement(sourceContainer.mode) }
    };
  return {
    ...canonicalLayout(container, nextPlacement, size),
    ...(nextMigration ? { migration: nextMigration } : {})
  } as DesignerNodeLayout;
}

function canonicalLayout(container: LayoutProtocol['container'], placement: LayoutProtocol['placement'], size: LayoutProtocol['size']): DesignerNodeLayout {
  return { container, placement, size };
}

function readCanonicalProtocol(layout: DesignerDocumentNode['layout']): LayoutProtocol | null {
  const source = isRecord(layout.protocol) ? layout.protocol : isRecord(layout.layoutProtocol) ? layout.layoutProtocol : layout.container && layout.placement && layout.size ? layout : null;
  if (!isRecord(source) || !isRecord(source.container) || !isRecord(source.placement) || !isRecord(source.size)) return null;
  return source as unknown as LayoutProtocol;
}

function layoutDimension(layout: DesignerDocumentNode['layout'], key: 'width' | 'height'): unknown {
  const canonical = readCanonicalProtocol(layout);
  return canonical?.size[key] ?? layout[key];
}

function toConstraintPlacement(snapshot: LayoutMigrationSnapshot): ConstraintPlacement {
  const source = snapshot.protocol.placement.constrained;
  if (source && Object.keys(source).length > 0) return source;
  return { left: snapshot.geometry.x, top: snapshot.geometry.y };
}

function readConstraintPlacement(value: unknown, geometry: Pick<LayoutGeometry, 'x' | 'y'> & Partial<Pick<LayoutGeometry, 'width' | 'height'>>): ConstraintPlacement {
  const source = isRecord(value) ? value : {};
  const constraints: ConstraintPlacement = {};
  for (const key of ['left', 'right', 'top', 'bottom', 'centerX', 'centerY'] as const) {
    const value = finiteNumber(source[key]);
    if (value !== undefined) constraints[key] = value;
  }
  for (const key of ['stretchX', 'stretchY'] as const) if (typeof source[key] === 'boolean') constraints[key] = source[key];
  if (Object.keys(constraints).length === 0) return { left: geometry.x, top: geometry.y };
  return constraints;
}

function resolveConstraintPosition(constraints: ConstraintPlacement, parentLayout: DesignerDocumentNode['layout'], siblings: readonly DesignerDocumentNode[], index: number, axis: 'x' | 'y', size: number): number {
  const start = axis === 'x' ? constraints.left : constraints.top;
  if (start !== undefined) return start;
  const end = axis === 'x' ? constraints.right : constraints.bottom;
  if (end !== undefined) return Math.max(0, positiveNumber(axis === 'x' ? parentLayout.width : parentLayout.height, size) - end - size);
  const center = axis === 'x' ? constraints.centerX : constraints.centerY;
  if (center !== undefined) return positiveNumber(axis === 'x' ? parentLayout.width : parentLayout.height, size) / 2 - size / 2 + center;
  return axis === 'x' ? flowOffset(siblings, index, parentLayout, 'x') : flowOffset(siblings, index, parentLayout, 'y');
}

function flowOffset(siblings: readonly DesignerDocumentNode[], index: number, parentLayout: DesignerDocumentNode['layout'], axis: 'x' | 'y'): number {
  const parentProtocol = readCanonicalProtocol(parentLayout);
  const direction = parentProtocol?.container.flex?.direction ?? (parentLayout.flexDirection === 'column' ? 'column' : 'row');
  if ((axis === 'x' && direction === 'column') || (axis === 'y' && direction === 'row')) return 0;
  const sizeKey = direction === 'row' ? 'width' : 'height';
  const gap = parentProtocol?.container.flex?.gap ?? nonNegativeNumber(parentLayout.gap);
  return siblings.slice(0, index).reduce((offset, sibling) => offset + positiveNumber(layoutDimension(sibling.layout, sizeKey), sizeKey === 'width' ? 160 : 48), 0) + gap * index;
}

function cleanLayoutFields(layout: DesignerDocumentNode['layout'], fields: readonly string[]): DesignerDocumentNode['layout'] {
  const next = { ...layout };
  for (const field of fields) delete next[field];
  return next;
}

function dimension(value: unknown): Dimension {
  if (typeof value === 'number' && Number.isFinite(value) && value >= 0) return value;
  if (typeof value === 'string' && /^(?:\d+(?:\.\d+)?)(?:%|px)$/.test(value.trim())) return value as Dimension;
  if (value === 'auto' || value === 'min-content' || value === 'max-content' || value === 'fit-content') return value;
  return 'auto';
}

function readFlexShorthand(value: unknown, fallback: number): number {
  if (typeof value !== 'string') return fallback;
  const candidate = Number.parseFloat(value.trim().split(/\s+/)[0] ?? '');
  return Number.isFinite(candidate) && candidate >= 0 ? candidate : fallback;
}

function readFlexBasis(value: unknown): unknown {
  if (typeof value !== 'string') return undefined;
  const parts = value.trim().split(/\s+/);
  return parts.length >= 3 ? parts[2] : undefined;
}

function readGridLine(value: unknown): number | undefined {
  if (typeof value === 'number') return finiteNumber(value);
  if (typeof value !== 'string') return undefined;
  return finiteNumber(Number.parseInt(value.trim().split(/\s|\//)[0] ?? '', 10));
}

function readFlexAlignment(value: unknown): 'auto' | 'start' | 'center' | 'end' | 'stretch' | 'baseline' {
  return value === 'center' || value === 'end' || value === 'stretch' || value === 'baseline' || value === 'start' || value === 'auto' ? value : 'auto';
}

function readGridAlignment(value: unknown): 'auto' | 'start' | 'center' | 'end' | 'stretch' {
  return value === 'center' || value === 'end' || value === 'stretch' || value === 'start' || value === 'auto' ? value : 'auto';
}

function isLayoutMode(value: unknown): value is LayoutContainer['mode'] { return value === 'free' || value === 'flex' || value === 'grid' || value === 'constraints'; }
function finiteNumber(value: unknown): number | undefined { return typeof value === 'number' && Number.isFinite(value) ? value : undefined; }
function positiveNumber(value: unknown, fallback: number): number { const result = finiteNumber(value); return result !== undefined && result > 0 ? result : fallback; }
function nonNegativeNumber(value: unknown, fallback = 0): number { const result = finiteNumber(value); return result !== undefined ? Math.max(0, result) : fallback; }
function integer(value: unknown, fallback: number): number { const result = finiteNumber(value); return result === undefined ? fallback : Math.floor(result); }
function positiveInteger(value: unknown, fallback: number): number { return Math.max(1, Math.floor(positiveNumber(value, fallback))); }
function clamp(value: number, min: number, max: number): number { return Math.min(max, Math.max(min, value)); }

export function createLayoutOperationCommand(
  nodeIds: readonly string[],
  operation: LayoutOperation,
  containerId?: string,
  breakpointId?: string | null
): DesignerCommand {
  return createDocumentCommand('LayoutOperation', `Apply layout operation ${operation}`, (document) => {
    const selected = nodeIds.map((nodeId) => document.elements[nodeId]);
    if (selected.some((node) => !node)) return failure(document, ['Layout operation contains an unknown node']);
    const parentId = containerId ?? selected[0]?.parentId ?? null;
    const parent = parentId ? document.elements[parentId] : undefined;
    const mode = resolveLayoutMode(parent?.layout ?? {});
    if (mode === 'free') {
      const nodes: LayoutNode[] = selected.map((node) => {
        const normalized = normalizeFreeLayout(node!.layout, parent?.layout);
        const geometry = readLayoutGeometry(normalized);
        return { id: node!.id, ...geometry };
      });
      const bounds = parent ? { height: number(layoutDimension(parent.layout, 'height')), width: number(layoutDimension(parent.layout, 'width')) } : undefined;
      const changes = calculateLayoutChanges(nodes, operation, mode, bounds && bounds.width > 0 && bounds.height > 0 ? bounds : undefined);
      if (changes.size === 0) return failure(document, ['Free layout operation requires at least two nodes']);
      const elements = { ...document.elements };
      const responsiveBreakpoint = breakpointId?.trim();
      for (const [id, change] of changes) {
        const node = elements[id];
        const nextLayout = normalizeFreeLayout({ ...node.layout, ...change }, parent?.layout);
        if (responsiveBreakpoint) {
          elements[id] = mergeNodeResponsiveLayout(node, responsiveBreakpoint, diffFreeGeometry(node.layout, nextLayout));
        } else {
          elements[id] = { ...node, layout: nextLayout };
        }
      }
      return success({ ...document, elements });
    }
    if (breakpointId?.trim()) return failure(document, [`Responsive layout operation is not supported by ${mode} layout`]);
    if (!parent) return failure(document, ['Layout operation requires a container']);
    const change = createLayoutOperationChange(mode, operation, parent.layout.flexDirection === 'column' ? 'column' : 'row');
    return change ? success({ ...document, elements: { ...document.elements, [parent.id]: { ...parent, layout: { ...parent.layout, ...change } } } }) : failure(document, [`Operation ${operation} is not supported by ${mode} layout`]);
  });
}

function setNodePath(node: DesignerDocumentNode, path: string, value: unknown): DesignerDocumentNode {
  const [scope, ...parts] = path.split('.').filter(Boolean);
  if (!scope || parts.length === 0) return node;
  const current = node[scope as keyof DesignerDocumentNode];
  const source = isRecord(current) ? current : {};
  return { ...node, [scope]: setRecordPath(source, parts, value) };
}

function setRecordPath(source: Record<string, unknown>, parts: readonly string[], value: unknown): Record<string, unknown> {
  const [head, ...tail] = parts;
  if (!head) return source;
  return { ...source, [head]: tail.length > 0 ? setRecordPath(isRecord(source[head]) ? source[head] : {}, tail, value) : value };
}

export function createBatchPatchCommand(patches: Record<string, Partial<DesignerDocumentNode>>, mergeKey?: string): DesignerCommand {
  return createDocumentCommand('BatchPatch', 'Patch multiple nodes', (document) => {
    const elements = { ...document.elements };
    for (const [nodeId, patch] of Object.entries(patches)) {
      const node = elements[nodeId];
      if (!node) return failure(document, [`Node not found: ${nodeId}`]);
      elements[nodeId] = { ...node, ...patch };
    }
    return success({ ...document, elements });
  }, mergeKey);
}

export function createInsertNodesCommand(nodes: DesignerDocumentNode[], insertionIndex?: number): DesignerCommand {
  return createDocumentCommand('InsertNodes', 'Insert nodes', (document) => {
    const elements = { ...document.elements };
    for (const node of nodes) {
      if (elements[node.id]) return failure(document, [`Duplicate node: ${node.id}`]);
      if (node.parentId && !elements[node.parentId] && !nodes.some((candidate) => candidate.id === node.parentId)) return failure(document, [`Parent not found: ${node.parentId}`]);
      const parent = node.parentId ? elements[node.parentId] : undefined;
      elements[node.id] = { ...node, children: [...node.children], layout: adaptLayoutToParentMode(node.layout, resolveLayoutMode(parent?.layout ?? {}), parent?.layout) };
    }
    const childErrors = reparentDeclaredChildren(elements, nodes.map((node) => node.id));
    if (childErrors.length > 0) return failure(document, childErrors);
    const attached = attachNodesToParents(elements, nodes.map((node) => node.id), insertionIndex);
    if (attached.length > 0) return failure(document, attached);
    return success({ ...document, elements });
  });
}

export function createDeleteNodesCommand(nodeIds: string[]): DesignerCommand {
  return createDocumentCommand('DeleteNodes', 'Delete nodes', (document) => {
    const elements = { ...document.elements };
    const deletedIds = new Set<string>();
    for (const nodeId of nodeIds) {
      if (!elements[nodeId]) return failure(document, [`Node not found: ${nodeId}`]);
      if (document.pages.some((page) => page.rootElementId === nodeId)) return failure(document, [`Cannot delete page root: ${nodeId}`]);
      for (const descendantId of collectSubtree(elements, nodeId)) deletedIds.add(descendantId);
    }
    for (const deletedId of deletedIds) {
      const parentId = elements[deletedId]?.parentId;
      if (parentId && elements[parentId]) elements[parentId] = { ...elements[parentId], children: elements[parentId].children.filter((childId) => childId !== deletedId) };
    }
    for (const deletedId of deletedIds) {
      delete elements[deletedId];
    }
    for (const node of Object.values(elements)) {
      elements[node.id] = {
        ...node,
        children: node.children.filter((childId) => !deletedIds.has(childId))
      };
    }
    return success({ ...document, elements });
  });
}

export function createMoveNodesCommand(nodeIdsOrRequest: string[] | MoveNodesRequest, parentId?: string | null, insertionIndex?: number): DesignerCommand {
  const request: MoveNodesRequest = Array.isArray(nodeIdsOrRequest)
    ? { insertionIndex, nodeIds: nodeIdsOrRequest, parentId: parentId ?? null }
    : nodeIdsOrRequest;
  return createDocumentCommand('MoveNodes', 'Move nodes', (document) => {
    if (request.parentId && !document.elements[request.parentId]) return failure(document, [`Parent not found: ${request.parentId}`]);
    const elements = { ...document.elements };
    const movingIds = unique(request.nodeIds);
    const moving = new Set(movingIds);
    const sourceParentIds = new Set(movingIds.map((nodeId) => elements[nodeId]?.parentId).filter((parentId): parentId is string => Boolean(parentId)));
    for (const nodeId of movingIds) {
      const node = elements[nodeId];
      if (!node) return failure(document, [`Node not found: ${nodeId}`]);
      if (node.locked === true || node.layout.locked === true || node.props.locked === true) return failure(document, [`Cannot move locked node: ${nodeId}`]);
      if (document.pages.some((page) => page.rootElementId === nodeId)) return failure(document, [`Cannot move page root: ${nodeId}`]);
      if (request.parentId === nodeId || (request.parentId && collectSubtree(elements, nodeId).includes(request.parentId))) return failure(document, [`Move would create a cycle: ${nodeId} -> ${request.parentId}`]);
      if (request.parentId && moving.has(request.parentId)) return failure(document, [`Move target is also being moved: ${request.parentId}`]);
      if (hasSelectedAncestor(elements, nodeId, moving)) return failure(document, ['Move selection must contain only root nodes']);
    }
    const target = request.parentId ? elements[request.parentId] : undefined;
    if (target && (target.locked === true || target.layout.locked === true || target.props.locked === true)) return failure(document, [`Cannot move into locked node: ${request.parentId}`]);
    const targetMode = request.targetLayoutMode ?? resolveLayoutMode(target?.layout ?? {});
    const baseTargetMode = resolveLayoutMode(target?.layout ?? {});
    const layoutPatchIds = Object.keys(request.layoutPatches ?? {});
    if (layoutPatchIds.some((nodeId) => !moving.has(nodeId))) return failure(document, ['Layout patch contains a node that is not being moved']);
    const responsiveBreakpoint = request.breakpointId?.trim();
    const responsiveGeometryOnly = Boolean(responsiveBreakpoint)
      && movingIds.every((nodeId) => elements[nodeId]?.parentId === request.parentId && request.layoutPatches?.[nodeId] !== undefined);
    for (const nodeId of movingIds) {
      const node = elements[nodeId];
      if (!responsiveGeometryOnly && node.parentId && elements[node.parentId]) elements[node.parentId] = { ...elements[node.parentId], children: elements[node.parentId].children.filter((childId) => childId !== nodeId) };
      const layoutPatch = request.layoutPatches?.[nodeId];
      if (responsiveBreakpoint && layoutPatch) {
        const responsiveLayoutPatch = targetMode === 'free' ? createFreeResponsivePatch(node.layout, layoutPatch, target?.layout) : layoutPatch;
        const normalized = normalizeResponsiveOverrideMap(node.responsiveOverrides).overrides;
        const nextOverride = mergeResponsiveOverride(normalized[responsiveBreakpoint], { layout: responsiveLayoutPatch });
        const responsiveOverrides = { ...normalized };
        if (nextOverride) responsiveOverrides[responsiveBreakpoint] = nextOverride;
        else delete responsiveOverrides[responsiveBreakpoint];
        elements[nodeId] = {
          ...node,
          layout: node.parentId === request.parentId ? node.layout : adaptLayoutToParentMode(node.layout, baseTargetMode, target?.layout),
          parentId: request.parentId,
          responsiveOverrides
        };
      } else {
        const mergedLayout = cleanLayout({ ...node.layout, ...(layoutPatch ?? {}) });
        const keepExistingLayout = node.parentId === request.parentId && layoutPatch === undefined && targetMode === baseTargetMode;
        elements[nodeId] = { ...node, parentId: request.parentId, layout: keepExistingLayout ? node.layout : adaptLayoutToParentMode(mergedLayout, targetMode, target?.layout) };
      }
    }
    if (request.parentId && !responsiveGeometryOnly) elements[request.parentId] = { ...elements[request.parentId], children: insertChildren(elements[request.parentId].children.filter((childId) => !moving.has(childId)), movingIds, request.insertionIndex) };
    const targetParentIds = new Set(sourceParentIds);
    if (request.parentId) targetParentIds.add(request.parentId);
    for (const parentId of targetParentIds) syncFlexChildOrders(elements, parentId);
    return success({ ...document, elements });
  });
}

function syncFlexChildOrders(elements: Record<string, DesignerDocumentNode>, parentId: string): void {
  const parent = elements[parentId];
  if (!parent || resolveLayoutMode(parent.layout) !== 'flex') return;
  parent.children.forEach((childId, index) => {
    const child = elements[childId];
    if (!child) return;
    const protocol = readCanonicalProtocol(child.layout);
    const layout = protocol?.placement.kind === 'flex-item'
      ? { ...child.layout, placement: { ...protocol.placement, flexItem: { ...defaultPlacement('flex').flexItem!, ...protocol.placement.flexItem, order: index } } }
      : (() => {
        const migrated = createLayoutMigrationSnapshot(child, 'flex', index, parent.children.map((id) => elements[id]).filter((item): item is DesignerDocumentNode => Boolean(item)), parent.layout).protocol;
        return canonicalLayout(migrated.container, { kind: 'flex-item', flexItem: { ...defaultPlacement('flex').flexItem!, ...migrated.placement.flexItem, order: index } }, migrated.size);
      })();
    elements[childId] = { ...child, layout };
  });
}

export function createDuplicateSubtreeCommand(nodeId: string, newRootId: string): DesignerCommand {
  return createDocumentCommand('DuplicateSubtree', 'Duplicate subtree', (document) => {
    const source = document.elements[nodeId];
    if (!source || document.elements[newRootId]) return failure(document, ['Source or destination node is invalid']);
    const descendants = collectSubtree(document.elements, nodeId);
    const idMap = new Map(descendants.map((id, index) => [id, index === 0 ? newRootId : `${newRootId}_${index}`]));
    const elements = { ...document.elements };
    const sourceParentId = source.parentId ?? source.id;
    if (!elements[sourceParentId]) return failure(document, [`Duplicate destination parent not found: ${sourceParentId}`]);
    for (const sourceId of descendants) {
      const node = document.elements[sourceId];
      const id = idMap.get(sourceId)!;
      elements[id] = { ...node, id, parentId: sourceId === nodeId ? sourceParentId : idMap.get(node.parentId ?? '') ?? null, children: node.children.map((childId) => idMap.get(childId)!).filter(Boolean) };
    }
    elements[sourceParentId] = { ...elements[sourceParentId], children: [...elements[sourceParentId].children, newRootId] };
    return success({ ...document, elements });
  });
}

function createNodeCommand(
  id: string,
  label: string,
  nodeId: string,
  transform: (document: DesignerDocument, node: DesignerDocumentNode) => DesignerDocument,
  mergeKey?: string
): DesignerCommand {
  return createDocumentCommand(id, label, (document) => {
    const node = document.elements[nodeId];
    return node ? success(transform(document, node)) : failure(document, [`Node not found: ${nodeId}`]);
  }, mergeKey);
}

function createDocumentCommand(id: string, label: string, transform: (document: DesignerDocument) => DesignerCommandResult, mergeKey?: string): DesignerCommand {
  return {
    id,
    label,
    mergeKey,
    execute: (context) => {
      const result = transform(context.document);
      return result.changed
        ? { ...success(result.document), inverse: createInverseDesignerCommand(context.document, result.document, id, label) }
        : result;
    },
  };
}

function success(document: DesignerDocument): DesignerCommandResult {
  return { changed: true, diagnostics: [], document };
}

function failure(document: DesignerDocument, diagnostics: string[]): DesignerCommandResult {
  return { changed: false, diagnostics, document };
}

function collectSubtree(elements: Record<string, DesignerDocumentNode>, rootId: string, visited = new Set<string>()): string[] {
  if (visited.has(rootId)) return [];
  visited.add(rootId);
  const node = elements[rootId];
  return node ? [rootId, ...node.children.flatMap((childId) => collectSubtree(elements, childId, visited))] : [];
}

function hasSelectedAncestor(elements: Record<string, DesignerDocumentNode>, nodeId: string, selected: ReadonlySet<string>): boolean {
  let parentId = elements[nodeId]?.parentId ?? null;
  const visited = new Set<string>();
  while (parentId) {
    if (selected.has(parentId)) return true;
    if (visited.has(parentId)) return true;
    visited.add(parentId);
    parentId = elements[parentId]?.parentId ?? null;
  }
  return false;
}

function unique(values: readonly string[]): string[] { return [...new Set(values)]; }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
function number(value: unknown, fallback = 0): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }

function attachNodesToParents(elements: Record<string, DesignerDocumentNode>, nodeIds: readonly string[], insertionIndex?: number): string[] {
  const errors: string[] = [];
  for (const nodeId of nodeIds) {
    const node = elements[nodeId];
    if (!node?.parentId) continue;
    const parent = elements[node.parentId];
    if (!parent) { errors.push(`Parent not found: ${node.parentId}`); continue; }
    if (!parent.children.includes(nodeId)) elements[node.parentId] = { ...parent, children: insertChildren(parent.children, [nodeId], insertionIndex) };
  }
  return errors;
}

function insertChildren(existing: readonly string[], additions: readonly string[], insertionIndex?: number): string[] {
  const next = [...existing];
  const index = insertionIndex === undefined ? next.length : Math.max(0, Math.min(Math.floor(insertionIndex), next.length));
  next.splice(index, 0, ...additions.filter((id) => !next.includes(id)));
  return next;
}

function adaptLayoutToParentMode(layout: DesignerDocumentNode['layout'], mode: ReturnType<typeof resolveLayoutMode>, parentLayout?: DesignerDocumentNode['layout']): DesignerDocumentNode['layout'] {
  if (mode === 'free') return normalizeFreeLayout(layout, parentLayout);
  if (mode === 'constraints') {
    const next = cleanLayout({ ...layout, constraints: isRecord(layout.constraints) ? layout.constraints : { left: number(layout.x), top: number(layout.y) } });
    delete next.position;
    delete next.x;
    delete next.y;
    return next;
  }
  if (mode === 'flex' || mode === 'grid') {
    const withoutPosition = { ...layout };
    delete withoutPosition.position;
    delete withoutPosition.x;
    delete withoutPosition.y;
    delete withoutPosition.constraints;
    return withoutPosition;
  }
  return cleanLayout(layout);
}

function cleanLayout(layout: DesignerDocumentNode['layout']): DesignerDocumentNode['layout'] {
  return Object.fromEntries(Object.entries(layout).filter(([, value]) => value !== undefined));
}

function normalizeFreeLayout(layout: DesignerDocumentNode['layout'], parentLayout?: DesignerDocumentNode['layout']): DesignerDocumentNode['layout'] {
  const canonical = readCanonicalProtocol(layout);
  const parentWidth = positiveNumber(layoutDimension(parentLayout ?? {}, 'width'), Number.POSITIVE_INFINITY);
  const parentHeight = positiveNumber(layoutDimension(parentLayout ?? {}, 'height'), Number.POSITIVE_INFINITY);
  const width = boundedDimension(layoutDimension(layout, 'width'), 160, parentWidth);
  const height = boundedDimension(layoutDimension(layout, 'height'), 48, parentHeight);
  const x = clamp(number(layout.x ?? canonical?.placement.absolute?.x), 0, finiteNumber(parentWidth) === undefined ? Number.POSITIVE_INFINITY : Math.max(0, parentWidth - width));
  const y = clamp(number(layout.y ?? canonical?.placement.absolute?.y), 0, finiteNumber(parentHeight) === undefined ? Number.POSITIVE_INFINITY : Math.max(0, parentHeight - height));
  if (canonical || readCanonicalProtocol(parentLayout ?? {})) {
    const placement = defaultPlacement('free', x, y);
    if (canonical?.placement.absolute?.zIndex !== undefined) placement.absolute!.zIndex = canonical.placement.absolute.zIndex;
    return canonicalLayout(defaultContainerLayout('free'), placement, { ...(canonical?.size ?? {}), height, width });
  }
  const next = cleanLayoutFields(layout, CHILD_PLACEMENT_FIELDS);
  return cleanLayout({ ...next, height, position: 'absolute', width, x, y });
}

function boundedDimension(value: unknown, fallback: number, parentSize: number): number {
  const candidate = positiveNumber(value, fallback);
  return Number.isFinite(parentSize) ? Math.min(candidate, parentSize) : candidate;
}

function diffFreeGeometry(before: DesignerDocumentNode['layout'], after: DesignerDocumentNode['layout']): DesignerDocumentNode['layout'] {
  const patch: DesignerDocumentNode['layout'] = {};
  const beforeGeometry = readLayoutGeometry(before);
  const afterGeometry = readLayoutGeometry(after);
  for (const field of ['height', 'width', 'x', 'y'] as const) {
    if (beforeGeometry[field] !== afterGeometry[field]) patch[field] = afterGeometry[field];
  }
  return patch;
}

function readLayoutGeometry(layout: DesignerDocumentNode['layout']): LayoutGeometry {
  const canonical = readCanonicalProtocol(layout);
  return {
    height: number(canonical?.size.height ?? layout.height, 48),
    width: number(canonical?.size.width ?? layout.width, 160),
    x: number(canonical?.placement.absolute?.x ?? layout.x),
    y: number(canonical?.placement.absolute?.y ?? layout.y)
  };
}

function createFreeResponsivePatch(base: DesignerDocumentNode['layout'], patch: DesignerDocumentNode['layout'], parentLayout?: DesignerDocumentNode['layout']): DesignerDocumentNode['layout'] {
  const normalized = normalizeFreeLayout({ ...base, ...patch }, parentLayout);
  const result: DesignerDocumentNode['layout'] = {};
  for (const field of ['height', 'width', 'x', 'y'] as const) if (field in patch) result[field] = number(normalized[field]);
  if ('position' in patch) result.position = patch.position;
  return cleanLayout(result);
}

function mergeNodeResponsiveLayout(node: DesignerDocumentNode, breakpointId: string, patch: DesignerDocumentNode['layout']): DesignerDocumentNode {
  const normalized = normalizeResponsiveOverrideMap(node.responsiveOverrides).overrides;
  const nextOverride = mergeResponsiveOverride(normalized[breakpointId], { layout: patch });
  const responsiveOverrides = { ...normalized };
  if (nextOverride) responsiveOverrides[breakpointId] = nextOverride;
  else delete responsiveOverrides[breakpointId];
  return { ...node, responsiveOverrides: responsiveOverrides as Record<string, Record<string, unknown>> };
}

function reparentDeclaredChildren(elements: Record<string, DesignerDocumentNode>, nodeIds: readonly string[]): string[] {
  const errors: string[] = [];
  for (const nodeId of nodeIds) {
    const node = elements[nodeId];
    if (!node) continue;
    for (const childId of node.children) {
      const child = elements[childId];
      if (!child) { errors.push(`Child not found: ${childId}`); continue; }
      if (child.parentId && elements[child.parentId]) elements[child.parentId] = { ...elements[child.parentId], children: elements[child.parentId].children.filter((id) => id !== childId) };
      elements[childId] = { ...child, parentId: nodeId };
    }
  }
  return errors;
}

export function createApplyWorkflowBindingCommand(page: WorkflowPageContext, draft: WorkflowBindingDraft): DesignerCommand {
  return {
    id: 'ApplyWorkflowBinding',
    label: 'Apply workflow binding',
    execute: (context) => {
      const result = applyWorkflowBindingDraft(context.document, page, draft);
      if (result.errors.length > 0) return failure(context.document, result.errors);
      return { ...success(result.document), inverse: createInverseDesignerCommand(context.document, result.document, 'ApplyWorkflowBinding', 'Apply workflow binding') };
    }
  };
}

export type { TypedValue };
