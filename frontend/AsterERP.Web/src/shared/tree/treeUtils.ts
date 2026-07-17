import type { FormOption } from '../forms/formTypes';

export interface TreeLikeNode<TNode> {
  children?: TNode[];
}

export function filterTreeNodes<TNode extends TreeLikeNode<TNode>>(
  nodes: TNode[],
  keyword: string,
  getSearchText: (node: TNode) => string
): TNode[] {
  const normalizedKeyword = keyword.trim().toLowerCase();

  if (!normalizedKeyword) {
    return nodes;
  }

  const result: TNode[] = [];

  for (const node of nodes) {
    const children = filterTreeNodes(node.children ?? [], normalizedKeyword, getSearchText);
    const matchesSelf = getSearchText(node).toLowerCase().includes(normalizedKeyword);

    if (matchesSelf || children.length > 0) {
      result.push({ ...node, children } as TNode);
    }
  }

  return result;
}

export function flattenTreeToOptions<TNode extends TreeLikeNode<TNode>>(
  nodes: TNode[],
  getLabel: (node: TNode) => string,
  getValue: (node: TNode) => string,
  depth = 0
): FormOption[] {
  return nodes.flatMap((node) => [
    { label: `${'　'.repeat(depth)}${getLabel(node)}`, value: getValue(node) },
    ...flattenTreeToOptions(node.children ?? [], getLabel, getValue, depth + 1)
  ]);
}
