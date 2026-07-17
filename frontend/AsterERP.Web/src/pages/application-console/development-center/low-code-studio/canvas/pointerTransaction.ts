import type { CanvasPoint, CanvasRect } from './coordinateSystem';

export type PointerTransactionKind = 'move' | 'resize' | 'pan' | 'select';
export type ResizeHandle = 'north' | 'west' | 'east' | 'south' | 'northwest' | 'northeast' | 'southwest' | 'southeast';

export interface PointerSnapshot { id: string; rect: CanvasRect }
export interface PointerTransaction {
  pointerId: number;
  kind: PointerTransactionKind;
  handle?: ResizeHandle;
  start: CanvasPoint;
  snapshots: readonly PointerSnapshot[];
}

export interface PointerUpdate { delta: CanvasPoint; rects: CanvasRect[]; selection?: CanvasRect }

export interface PointerTransactionOptions {
  minWidth?: number;
  minHeight?: number;
}

export function beginPointerTransaction(kind: PointerTransactionKind, pointerId: number, start: CanvasPoint, snapshots: readonly PointerSnapshot[], handle?: ResizeHandle): PointerTransaction {
  return { pointerId, kind, start, snapshots: snapshots.map((snapshot) => ({ ...snapshot, rect: { ...snapshot.rect } })), ...(handle ? { handle } : {}) };
}

export function updatePointerTransaction(transaction: PointerTransaction, point: CanvasPoint, options: PointerTransactionOptions = {}): PointerUpdate {
  const delta = { x: point.x - transaction.start.x, y: point.y - transaction.start.y };
  if (transaction.kind === 'pan') return { delta, rects: [] };
  if (transaction.kind === 'select') return { delta, rects: [], selection: normalizeRect({ x: transaction.start.x, y: transaction.start.y, width: delta.x, height: delta.y }) };
  const rects = transaction.snapshots.map(({ id, rect }) => ({ id, ...(transaction.kind === 'resize' ? resizeRect(rect, delta, transaction.handle ?? 'southeast', options) : moveRect(rect, delta)) }));
  return { delta, rects };
}

export function finishPointerTransaction(transaction: PointerTransaction, point: CanvasPoint, options: PointerTransactionOptions = {}): PointerUpdate {
  return updatePointerTransaction(transaction, point, options);
}

function moveRect(rect: CanvasRect, delta: CanvasPoint): CanvasRect {
  return { ...rect, x: rect.x + delta.x, y: rect.y + delta.y };
}

function resizeRect(rect: CanvasRect, delta: CanvasPoint, handle: ResizeHandle, options: PointerTransactionOptions): CanvasRect {
  const minWidth = Math.max(1, options.minWidth ?? 1);
  const minHeight = Math.max(1, options.minHeight ?? 1);
  const affectsWest = handle.includes('west');
  const affectsNorth = handle.includes('north');
  const requestedWidth = rect.width + (affectsWest ? -delta.x : handle.includes('east') ? delta.x : 0);
  const requestedHeight = rect.height + (affectsNorth ? -delta.y : handle.includes('south') ? delta.y : 0);
  const width = Math.max(minWidth, requestedWidth);
  const height = Math.max(minHeight, requestedHeight);
  return {
    ...rect,
    x: affectsWest ? rect.x + rect.width - width : rect.x,
    y: affectsNorth ? rect.y + rect.height - height : rect.y,
    width,
    height
  };
}

function normalizeRect(rect: CanvasRect): CanvasRect {
  const x = rect.width < 0 ? rect.x + rect.width : rect.x;
  const y = rect.height < 0 ? rect.y + rect.height : rect.y;
  return { x, y, width: Math.abs(rect.width), height: Math.abs(rect.height) };
}
