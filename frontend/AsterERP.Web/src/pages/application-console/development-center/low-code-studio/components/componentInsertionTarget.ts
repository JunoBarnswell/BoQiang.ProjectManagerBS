import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import { resolveParentLayout, type ComponentContentModel, type ComponentResizeHandle } from './componentInteractionPolicy';
import type { ComponentManifest } from './ComponentManifest';
import { ComponentRegistry } from './ComponentRegistry';

export interface ComponentInsertionTargetInput {
  document: DesignerDocument;
  manifests: ComponentRegistry;
  component: ComponentManifest;
  movingNodeIds?: readonly string[];
  selectedNodeId?: string | null;
  dropTargetNodeId?: string | null;
}

export type ComponentInsertionPlacement = 'before' | 'after' | 'inside';

export interface ComponentInsertionTarget {
  index: number;
  parentId: string;
  placement: ComponentInsertionPlacement;
  targetNodeId: string;
}

export interface ComponentInsertionPlacementInput {
  clientX: number;
  clientY: number;
  parentLayout?: Record<string, unknown>;
  rect: Pick<DOMRect, 'height' | 'left' | 'top' | 'width'>;
  targetAcceptsChildren: boolean;
  targetContentModel?: ComponentContentModel;
}

export interface ComponentResizeDecision {
  allowed: boolean;
  reason: 'allowed' | 'missing-node' | 'locked-node' | 'unknown-component' | 'unsupported-handle';
}

export function resolveComponentResizeDecision({ manifests, node, handle }: { manifests: ComponentRegistry; node: DesignerDocumentNode | undefined; handle: ComponentResizeHandle }): ComponentResizeDecision {
  if (!node) return { allowed: false, reason: 'missing-node' };
  if (isLocked(node)) return { allowed: false, reason: 'locked-node' };
  if (!manifests.get(node.type)) return { allowed: false, reason: 'unknown-component' };
  return manifests.canResize(node.type, handle)
    ? { allowed: true, reason: 'allowed' }
    : { allowed: false, reason: 'unsupported-handle' };
}

export function resolveComponentInsertionTarget({ document, manifests, component, movingNodeIds, selectedNodeId, dropTargetNodeId, placement = 'inside' }: ComponentInsertionTargetInput & { placement?: ComponentInsertionPlacement }): ComponentInsertionTarget | null {
  const candidateId = dropTargetNodeId ?? selectedNodeId ?? null;
  const candidate = candidateId ? document.elements[candidateId] : undefined;
  const moving = movingNodeIds ?? [];
  if (dropTargetNodeId && !candidate) return null;
  const movingComponents = movingNodeTypes(document, manifests, moving);
  if (moving.length > 0 && (!movingComponents || !isValidMovingSelection(document, moving))) return null;
  if (moving.length > 0 && candidate && isMovingIntoOwnSubtree(document, moving, candidate.id)) return null;
  if (candidate && isLocked(candidate)) return null;
  if (candidate) {
    if (placement === 'inside' && canAcceptComponents(candidate, manifests, component, movingComponents ?? [])) return { index: candidate.children.length, parentId: candidate.id, placement, targetNodeId: candidate.id };
    if ((placement === 'before' || placement === 'after') && candidate.parentId) {
      const parent = document.elements[candidate.parentId];
      const index = parent?.children.indexOf(candidate.id) ?? -1;
      if (parent && index >= 0 && canAcceptComponents(parent, manifests, component, movingComponents ?? [])) return { index: adjustInsertionIndex(parent.children, index + (placement === 'after' ? 1 : 0), moving), parentId: parent.id, placement, targetNodeId: candidate.id };
    }
  }
  if (moving.length > 0 && candidate) return null;
  const parentId = resolveComponentInsertionParent({ document, manifests, component, movingNodeIds: moving, dropTargetNodeId, selectedNodeId });
  if (!parentId) return null;
  return { index: document.elements[parentId]?.children.length ?? 0, parentId, placement: 'inside', targetNodeId: parentId };
}

export function resolveComponentInsertionPlacement({ clientX, clientY, parentLayout, rect, targetAcceptsChildren, targetContentModel }: ComponentInsertionPlacementInput): ComponentInsertionPlacement {
  const mode = resolveParentLayout(parentLayout ?? {});
  const horizontal = mode === 'grid' || (mode === 'flex' && parentLayout?.flexDirection !== 'column');
  const coordinate = horizontal ? clientX - rect.left : clientY - rect.top;
  const size = horizontal ? rect.width : rect.height;
  if (targetAcceptsChildren && targetContentModel !== 'void' && targetContentModel !== 'text' && coordinate >= size * 0.3 && coordinate <= size * 0.7) return 'inside';
  return coordinate < size / 2 ? 'before' : 'after';
}

