import { describe, expect, it } from 'vitest';

import { COMPONENT_POINTER_DROP_EVENT, createComponentNode, createComponentNodeId, createComponentPointerDropEvent } from './componentInsertion';
import { latestComponentRegistry } from './latestComponentManifestCatalog';

describe('component insertion', () => {
  it('creates a manifest-backed node with copied defaults and target parent', () => {
    const manifest = latestComponentRegistry.get('action.button');
    expect(manifest).toBeDefined();
    const node = createComponentNode(manifest!, 'layout-root', 'component-action-button-1');
    expect(node).toMatchObject({ id: 'component-action-button-1', parentId: 'layout-root', type: 'action.button', children: [], props: manifest!.defaults.props, style: manifest!.defaults.style });
    expect(node.events).toEqual([{ name: manifest!.events[0].name, steps: [], trigger: manifest!.events[0].trigger }]);
  });

  it('allocates a deterministic unused id for drag/drop insertion', () => {
    expect(createComponentNodeId('text.heading', new Set(['component-text-heading-1']))).toBe('component-text-heading-2');
  });

  it('creates a pointer drop event without relying on HTML5 drag data transfer', () => {
    const event = createComponentPointerDropEvent({ clientX: 12, clientY: 24, pointerId: 7, type: 'text.heading' });
    expect(event.type).toBe(COMPONENT_POINTER_DROP_EVENT);
    expect(event.detail).toEqual({ clientX: 12, clientY: 24, pointerId: 7, type: 'text.heading' });
  });
});
