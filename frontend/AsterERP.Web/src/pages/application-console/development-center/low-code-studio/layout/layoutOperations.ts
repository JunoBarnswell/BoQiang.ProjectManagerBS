export type LayoutMode = 'free' | 'flex' | 'grid' | 'constraints';
export type LayoutOperation = 'align-left' | 'align-center' | 'align-right' | 'align-top' | 'align-middle' | 'align-bottom' | 'distribute-horizontal' | 'distribute-vertical' | 'same-width' | 'same-height';
import { createConstraintAnchors } from './constraintModel';
export interface LayoutNode { id: string; x: number; y: number; width: number; height: number }
export interface LayoutBounds { height: number; width: number }
export type LayoutChange = Partial<Pick<LayoutNode, 'x' | 'y' | 'width' | 'height'>>;
export interface LayoutContainer { mode: LayoutMode; gap?: number; columns?: number; rows?: number; flexDirection?: 'row' | 'column'; align?: 'start' | 'center' | 'end' | 'stretch'; justify?: 'start' | 'center' | 'end' | 'space-between' }
export interface LayoutContainerChange { display: 'block' | 'flex' | 'grid' | 'constraints'; layoutMode: LayoutMode; flexDirection?: 'row' | 'column'; gap?: number; gridTemplateColumns?: string; gridTemplateRows?: string; alignItems?: string; justifyContent?: string; justifyItems?: string; constraints?: Record<string, unknown> }

export function resolveLayoutMode(layout: Record<string, unknown>): LayoutMode {
  const canonical = isRecord(layout.container) ? layout.container : isRecord(layout.protocol) && isRecord(layout.protocol.container) ? layout.protocol.container : isRecord(layout.layoutProtocol) && isRecord(layout.layoutProtocol.container) ? layout.layoutProtocol.container : undefined;
  if (canonical?.mode === 'flex' || canonical?.mode === 'grid' || canonical?.mode === 'constraints' || canonical?.mode === 'free') return canonical.mode;
  if (layout.layoutMode === 'flex' || layout.layoutMode === 'grid' || layout.layoutMode === 'constraints' || layout.layoutMode === 'free') return layout.layoutMode;
  if (layout.display === 'flex') return 'flex';
  if (layout.display === 'grid') return 'grid';
  if (layout.display === 'constraints') return 'constraints';
  return 'free';
}

export function resolveLayoutStyle(layout: Record<string, unknown>): Record<string, unknown> {
  const mode = resolveLayoutMode(layout);
  if (mode === 'flex') return {
    display: 'flex',
    flexDirection: layout.flexDirection === 'column' ? 'column' : 'row',
    gap: numeric(layout.gap),
    alignItems: layout.alignItems ?? 'flex-start',
    justifyContent: layout.justifyContent ?? 'flex-start',
    ...(isCssLayoutValue(layout.flex) ? { flex: layout.flex } : {}),
    ...(isCssLayoutValue(layout.flexGrow) ? { flexGrow: layout.flexGrow } : {}),
    ...(isCssLayoutValue(layout.flexShrink) ? { flexShrink: layout.flexShrink } : {}),
    ...(isCssLayoutValue(layout.flexBasis) ? { flexBasis: layout.flexBasis } : {})
  };
  if (mode === 'grid') return { display: 'grid', gap: numeric(layout.gap), gridTemplateColumns: typeof layout.gridTemplateColumns === 'string' ? layout.gridTemplateColumns : repeatTrack(numberValue(layout.columns, 1)), gridTemplateRows: typeof layout.gridTemplateRows === 'string' ? layout.gridTemplateRows : repeatTrack(numberValue(layout.rows, 1)) };
  if (mode === 'constraints') return { position: 'relative' };
  return {};
}

