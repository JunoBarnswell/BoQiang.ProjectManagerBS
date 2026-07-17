import type { DesignerDocumentNode } from '../document/DesignerDocument';

import type { ComponentManifest } from './ComponentManifest';

export const COMPONENT_POINTER_DROP_EVENT = 'astererp:component-pointer-drop';
export const COMPONENT_POINTER_DRAG_EVENT = 'astererp:component-pointer-drag';
export const COMPONENT_POINTER_DRAG_END_EVENT = 'astererp:component-pointer-drag-end';

export interface ComponentPointerDropDetail {
  clientX: number;
  clientY: number;
  pointerId: number;
  type: string;
}

export interface ComponentInsertionPosition {
  x: number;
  y: number;
}

export function createComponentPointerDropEvent(detail: ComponentPointerDropDetail): CustomEvent<ComponentPointerDropDetail> {
  return new CustomEvent<ComponentPointerDropDetail>(COMPONENT_POINTER_DROP_EVENT, { detail });
}

export function createComponentPointerDragEvent(detail: ComponentPointerDropDetail): CustomEvent<ComponentPointerDropDetail> {
  return new CustomEvent<ComponentPointerDropDetail>(COMPONENT_POINTER_DRAG_EVENT, { detail });
}

export function createComponentPointerDragEndEvent(pointerId: number): CustomEvent<{ pointerId: number }> {
  return new CustomEvent<{ pointerId: number }>(COMPONENT_POINTER_DRAG_END_EVENT, { detail: { pointerId } });
}

export function createComponentNode(manifest: ComponentManifest, parentId: string, id: string, position?: ComponentInsertionPosition): DesignerDocumentNode {
  return {
    children: [],
    events: manifest.events.map((event) => ({ name: event.name, steps: [], trigger: event.trigger })),
    id,
    layout: {
      ...manifest.defaults.layout,
      ...(position ? { position: 'absolute', x: Math.max(0, Math.round(position.x)), y: Math.max(0, Math.round(position.y)) } : {})
    },
    parentId,
    props: { ...manifest.defaults.props },
    style: { ...manifest.defaults.style },
    type: manifest.type
  };
}

export function createComponentNodeId(type: string, occupiedIds: ReadonlySet<string>): string {
  const prefix = `component-${type.replaceAll('.', '-')}`;
  let index = 1;
  let id = `${prefix}-${index}`;
  while (occupiedIds.has(id)) id = `${prefix}-${++index}`;
  return id;
}