export function resolveComponentInsertionParent({ document, manifests, component, movingNodeIds, selectedNodeId, dropTargetNodeId }: ComponentInsertionTargetInput): string | null {
  const moving = movingNodeIds ?? [];
  const movingComponents = movingNodeTypes(document, manifests, moving);
  if (moving.length > 0 && (!movingComponents || !isValidMovingSelection(document, moving))) return null;
  const candidateId = dropTargetNodeId ?? selectedNodeId ?? null;
  if (moving.length > 0 && candidateId && isMovingIntoOwnSubtree(document, moving, candidateId)) return null;
  if (dropTargetNodeId && !document.elements[dropTargetNodeId]) return null;
  const rootId = document.pages.map((page) => page.rootElementId).find((id) => canAcceptComponents(document.elements[id], manifests, component, movingComponents ?? []));
  const target = resolveContainerAncestor(document, manifests, dropTargetNodeId, component, movingComponents ?? []) ?? resolveContainerAncestor(document, manifests, selectedNodeId, component, movingComponents ?? []);
  return target ?? rootId ?? null;
}

function resolveContainerAncestor(document: DesignerDocument, manifests: ComponentRegistry, nodeId: string | null | undefined, component: ComponentManifest, movingComponents: readonly ComponentManifest[]): string | null {
  const visited = new Set<string>();
  let currentId = nodeId ?? null;
  while (currentId && !visited.has(currentId)) {
    visited.add(currentId);
    const current = document.elements[currentId];
    if (!current || isLocked(current)) return null;
    if (canAcceptComponents(current, manifests, component, movingComponents)) return current.id;
    currentId = current.parentId;
  }
  return null;
}

function canAcceptComponents(node: DesignerDocumentNode | undefined, manifests: ComponentRegistry, component: ComponentManifest, movingComponents: readonly ComponentManifest[] = []): boolean {
  if (!node || node.locked || node.layout.locked === true || node.props.locked === true) return false;
  return [component, ...movingComponents].every((item) => manifests.canContain(node.type, item.type, node.layout));
}

function movingNodeTypes(document: DesignerDocument, manifests: ComponentRegistry, movingNodeIds: readonly string[]): ComponentManifest[] | null {
  const nodes = movingNodeIds.map((id) => document.elements[id]);
  if (nodes.some((node) => !node)) return null;
  const manifestsForNodes = nodes.map((node) => node ? manifests.get(node.type) : undefined);
  if (manifestsForNodes.some((manifest) => !manifest)) return null;
  return manifestsForNodes.filter((manifest): manifest is ComponentManifest => Boolean(manifest));
}

function isValidMovingSelection(document: DesignerDocument, movingNodeIds: readonly string[]): boolean {
  if (movingNodeIds.some((id) => document.pages.some((page) => page.rootElementId === id))) return false;
  if (movingNodeIds.some((id) => isLocked(document.elements[id]!))) return false;
  return movingNodeIds.every((id, index) => movingNodeIds.every((otherId, otherIndex) => index === otherIndex || !isDescendant(document, otherId, id)));
}

function adjustInsertionIndex(children: readonly string[], index: number, movingNodeIds: readonly string[]): number {
  const moving = new Set(movingNodeIds);
  return Math.max(0, index - children.slice(0, index).filter((childId) => moving.has(childId)).length);
}

function isLocked(node: DesignerDocumentNode): boolean {
  return node.locked === true || node.layout.locked === true || node.props.locked === true;
}

function isMovingIntoOwnSubtree(document: DesignerDocument, movingNodeIds: readonly string[], targetId: string): boolean {
  return movingNodeIds.some((movingId) => movingId === targetId || isDescendant(document, movingId, targetId));
}

function isDescendant(document: DesignerDocument, ancestorId: string, candidateId: string): boolean {
  const visited = new Set<string>();
  let currentId: string | null = candidateId;
  while (currentId && !visited.has(currentId)) {
    if (currentId === ancestorId) return true;
    visited.add(currentId);
    currentId = document.elements[currentId]?.parentId ?? null;
  }
  return false;
}
