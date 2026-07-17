export interface SelectionModel {
  anchorNodeId: string | null;
  primaryNodeId: string | null;
  selectedNodeIds: string[];
}

export function createSelectionModel(ids: readonly string[] = []): SelectionModel {
  const selectedNodeIds = unique(ids);
  return {
    anchorNodeId: selectedNodeIds[0] ?? null,
    primaryNodeId: selectedNodeIds[0] ?? null,
    selectedNodeIds
  };
}

export function selectNode(selection: SelectionModel, nodeId: string, additive = false): SelectionModel {
  const selectedNodeIds = additive
    ? selection.selectedNodeIds.includes(nodeId)
      ? selection.selectedNodeIds.filter((id) => id !== nodeId)
      : [...selection.selectedNodeIds, nodeId]
    : [nodeId];
  return {
    anchorNodeId: selectedNodeIds.length > 0 ? selection.anchorNodeId ?? selectedNodeIds[0] : null,
    primaryNodeId: selectedNodeIds.length > 0 ? nodeId : null,
    selectedNodeIds
  };
}

export function selectRange(selection: SelectionModel, orderedIds: readonly string[], nodeId: string): SelectionModel {
  if (!selection.anchorNodeId) return selectNode(selection, nodeId);
  const start = orderedIds.indexOf(selection.anchorNodeId);
  const end = orderedIds.indexOf(nodeId);
  if (start < 0 || end < 0) return selectNode(selection, nodeId);
  const selectedNodeIds = orderedIds.slice(Math.min(start, end), Math.max(start, end) + 1);
  return { anchorNodeId: selection.anchorNodeId, primaryNodeId: nodeId, selectedNodeIds };
}

export function removeNodes(selection: SelectionModel, removedIds: ReadonlySet<string>, fallbackNodeId: string | null): SelectionModel {
  const selectedNodeIds = selection.selectedNodeIds.filter((id) => !removedIds.has(id));
  const fallback = selectedNodeIds[0] ?? fallbackNodeId;
  return {
    anchorNodeId: selection.anchorNodeId && !removedIds.has(selection.anchorNodeId) ? selection.anchorNodeId : fallback,
    primaryNodeId: selection.primaryNodeId && !removedIds.has(selection.primaryNodeId) ? selection.primaryNodeId : fallback,
    selectedNodeIds
  };
}

export function normalizeSelection(selection: SelectionModel, existingIds: ReadonlySet<string>): SelectionModel {
  const selectedNodeIds = selection.selectedNodeIds.filter((id) => existingIds.has(id));
  const primaryNodeId = selection.primaryNodeId && existingIds.has(selection.primaryNodeId) ? selection.primaryNodeId : selectedNodeIds[0] ?? null;
  const anchorNodeId = selection.anchorNodeId && existingIds.has(selection.anchorNodeId) ? selection.anchorNodeId : primaryNodeId;
  return { anchorNodeId, primaryNodeId, selectedNodeIds };
}

function unique(ids: readonly string[]): string[] {
  return [...new Set(ids.filter(Boolean))];
}
