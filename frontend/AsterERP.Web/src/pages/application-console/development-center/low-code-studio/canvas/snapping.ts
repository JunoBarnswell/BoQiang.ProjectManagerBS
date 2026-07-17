import type { CanvasPoint, CanvasRect } from './coordinateSystem';

export interface SnapGuide { axis: 'x' | 'y'; position: number; sourceId?: string }
export interface SnapResult { point: CanvasPoint; guides: SnapGuide[] }

export interface SnapOptions {
  gridSize?: number;
  threshold?: number;
  enabled?: boolean;
}

export function snapRect(rect: CanvasRect, peers: readonly CanvasRect[], gridSize = 8, threshold = 6): SnapResult {
  return snapRectWithOptions(rect, peers, { gridSize, threshold });
}

export function snapRectWithOptions(rect: CanvasRect, peers: readonly CanvasRect[], options: SnapOptions = {}): SnapResult {
  if (options.enabled === false) return { point: { x: rect.x, y: rect.y }, guides: [] };
  const gridSize = Math.max(1, options.gridSize ?? 8);
  const threshold = Math.max(0, options.threshold ?? 6);
  const xEdges = [rect.x, rect.x + rect.width / 2, rect.x + rect.width];
  const yEdges = [rect.y, rect.y + rect.height / 2, rect.y + rect.height];
  const xCandidates = [{ value: Math.round(rect.x / gridSize) * gridSize }, ...peers.flatMap((peer) => [
    { value: peer.x, sourceId: peer.id }, { value: peer.x + peer.width / 2, sourceId: peer.id }, { value: peer.x + peer.width, sourceId: peer.id }
  ])];
  const yCandidates = [{ value: Math.round(rect.y / gridSize) * gridSize }, ...peers.flatMap((peer) => [
    { value: peer.y, sourceId: peer.id }, { value: peer.y + peer.height / 2, sourceId: peer.id }, { value: peer.y + peer.height, sourceId: peer.id }
  ])];
  const x = nearestEdge(xEdges, xCandidates, threshold);
  const y = nearestEdge(yEdges, yCandidates, threshold);
  return { point: { x: rect.x + (x?.delta ?? 0), y: rect.y + (y?.delta ?? 0) }, guides: [...(x ? [{ axis: 'x' as const, position: x.target, sourceId: x.sourceId }] : []), ...(y ? [{ axis: 'y' as const, position: y.target, sourceId: y.sourceId }] : [])] };
}

function nearestEdge(edges: readonly number[], candidates: readonly { value: number; sourceId?: string }[], threshold: number): { delta: number; target: number; sourceId?: string } | null {
  let best: { distance: number; delta: number; target: number; sourceId?: string } | null = null;
  for (const edge of edges) for (const candidate of candidates) {
    const distance = Math.abs(candidate.value - edge);
    if (distance <= threshold && (!best || distance < best.distance)) best = { distance, delta: candidate.value - edge, target: candidate.value, sourceId: candidate.sourceId };
  }
  return best;
}
