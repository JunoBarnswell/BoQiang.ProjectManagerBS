export interface GridLayoutNode {
  id: string;
  layout: Readonly<Record<string, unknown>>;
}

export interface GridCellPlacement {
  row: number;
  column: number;
  rowSpan: number;
  columnSpan: number;
}

export interface GridTrackCounts {
  columns: number;
  rows: number;
}

export type GridPlacementPlan =
  | { ok: true; placements: ReadonlyMap<string, GridCellPlacement> }
  | { ok: false; diagnostic: string };

export function resolveGridTrackCounts(layout: Readonly<Record<string, unknown>>): GridTrackCounts {
  return {
    columns: resolveTrackCount(layout.columns, layout.gridTemplateColumns),
    rows: resolveTrackCount(layout.rows, layout.gridTemplateRows)
  };
}

export function resolveGridColumnCount(layout: Readonly<Record<string, unknown>>): number {
  return resolveGridTrackCounts(layout).columns;
}

export function planGridPlacements(
  existingChildren: readonly GridLayoutNode[],
  movingChildren: readonly GridLayoutNode[],
  insertionIndex: number,
  tracks: number | GridTrackCounts
): GridPlacementPlan {
  const trackCounts = typeof tracks === 'number' ? { columns: tracks, rows: 1 } : tracks;
  if (!isPositiveInteger(trackCounts.columns)) return { diagnostic: 'Grid columns must be a positive integer', ok: false };
  if (!isPositiveInteger(trackCounts.rows)) return { diagnostic: 'Grid rows must be a positive integer', ok: false };
  const movingIds = new Set(movingChildren.map((child) => child.id));
  const remaining = existingChildren.filter((child) => !movingIds.has(child.id));
  const index = clampIndex(insertionIndex, remaining.length);
  const ordered = [...remaining.slice(0, index), ...movingChildren, ...remaining.slice(index)];
  const occupied = new Set<string>();
  const placements = new Map<string, GridCellPlacement>();

  for (const child of ordered) {
    const span = readSpan(child.layout, child.id, trackCounts.columns);
    if (!span.ok) return span;
    const isMoving = movingIds.has(child.id);
    const start = isMoving ? { ok: true as const, row: undefined, column: undefined } : readStart(child.layout, child.id);
    if (!start.ok) return start;
    const placement = findAvailableCell(start.row, start.column, span.rowSpan, span.columnSpan, trackCounts.columns, trackCounts.rows, occupied, ordered.length);
    if (!placement) return { diagnostic: `Grid cell unavailable for ${child.id}`, ok: false };
    occupy(placement, occupied);
    placements.set(child.id, placement);
  }

  return { ok: true, placements };
}

function readSpan(layout: Readonly<Record<string, unknown>>, nodeId: string, columns: number): { ok: true; rowSpan: number; columnSpan: number } | { ok: false; diagnostic: string } {
  const rowSpan = readPositiveInteger(layout.gridRowSpan);
  const columnSpan = readPositiveInteger(layout.gridColumnSpan);
  if (rowSpan === null || columnSpan === null) return { diagnostic: `Invalid grid span for ${nodeId}`, ok: false };
  if (columnSpan > columns) return { diagnostic: `Grid column span exceeds columns for ${nodeId}`, ok: false };
  return { columnSpan, ok: true, rowSpan };
}

function readStart(layout: Readonly<Record<string, unknown>>, nodeId: string): { ok: true; row?: number; column?: number } | { ok: false; diagnostic: string } {
  const row = readGridLine(layout.gridRow);
  const column = readGridLine(layout.gridColumn);
  if (row === null || column === null) return { diagnostic: `Invalid grid row or column for ${nodeId}`, ok: false };
  return { column, ok: true, row };
}

function readGridLine(value: unknown): number | undefined | null {
  if (value === undefined || value === null || value === 'auto') return undefined;
  if (typeof value === 'number') return isPositiveInteger(value) ? value : null;
  if (typeof value !== 'string') return null;
  const token = value.trim().split(/\s|\//)[0];
  if (!token || token === 'auto') return undefined;
  const line = Number(token);
  return isPositiveInteger(line) ? line : null;
}

function findAvailableCell(
  row: number | undefined,
  column: number | undefined,
  rowSpan: number,
  columnSpan: number,
  columns: number,
  rows: number,
  occupied: ReadonlySet<string>,
  itemCount: number
): GridCellPlacement | null {
  const maxRow = Math.max(rows, itemCount * rowSpan + 1, row ?? 0);
  for (let candidateRow = row ?? 1; candidateRow <= maxRow; candidateRow += 1) {
    const firstColumn = column ?? 1;
    const lastColumn = column === undefined ? columns - columnSpan + 1 : column;
    for (let candidateColumn = firstColumn; candidateColumn <= lastColumn; candidateColumn += 1) {
      const placement = { column: candidateColumn, columnSpan, row: candidateRow, rowSpan };
      if (fits(placement, columns) && isFree(placement, occupied)) return placement;
    }
    if (row !== undefined) break;
  }
  return null;
}

function fits(placement: GridCellPlacement, columns: number): boolean {
  return placement.row >= 1 && placement.column >= 1 && placement.column + placement.columnSpan - 1 <= columns;
}

function isFree(placement: GridCellPlacement, occupied: ReadonlySet<string>): boolean {
  for (let row = placement.row; row < placement.row + placement.rowSpan; row += 1) {
    for (let column = placement.column; column < placement.column + placement.columnSpan; column += 1) {
      if (occupied.has(`${row}:${column}`)) return false;
    }
  }
  return true;
}

function occupy(placement: GridCellPlacement, occupied: Set<string>): void {
  for (let row = placement.row; row < placement.row + placement.rowSpan; row += 1) {
    for (let column = placement.column; column < placement.column + placement.columnSpan; column += 1) occupied.add(`${row}:${column}`);
  }
}

function readPositiveInteger(value: unknown): number | null {
  if (value === undefined) return 1;
  return isPositiveInteger(value) ? value : null;
}

function isPositiveInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && Number.isFinite(value) && value > 0;
}

function resolveTrackCount(value: unknown, template: unknown): number {
  if (isPositiveInteger(value)) return value;
  if (typeof template !== 'string') return 1;
  const repeatMatch = template.match(/^repeat\(\s*(\d+)\s*,/i);
  if (repeatMatch && Number(repeatMatch[1]) > 0) return Number(repeatMatch[1]);
  const explicitTracks = template.split(',').map((track) => track.trim()).filter(Boolean);
  return explicitTracks.length > 0 ? explicitTracks.length : 1;
}

function clampIndex(value: number, length: number): number {
  return Number.isFinite(value) ? Math.max(0, Math.min(length, Math.trunc(value))) : length;
}
