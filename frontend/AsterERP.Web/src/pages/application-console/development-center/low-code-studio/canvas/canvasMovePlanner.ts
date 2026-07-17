import type { MoveNodesRequest } from '../commands/moveNodesContract';
import type { ComponentRegistry } from '../components/ComponentRegistry';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';
import { planGridPlacements, resolveGridTrackCounts, type GridCellPlacement } from '../layout/gridLayout';
import { resolveLayoutMode, type LayoutMode } from '../layout/layoutOperations';

import type { CanvasPoint, CanvasRect } from './coordinateSystem';

export type CanvasDropPlacement = 'after' | 'before' | 'free-position' | 'inside';

export interface CanvasDropTarget {
  index: number;
  parentId: string;
  placement: CanvasDropPlacement;
  targetNodeId: string;
}

export interface CanvasMoveRequest {
  breakpointId?: string | null;
  document: DesignerDocument;
  geometry: CanvasMoveGeometry;
  nodeIds: readonly string[];
  rects: readonly CanvasRect[];
  target: CanvasDropTarget;
}

export type CanvasMovePlan = MoveNodesRequest & { target: CanvasDropTarget };

export type CanvasMoveResult =
  | { changed: boolean; diagnostics: readonly []; ok: true; plan: CanvasMovePlan }
  | { changed: false; diagnostics: readonly string[]; ok: false };

export interface CanvasMoveGeometry {
  layoutModes: Readonly<Record<string, LayoutMode>>;
  nodes: Readonly<Record<string, DesignerDocumentNode>>;
  rects: Readonly<Record<string, CanvasRect>>;
}

interface ResolveCanvasMoveTargetInput {
  clientX: number;
  clientY: number;
  document: DesignerDocument;
  hitElement: Element | null;
  manifests: ComponentRegistry;
  movingNodeIds: readonly string[];
  nodeElements: ReadonlyMap<string, HTMLElement>;
  rootElement?: HTMLElement | null;
  rootId: string;
}

export function resolveCanvasMoveTarget(input: ResolveCanvasMoveTargetInput): CanvasDropTarget | null {
  const moving = new Set(input.movingNodeIds);
  const hitArtboard = input.hitElement?.closest<HTMLElement>('[data-canvas-artboard="true"]');
  const artboard = hitArtboard ?? (input.rootElement && containsClientPoint(input.rootElement, input.clientX, input.clientY) ? input.rootElement : null);
  const hitNodeId = input.hitElement?.closest<HTMLElement>('[data-node-id]')?.dataset.nodeId;
  const targetId = hitNodeId ?? (artboard ? input.rootId : null);
  if (!targetId) return null;
  if (moving.has(targetId) || input.movingNodeIds.some((nodeId) => isDescendant(input.document.elements, nodeId, targetId))) return null;

  const target = input.document.elements[targetId];
  if (!target || isLocked(target)) return null;
  const targetAcceptsChildren = input.manifests.get(target.type)?.capability.acceptsChildren === true;
  const targetMode = resolveLayoutMode(target.layout);
  if (targetAcceptsChildren && (targetId === input.rootId || targetMode === 'free' || targetMode === 'constraints')) {
    return {
      index: target.children.filter((childId) => !moving.has(childId)).length,
      parentId: target.id,
      placement: 'free-position',
      targetNodeId: target.id
    };
  }
  const targetElement = input.nodeElements.get(target.id) ?? artboard;
  const placement = targetElement
    ? resolvePlacement(input.clientX, input.clientY, targetElement.getBoundingClientRect(), targetAcceptsChildren, target.parentId ? input.document.elements[target.parentId]?.layout : undefined)
    : 'inside';

  if (targetAcceptsChildren && placement === 'inside') {
    const mode = resolveLayoutMode(target.layout);
    return {
      index: target.children.filter((childId) => !moving.has(childId)).length,
      parentId: target.id,
      placement: mode === 'free' || mode === 'constraints' ? 'free-position' : 'inside',
      targetNodeId: target.id
    };
  }

  const parentId = target.parentId;
  const parent = parentId ? input.document.elements[parentId] : undefined;
  if (!parentId || !parent || isLocked(parent) || input.manifests.get(parent.type)?.capability.acceptsChildren !== true) return null;
  const parentMode = resolveLayoutMode(parent.layout);
  if (parentMode === 'free' || parentMode === 'constraints') {
    const sourceIndices = input.movingNodeIds
      .filter((nodeId) => input.document.elements[nodeId]?.parentId === parentId)
      .map((nodeId) => parent.children.indexOf(nodeId))
      .filter((index) => index >= 0);
    return {
      index: sourceIndices.length > 0 ? Math.min(...sourceIndices) : parent.children.length,
      parentId,
      placement: 'free-position',
      targetNodeId: parentId
    };
  }
  const remainingChildren = parent.children.filter((childId) => !moving.has(childId));
  const index = remainingChildren.indexOf(target.id);
  if (index < 0) return null;
  if (parentMode === 'flex' && isFlexWrapped(parent.layout)) {
    const flexIndex = resolveFlexInsertionIndex(input.clientX, input.clientY, remainingChildren, input.nodeElements, parent.layout);
    if (flexIndex !== null) return { index: flexIndex, parentId, placement: flexIndex <= index ? 'before' : 'after', targetNodeId: target.id };
  }
  return {
    index: index + (placement === 'after' ? 1 : 0),
    parentId,
    placement: placement === 'inside' ? 'after' : placement,
    targetNodeId: target.id
  };
}