export function calculateLayoutChanges(nodes: readonly LayoutNode[], operation: LayoutOperation, mode: LayoutMode = 'free', bounds?: LayoutBounds): Map<string, LayoutChange> {
  if (nodes.length < 2 || mode !== 'free') return new Map();
  const xs = nodes.map((node) => node.x), ys = nodes.map((node) => node.y);
  const right = Math.max(...nodes.map((node) => node.x + node.width)), bottom = Math.max(...nodes.map((node) => node.y + node.height));
  const centerX = (Math.min(...xs) + right) / 2, centerY = (Math.min(...ys) + bottom) / 2;
  const changes = new Map<string, LayoutChange>();
  for (const node of nodes) {
    const change: LayoutChange = {};
    if (operation === 'align-left') change.x = Math.min(...xs);
    if (operation === 'align-center') change.x = Math.round(centerX - node.width / 2);
    if (operation === 'align-right') change.x = right - node.width;
    if (operation === 'align-top') change.y = Math.min(...ys);
    if (operation === 'align-middle') change.y = Math.round(centerY - node.height / 2);
    if (operation === 'align-bottom') change.y = bottom - node.height;
    if (operation === 'same-width') change.width = nodes[0].width;
    if (operation === 'same-height') change.height = nodes[0].height;
    changes.set(node.id, change);
  }
  if (operation === 'distribute-horizontal' || operation === 'distribute-vertical') distribute(nodes, changes, operation === 'distribute-horizontal' ? 'x' : 'y');
  if (mode === 'free' && bounds) constrainFreeChanges(nodes, changes, bounds);
  return changes;
}

function constrainFreeChanges(nodes: readonly LayoutNode[], changes: Map<string, LayoutChange>, bounds: LayoutBounds): void {
  const width = finitePositive(bounds.width);
  const height = finitePositive(bounds.height);
  if (!width && !height) return;
  for (const node of nodes) {
    const change = changes.get(node.id);
    if (!change) continue;
    const nextWidth = width ? clamp(node.width + (change.width ?? 0), 1, width) : node.width + (change.width ?? 0);
    const nextHeight = height ? clamp(node.height + (change.height ?? 0), 1, height) : node.height + (change.height ?? 0);
    if (change.width !== undefined) change.width = nextWidth - node.width;
    if (change.height !== undefined) change.height = nextHeight - node.height;
    if (change.x !== undefined && width) change.x = clamp(change.x, 0, Math.max(0, width - nextWidth));
    if (change.y !== undefined && height) change.y = clamp(change.y, 0, Math.max(0, height - nextHeight));
  }
}

export function isLayoutOperationSupported(mode: LayoutMode, operation: LayoutOperation): boolean {
  if (mode === 'free') return true;
  if (mode === 'constraints') return false;
  if (mode === 'grid') return operation.startsWith('align-');
  return operation.startsWith('align-') || operation === 'distribute-horizontal' || operation === 'distribute-vertical';
}

/**
 * Returns a container-level operation for modes whose children are not positioned
 * by x/y. Free layout uses calculateLayoutChanges; flex/grid must mutate their
 * own layout protocol instead of pretending alignment is a child geometry edit.
 */
export function createLayoutOperationChange(
  mode: Exclude<LayoutMode, 'free'>,
  operation: LayoutOperation,
  flexDirection: 'row' | 'column' = 'row'
): Partial<LayoutContainerChange> | null {
  if (mode === 'constraints') return null;
  if (mode === 'grid') {
    if (operation === 'align-left') return { justifyItems: 'start' };
    if (operation === 'align-center') return { justifyItems: 'center' };
    if (operation === 'align-right') return { justifyItems: 'end' };
    if (operation === 'align-top') return { alignItems: 'start' };
    if (operation === 'align-middle') return { alignItems: 'center' };
    if (operation === 'align-bottom') return { alignItems: 'end' };
    return null;
  }

  const horizontalMainAxis = flexDirection !== 'column';
  if (operation === 'distribute-horizontal' && horizontalMainAxis) return { justifyContent: 'space-between' };
  if (operation === 'distribute-vertical' && !horizontalMainAxis) return { justifyContent: 'space-between' };
  if (operation === 'align-left' && horizontalMainAxis) return { justifyContent: 'flex-start' };
  if (operation === 'align-center' && horizontalMainAxis) return { justifyContent: 'center' };
  if (operation === 'align-right' && horizontalMainAxis) return { justifyContent: 'flex-end' };
  if (operation === 'align-top' && horizontalMainAxis) return { alignItems: 'flex-start' };
  if (operation === 'align-middle' && horizontalMainAxis) return { alignItems: 'center' };
  if (operation === 'align-bottom' && horizontalMainAxis) return { alignItems: 'flex-end' };
  if (operation === 'align-top' && !horizontalMainAxis) return { justifyContent: 'flex-start' };
  if (operation === 'align-middle' && !horizontalMainAxis) return { justifyContent: 'center' };
  if (operation === 'align-bottom' && !horizontalMainAxis) return { justifyContent: 'flex-end' };
  if (operation === 'align-left' && !horizontalMainAxis) return { alignItems: 'flex-start' };
  if (operation === 'align-center' && !horizontalMainAxis) return { alignItems: 'center' };
  if (operation === 'align-right' && !horizontalMainAxis) return { alignItems: 'flex-end' };
  return null;
}

