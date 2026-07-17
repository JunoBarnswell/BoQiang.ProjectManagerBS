import { describe, expect, it } from 'vitest';

import { createSelectionModel, normalizeSelection, removeNodes, selectNode, selectRange } from './SelectionModel';

describe('SelectionModel', () => {
  it('supports additive and range selection with primary and anchor nodes', () => {
    let selection = createSelectionModel(['a']);
    selection = selectNode(selection, 'b', true);
    expect(selection.selectedNodeIds).toEqual(['a', 'b']);
    selection = selectRange(selection, ['a', 'b', 'c', 'd'], 'd');
    expect(selection.selectedNodeIds).toEqual(['a', 'b', 'c', 'd']);
    expect(selection.primaryNodeId).toBe('d');
    expect(selection.anchorNodeId).toBe('a');
  });

  it('falls back after deletion and removes stale nodes', () => {
    const selection = removeNodes(createSelectionModel(['a', 'b']), new Set(['a']), 'root');
    expect(selection.selectedNodeIds).toEqual(['b']);
    expect(selection.primaryNodeId).toBe('b');
    const fallback = removeNodes(selection, new Set(['b']), 'root');
    expect(fallback).toEqual({ anchorNodeId: 'root', primaryNodeId: 'root', selectedNodeIds: [] });
    expect(normalizeSelection(fallback, new Set(['root']))).toEqual(fallback);
  });
});
