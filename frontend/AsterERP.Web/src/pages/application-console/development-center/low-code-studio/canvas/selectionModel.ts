import type { CanvasRect } from './coordinateSystem';

export interface CanvasSelection { selectedNodeIds: string[]; primaryNodeId: string | null; anchorNodeId: string | null }

export function createSelection(selectedNodeIds: readonly string[] = []): CanvasSelection {
  const ids = [...new Set(selectedNodeIds)];
  return { selectedNodeIds: ids, primaryNodeId: ids.at(-1) ?? null, anchorNodeId: ids[0] ?? null };
}

export function toggleSelection(selection: CanvasSelection, id: string, additive: boolean): CanvasSelection {
  if (!additive) return createSelection([id]);
  const ids = selection.selectedNodeIds.includes(id) ? selection.selectedNodeIds.filter((candidate) => candidate !== id) : [...selection.selectedNodeIds, id];
  const anchorNodeId = selection.anchorNodeId && ids.includes(selection.anchorNodeId) ? selection.anchorNodeId : ids[0] ?? null;
  return { selectedNodeIds: ids, primaryNodeId: ids.at(-1) ?? null, anchorNodeId };
}

export function selectByMarquee(nodes: readonly CanvasRect[], marquee: CanvasRect, additive = false, previous: CanvasSelection = createSelection()): CanvasSelection {
  const ids = nodes.filter((node) => intersects(node, marquee)).map((node) => node.id).filter((id): id is string => Boolean(id));
  return createSelection(additive ? [...previous.selectedNodeIds, ...ids] : ids);
}

export function intersects(a: CanvasRect, b: CanvasRect): boolean {
  return a.x < b.x + b.width && a.x + a.width > b.x && a.y < b.y + b.height && a.y + a.height > b.y;
}