export function createLayoutContainerChange(container: LayoutContainer): LayoutContainerChange {
  const gap = Math.max(0, container.gap ?? 0);
  if (container.mode === 'flex') return { display: 'flex', layoutMode: 'flex', flexDirection: container.flexDirection ?? 'row', gap, alignItems: toCssAlignment(container.align), justifyContent: toCssJustify(container.justify) };
  if (container.mode === 'grid') return { display: 'grid', layoutMode: 'grid', gap, gridTemplateColumns: repeatTrack(container.columns), gridTemplateRows: repeatTrack(container.rows) };
  if (container.mode === 'constraints') return { display: 'block', layoutMode: 'constraints', constraints: { strategy: 'parent-anchored' } };
  return { display: 'block', layoutMode: 'free' };
}

export function createConstraintChange(node: LayoutNode, parent: LayoutNode, anchors?: readonly ('left' | 'right' | 'top' | 'bottom' | 'centerX' | 'centerY')[]): LayoutChange & { constraints: Record<string, unknown> } {
  const constraints: Record<string, unknown> = { ...createConstraintAnchors(node, parent, anchors) };
  return { constraints };
}

function distribute(nodes: readonly LayoutNode[], changes: Map<string, LayoutChange>, axis: 'x' | 'y'): void {
  const sorted = [...nodes].sort((a, b) => a[axis] - b[axis]);
  const end = sorted.at(-1)![axis], start = sorted[0][axis];
  const occupied = sorted.slice(0, -1).reduce((sum, node) => sum + (axis === 'x' ? node.width : node.height), 0);
  const gap = (end - start - occupied) / Math.max(1, sorted.length - 1);
  let cursor = start;
  for (const node of sorted) { changes.set(node.id, { [axis]: Math.round(cursor) }); cursor += (axis === 'x' ? node.width : node.height) + gap; }
}

function repeatTrack(count = 1): string { return `repeat(${Math.max(1, Math.floor(count))}, minmax(0, 1fr))`; }
function numeric(value: unknown): number | undefined { return typeof value === 'number' && Number.isFinite(value) ? Math.max(0, value) : undefined; }
function numberValue(value: unknown, fallback: number): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
function finitePositive(value: number): number | undefined { return Number.isFinite(value) && value > 0 ? value : undefined; }
function clamp(value: number, min: number, max: number): number { return Math.min(max, Math.max(min, value)); }
function toCssAlignment(value: LayoutContainer['align']): string { return value === 'center' ? 'center' : value === 'end' ? 'flex-end' : value === 'stretch' ? 'stretch' : 'flex-start'; }
function toCssJustify(value: LayoutContainer['justify']): string { return value === 'center' ? 'center' : value === 'end' ? 'flex-end' : value === 'space-between' ? 'space-between' : 'flex-start'; }
function isCssLayoutValue(value: unknown): boolean { return (typeof value === 'number' && Number.isFinite(value)) || (typeof value === 'string' && value.trim().length > 0); }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }
