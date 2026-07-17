import type { CanvasRect } from './coordinateSystem';

export interface CanvasSpatialIndex {
  readonly size: number;
  query(rect: CanvasRect, padding?: number): readonly CanvasRect[];
  measure(rect: CanvasRect, padding?: number): readonly CanvasProximityMeasurement[];
}

export interface CanvasProximityMeasurement {
  readonly centers: { x: number; y: number };
  readonly edges: { bottom: number; left: number; right: number; top: number };
  readonly gap: { x: number; y: number };
  readonly rect: CanvasRect;
}

export function createCanvasSpatialIndex(rects: readonly CanvasRect[], cellSize = 128): CanvasSpatialIndex {
  const normalizedCellSize = Math.max(1, cellSize);
  const cells = new Map<string, Set<string>>();
  const byId = new Map<string, CanvasRect>();
  const anonymous = new Map<string, CanvasRect>();

  rects.forEach((rect, index) => {
    const key = rect.id ?? `anonymous-${index}`;
    if (rect.id) byId.set(rect.id, rect);
    else anonymous.set(key, rect);
    forEachCell(rect, normalizedCellSize, (cell) => {
      const entries = cells.get(cell) ?? new Set<string>();
      entries.add(key);
      cells.set(cell, entries);
    });
  });

  return {
    size: rects.length,
    query(rect, padding = 0) {
      const result = new Map<string, CanvasRect>();
      forEachCell(expand(rect, Math.max(0, padding)), normalizedCellSize, (cell) => {
        for (const key of cells.get(cell) ?? []) {
          const candidate = byId.get(key) ?? anonymous.get(key);
          if (candidate && intersects(expand(rect, Math.max(0, padding)), candidate)) result.set(key, candidate);
        }
      });
      return [...result.values()];
    },
    measure(rect, padding = 0) {
      return this.query(rect, padding).map((candidate) => measure(candidate, rect));
    }
  };
}

function measure(candidate: CanvasRect, reference: CanvasRect): CanvasProximityMeasurement {
  const candidateRight = candidate.x + candidate.width;
  const candidateBottom = candidate.y + candidate.height;
  const referenceRight = reference.x + reference.width;
  const referenceBottom = reference.y + reference.height;
  return {
    centers: { x: candidate.x + candidate.width / 2, y: candidate.y + candidate.height / 2 },
    edges: { bottom: candidateBottom, left: candidate.x, right: candidateRight, top: candidate.y },
    gap: { x: axisGap(candidate.x, candidateRight, reference.x, referenceRight), y: axisGap(candidate.y, candidateBottom, reference.y, referenceBottom) },
    rect: candidate
  };
}

function axisGap(candidateStart: number, candidateEnd: number, referenceStart: number, referenceEnd: number): number {
  if (candidateEnd < referenceStart) return referenceStart - candidateEnd;
  if (referenceEnd < candidateStart) return candidateStart - referenceEnd;
  return 0;
}

function forEachCell(rect: CanvasRect, cellSize: number, visit: (key: string) => void): void {
  const left = Math.floor(rect.x / cellSize);
  const right = Math.floor((rect.x + Math.max(0, rect.width)) / cellSize);
  const top = Math.floor(rect.y / cellSize);
  const bottom = Math.floor((rect.y + Math.max(0, rect.height)) / cellSize);
  for (let x = left; x <= right; x += 1) for (let y = top; y <= bottom; y += 1) visit(`${x}:${y}`);
}

function expand(rect: CanvasRect, padding: number): CanvasRect {
  return { ...rect, x: rect.x - padding, y: rect.y - padding, width: rect.width + padding * 2, height: rect.height + padding * 2 };
}

function intersects(left: CanvasRect, right: CanvasRect): boolean {
  return left.x <= right.x + right.width && left.x + left.width >= right.x && left.y <= right.y + right.height && left.y + left.height >= right.y;
}