function containsClientPoint(element: HTMLElement, clientX: number, clientY: number): boolean {
  const rect = element.getBoundingClientRect();
  return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom;
}

export function planCanvasMove(request: CanvasMoveRequest): CanvasMoveResult {
  const movingIds = orderMovingNodeIds(request.document, [...new Set(request.nodeIds)]);
  if (movingIds.length === 0) return { changed: false, diagnostics: ['Move requires at least one node'], ok: false };
  const targetParent = request.document.elements[request.target.parentId];
  if (!targetParent) return { changed: false, diagnostics: [`Parent not found: ${request.target.parentId}`], ok: false };
  if (isLocked(targetParent)) return { changed: false, diagnostics: [`Cannot move into locked node: ${targetParent.id}`], ok: false };

  const targetLayoutMode = request.geometry.layoutModes[targetParent.id] ?? resolveLayoutMode(targetParent.layout);
  const rects = new Map(request.rects.filter((rect) => rect.id).map((rect) => [rect.id!, rect]));
  const parentRect = request.geometry.rects[targetParent.id] ?? { height: 0, width: 0, x: 0, y: 0 };
  const layoutPatches: Record<string, DesignerDocumentNode['layout']> = {};
  const gridPlacements = targetLayoutMode === 'grid'
    ? planGridPlacements(
      targetParent.children.map((childId) => ({ id: childId, layout: request.document.elements[childId]?.layout ?? {} })),
      movingIds.map((nodeId) => ({ id: nodeId, layout: (request.geometry.nodes[nodeId] ?? request.document.elements[nodeId])?.layout ?? {} })),
      request.target.index,
      resolveGridTrackCounts(targetParent.layout)
    )
    : null;
  if (gridPlacements && !gridPlacements.ok) return { changed: false, diagnostics: [gridPlacements.diagnostic], ok: false };

  for (const nodeId of movingIds) {
    const node = request.geometry.nodes[nodeId] ?? request.document.elements[nodeId];
    const rect = rects.get(nodeId);
    if (!node || !rect) return { changed: false, diagnostics: [`Move geometry not found: ${nodeId}`], ok: false };
    if (isLocked(node)) return { changed: false, diagnostics: [`Cannot move locked node: ${nodeId}`], ok: false };
    if (nodeId === targetParent.id || isDescendant(request.document.elements, nodeId, targetParent.id)) {
      return { changed: false, diagnostics: [`Move would create a cycle: ${nodeId} -> ${targetParent.id}`], ok: false };
    }
    layoutPatches[nodeId] = createTargetLayoutPatch(node, rect, parentRect, targetLayoutMode, gridPlacements?.ok ? gridPlacements.placements.get(nodeId) : undefined);
  }

  return {
    changed: !isNoOpMove(request, movingIds),
    diagnostics: [],
    ok: true,
    plan: {
      breakpointId: request.breakpointId,
      insertionIndex: request.target.index,
      layoutPatches,
      nodeIds: movingIds,
      parentId: request.target.parentId,
      target: request.target,
      targetLayoutMode
    }
  };
}

