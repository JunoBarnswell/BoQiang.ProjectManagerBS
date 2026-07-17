import { describe, expect, it } from 'vitest';

import { cloneClipboardForest, cloneClipboardTree, collectClipboardIds, type ClipboardTree, type IdentifiedNode } from './clipboardTree';
import { findShortcutConflicts, nudgeDistance, resolveShortcut } from './shortcutModel';

describe('latest canvas shortcuts and clipboard', () => {
  it('resolves command shortcuts and keyboard nudges', () => {
    expect(resolveShortcut({ key: 'c', ctrlKey: true })).toBe('copy');
    expect(resolveShortcut({ key: 'z', metaKey: true, shiftKey: true })).toBe('redo');
    expect(resolveShortcut({ key: 'ArrowRight' })).toBe('nudge-right');
    expect(resolveShortcut({ key: 'd', ctrlKey: true })).toBe('duplicate');
    expect(resolveShortcut({ key: '0' })).toBe('fit-page');
    expect(resolveShortcut({ key: '0', shiftKey: true })).toBe('fit-selection');
    expect(nudgeDistance({ key: 'ArrowRight', shiftKey: true })).toBe(10);
  });

  it('clones a subtree with unique descendant IDs and rewritten parent IDs', () => {
    const tree: ClipboardTree<IdentifiedNode> = { root: { id: 'root', parentId: null, children: ['child'] }, children: [{ root: { id: 'child', parentId: 'root', children: [] }, children: [] }] };
    const copy = cloneClipboardTree(tree, 'page', (id) => `${id}-copy`);
    expect(collectClipboardIds(copy)).toEqual(['root-copy', 'child-copy']);
    expect(copy.root.parentId).toBe('page');
    expect(copy.children[0].root.parentId).toBe('root-copy');
  });

  it('clones a forest with one unique ID allocation per source node', () => {
    const tree: ClipboardTree<IdentifiedNode> = { root: { id: 'root', parentId: null, children: ['child'] }, children: [{ root: { id: 'child', parentId: 'root', children: [] }, children: [] }] };
    const ids = new Set<string>();
    const copy = cloneClipboardForest([tree], 'page', (source) => { const id = `${source}-${ids.size + 1}`; ids.add(id); return id; }, { create: (source, id, parentId, children) => ({ ...source, id, parentId, children: [...children] }) });
    expect(copy[0].root.children).toEqual([copy[0].children[0].root.id]);
    expect(new Set(collectClipboardIds(copy[0])).size).toBe(2);
  });

  it('supports injected bindings and reports conflicting mappings', () => {
    const bindings = [
      { action: 'copy' as const, key: 'k', ctrlOrMeta: true },
      { action: 'paste' as const, key: 'k', ctrlOrMeta: true },
      { action: 'delete' as const, key: 'x' }
    ];
    expect(resolveShortcut({ key: 'k', ctrlKey: true }, bindings)).toBe('copy');
    expect(resolveShortcut({ key: 'c', ctrlKey: true }, bindings)).toBeNull();
    expect(findShortcutConflicts(bindings)).toEqual([['k|true|false|false', ['copy', 'paste']]]);
  });
});
