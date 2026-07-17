export interface ClipboardTree<T> { root: T; children: readonly ClipboardTree<T>[] }
export interface IdentifiedNode { id: string; parentId: string | null; children: readonly string[] }

export interface ClipboardTreeFactory<T> { create(source: T, id: string, parentId: string | null, children: readonly string[]): T }

export function cloneClipboardTree<T extends IdentifiedNode>(tree: ClipboardTree<T>, parentId: string | null, nextId: (sourceId: string) => string): ClipboardTree<T> {
  const id = nextId(tree.root.id);
  const root = { ...tree.root, id, parentId, children: tree.children.map((child) => nextId(child.root.id)) } as T;
  return { root, children: tree.children.map((child) => cloneClipboardTree(child, id, nextId)) };
}

export function collectClipboardIds<T>(tree: ClipboardTree<T & { id: string }>): string[] { return [tree.root.id, ...tree.children.flatMap(collectClipboardIds)]; }

export function cloneClipboardForest<T extends IdentifiedNode>(trees: readonly ClipboardTree<T>[], parentId: string | null, nextId: (sourceId: string) => string, factory: ClipboardTreeFactory<T>): ClipboardTree<T>[] {
  return trees.map((tree) => cloneWithFactory(tree, parentId, nextId, factory));
}

function cloneWithFactory<T extends IdentifiedNode>(tree: ClipboardTree<T>, parentId: string | null, nextId: (sourceId: string) => string, factory: ClipboardTreeFactory<T>): ClipboardTree<T> {
  const id = nextId(tree.root.id);
  const children = tree.children.map((child) => cloneWithFactory(child, id, nextId, factory));
  return { root: factory.create(tree.root, id, parentId, children.map((child) => child.root.id)), children };
}