function isNoOpMove(request: CanvasMoveRequest, movingIds: readonly string[]): boolean {
  if (!movingIds.every((nodeId) => request.document.elements[nodeId]?.parentId === request.target.parentId)) return false;
  const parent = request.document.elements[request.target.parentId];
  if (!parent) return false;
  const moving = new Set(movingIds);
  const remaining = parent.children.filter((childId) => !moving.has(childId));
  const index = clampIndex(request.target.index, remaining.length);
  const reordered = [...remaining.slice(0, index), ...movingIds, ...remaining.slice(index)];
  if (!sameArray(reordered, parent.children)) return false;
  const mode = request.geometry.layoutModes[parent.id] ?? resolveLayoutMode(parent.layout);
  if (mode === 'flex' || mode === 'grid') return true;
  const rects = new Map(request.rects.filter((rect) => rect.id).map((rect) => [rect.id!, rect]));
  return movingIds.every((nodeId) => {
    const before = request.geometry.rects[nodeId];
    const after = rects.get(nodeId);
    return Boolean(before && after && nearlyEqual(before.x, after.x) && nearlyEqual(before.y, after.y));
  });
}

function orderMovingNodeIds(document: DesignerDocument, nodeIds: readonly string[]): string[] {
  const requested = new Set(nodeIds);
  const ordered: string[] = [];
  const visited = new Set<string>();
  const visit = (nodeId: string) => {
    if (visited.has(nodeId)) return;
    visited.add(nodeId);
    if (requested.has(nodeId)) ordered.push(nodeId);
    document.elements[nodeId]?.children.forEach(visit);
  };
  document.pages.forEach((page) => visit(page.rootElementId));
  nodeIds.forEach((nodeId) => { if (!visited.has(nodeId) && document.elements[nodeId]) ordered.push(nodeId); });
  return ordered;
}

function createTargetLayoutPatch(node: DesignerDocumentNode, rect: CanvasRect, parent: CanvasRect, mode: LayoutMode, gridPlacement?: GridCellPlacement): DesignerDocumentNode['layout'] {
  switch (mode) {
    case 'grid':
      if (!gridPlacement) return { constraints: undefined, position: undefined, x: undefined, y: undefined };
      return {
        constraints: undefined,
        gridColumn: gridPlacement.column,
        gridColumnSpan: gridPlacement.columnSpan,
        gridRow: gridPlacement.row,
        gridRowSpan: gridPlacement.rowSpan,
        position: undefined,
        x: undefined,
        y: undefined
      };
    case 'flex':
      return { constraints: undefined, position: undefined, x: undefined, y: undefined };
    case 'free':
      break;
    case 'constraints':
      break;
  }
  const local = {
    height: rect.height,
    width: rect.width,
    x: round(rect.x - parent.x),
    y: round(rect.y - parent.y)
  };
  if (mode === 'constraints') {
    return {
      constraints: createConstraintPatch(node.layout.constraints, parent, local),
      position: undefined,
      x: undefined,
      y: undefined
    };
  }
  return { constraints: undefined, position: 'absolute', x: local.x, y: local.y };
}

function createConstraintPatch(currentValue: unknown, parent: CanvasRect, local: CanvasRect): Record<string, unknown> {
  const current = isRecord(currentValue) ? currentValue : {};
  const horizontalAnchors = ['left', 'right', 'centerX'].filter((anchor) => anchor in current);
  const verticalAnchors = ['top', 'bottom', 'centerY'].filter((anchor) => anchor in current);
  const active = [
    ...(horizontalAnchors.length > 0 ? horizontalAnchors : ['left']),
    ...(verticalAnchors.length > 0 ? verticalAnchors : ['top'])
  ];
  const values: Record<string, number> = {
    bottom: round(parent.height - local.y - local.height),
    centerX: round(local.x + local.width / 2 - parent.width / 2),
    centerY: round(local.y + local.height / 2 - parent.height / 2),
    left: local.x,
    right: round(parent.width - local.x - local.width),
    top: local.y
  };
  return { ...current, ...Object.fromEntries(active.map((anchor) => [anchor, values[anchor]])) };
}

function resolvePlacement(clientX: number, clientY: number, rect: Pick<DOMRect, 'height' | 'left' | 'top' | 'width'>, acceptsChildren: boolean, parentLayout?: Record<string, unknown>): 'after' | 'before' | 'inside' {
  const parentMode = resolveLayoutMode(parentLayout ?? {});
  const horizontal = parentMode === 'grid' || (parentMode === 'flex' && parentLayout?.flexDirection !== 'column');
  const coordinate = horizontal ? clientX - rect.left : clientY - rect.top;
  const size = horizontal ? rect.width : rect.height;
  if (!Number.isFinite(size) || size <= 0) return acceptsChildren ? 'inside' : 'after';
  if (acceptsChildren && coordinate >= size * 0.3 && coordinate <= size * 0.7) return 'inside';
  return coordinate < size / 2 ? 'before' : 'after';
}

function isFlexWrapped(layout: Record<string, unknown>): boolean {
  return layout.flexWrap === 'wrap' || layout.flexWrap === 'wrap-reverse' || layout.wrap === 'wrap' || layout.wrap === 'wrap-reverse';
}

function resolveFlexInsertionIndex(clientX: number, clientY: number, childIds: readonly string[], nodeElements: ReadonlyMap<string, HTMLElement>, layout: Record<string, unknown>): number | null {
  const items = childIds.map((id, index) => ({ id, index, rect: nodeElements.get(id)?.getBoundingClientRect() })).filter((item): item is { id: string; index: number; rect: DOMRect } => Boolean(item.rect));
  if (items.length === 0) return null;
  const column = layout.flexDirection === 'column';
  const crossPoint = column ? clientX : clientY;
  const mainPoint = column ? clientY : clientX;
  const crossStart = (item: { rect: DOMRect }) => column ? item.rect.left : item.rect.top;
  const crossSize = (item: { rect: DOMRect }) => column ? item.rect.width : item.rect.height;
  const mainStart = (item: { rect: DOMRect }) => column ? item.rect.top : item.rect.left;
  const mainSize = (item: { rect: DOMRect }) => column ? item.rect.height : item.rect.width;
  const lines: Array<typeof items> = [];
  for (const item of [...items].sort((left, right) => crossStart(left) - crossStart(right) || left.index - right.index)) {
    const line = lines.find((candidate) => Math.abs(crossStart(candidate[0]) - crossStart(item)) <= Math.max(4, Math.min(crossSize(candidate[0]), crossSize(item)) / 2));
    if (line) line.push(item); else lines.push([item]);
  }
  const line = lines.find((candidate) => crossPoint >= crossStart(candidate[0]) && crossPoint <= crossStart(candidate[0]) + crossSize(candidate[0]))
    ?? lines.reduce((closest, candidate) => Math.abs(crossPoint - (crossStart(candidate[0]) + crossSize(candidate[0]) / 2)) < Math.abs(crossPoint - (crossStart(closest[0]) + crossSize(closest[0]) / 2)) ? candidate : closest);
  const ordered = [...line].sort((left, right) => mainStart(left) - mainStart(right) || left.index - right.index);
  const before = ordered.find((item) => mainPoint < mainStart(item) + mainSize(item) / 2);
  return before ? before.index : ordered[ordered.length - 1].index + 1;
}

export function createFlexResizeLayoutPatch(layout: DesignerDocumentNode['layout'], width: number, height: number): DesignerDocumentNode['layout'] {
  const next: DesignerDocumentNode['layout'] = { ...layout, width, height };
  delete next.position;
  delete next.x;
  delete next.y;
  delete next.constraints;
  return next;
}

function isDescendant(elements: Readonly<Record<string, DesignerDocumentNode>>, ancestorId: string, candidateId: string): boolean {
  const visited = new Set<string>();
  let currentId: string | null = candidateId;
  while (currentId && !visited.has(currentId)) {
    if (currentId === ancestorId) return true;
    visited.add(currentId);
    currentId = elements[currentId]?.parentId ?? null;
  }
  return false;
}

function isLocked(node: DesignerDocumentNode): boolean {
  return node.locked === true || node.layout.locked === true || node.props.locked === true;
}

function round(value: number): number { return Math.round(value * 100) / 100; }
function clampIndex(value: number, length: number): number { return Number.isFinite(value) ? Math.max(0, Math.min(length, Math.trunc(value))) : length; }
function nearlyEqual(left: number, right: number): boolean { return Math.abs(left - right) < 0.01; }
function sameArray(left: readonly string[], right: readonly string[]): boolean { return left.length === right.length && left.every((value, index) => value === right[index]); }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }

export function worldPointFromViewportCenter(stage: { height: number; width: number }, viewport: { pan: CanvasPoint; zoom: number }): CanvasPoint {
  return {
    x: (stage.width / 2 - viewport.pan.x) / viewport.zoom,
    y: (stage.height / 2 - viewport.pan.y) / viewport.zoom
  };
}
