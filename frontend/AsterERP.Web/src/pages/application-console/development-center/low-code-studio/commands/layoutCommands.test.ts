import { describe, expect, it } from 'vitest';

import type { DesignerDocument } from '../document/DesignerDocument';

import { createInsertNodesCommand, createLayoutOperationCommand, createMoveNodesCommand, createSetLayoutModeCommand } from './createDesignerCommands';
import { DesignerCommandBus } from './DesignerCommandBus';

describe('layout commands', () => {
  it('switches a container mode as one undoable document command', () => {
    const document = createDocument({ layoutMode: 'free', width: 640 });
    const bus = new DesignerCommandBus(document);

    const result = bus.execute(createSetLayoutModeCommand('root', { mode: 'grid', columns: 2, gap: 12 }));

    expect(result.changed).toBe(true);
    expect(bus.document.elements.root.layout).toMatchObject({ container: { mode: 'grid', grid: { columnGap: 12, rowGap: 12, columns: ['1fr', '1fr'] } }, placement: { kind: 'grid-item' }, size: { width: 640 } });
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.root.layout).toMatchObject({ layoutMode: 'free', width: 640 });
  });

  it('aligns free children by geometry and flex children through the parent protocol', () => {
    const free = new DesignerCommandBus(createDocument({ layoutMode: 'free' }));
    expect(free.execute(createLayoutOperationCommand(['left', 'right'], 'align-top')).changed).toBe(true);
    expect(free.document.elements.right.layout.y).toBe(10);

    const flex = new DesignerCommandBus(createDocument({ layoutMode: 'flex' }));
    expect(flex.execute(createLayoutOperationCommand(['left', 'right'], 'align-center')).changed).toBe(true);
    expect(flex.document.elements.root.layout.justifyContent).toBe('center');
    expect(flex.document.elements.left.layout.x).toBe(10);
  });

  it('rejects an operation with no valid semantics for the selected mode', () => {
    const bus = new DesignerCommandBus(createDocument({ layoutMode: 'grid' }));
    const result = bus.execute(createLayoutOperationCommand(['left', 'right'], 'distribute-horizontal'));
    expect(result.changed).toBe(false);
    expect(result.diagnostics).toContain('Operation distribute-horizontal is not supported by grid layout');
  });

  it('uses insertion order and target-mode layout semantics for structural commands', () => {
    const bus = new DesignerCommandBus(createDocument({ layoutMode: 'flex' }));
    const inserted = { id: 'inserted', parentId: 'root', children: [], events: [], layout: { position: 'absolute', x: 22, y: 33 }, props: {}, type: 'text' };
    expect(bus.execute(createInsertNodesCommand([inserted], 1)).changed).toBe(true);
    expect(bus.document.elements.root.children).toEqual(['left', 'inserted', 'right']);
    expect(bus.document.elements.inserted.layout).not.toHaveProperty('position');

    expect(bus.execute(createMoveNodesCommand(['right'], 'root', 0)).changed).toBe(true);
    expect(bus.document.elements.root.children).toEqual(['right', 'left', 'inserted']);
  });

  it('keeps flex visual order aligned with reordered children', () => {
    const bus = new DesignerCommandBus(createDocument({ display: 'flex', layoutMode: 'flex' }));
    expect(bus.execute(createMoveNodesCommand(['right'], 'root', 0)).changed).toBe(true);
    expect(bus.document.elements.root.children).toEqual(['right', 'left']);
    expect(bus.document.elements.right.layout.placement?.flexItem?.order).toBe(0);
    expect(bus.document.elements.left.layout.placement?.flexItem?.order).toBe(1);
    expect(bus.document.elements.right.layout).not.toHaveProperty('order');
    expect(bus.document.elements.left.layout).not.toHaveProperty('order');
  });

  it('inserts and resizes free children with bounded x/y/width/height geometry', () => {
    const document = createDocument({ height: 200, layoutMode: 'free', width: 300 });
    const bus = new DesignerCommandBus(document);
    const inserted = { children: [], events: [], id: 'bounded', layout: { constraints: { left: 10 }, height: 40, width: 400, x: -20, y: 190 }, parentId: 'root', props: {}, type: 'text' };

    expect(bus.execute(createInsertNodesCommand([inserted])).changed).toBe(true);
    expect(bus.document.elements.bounded.layout).toMatchObject({ height: 40, position: 'absolute', width: 300, x: 0, y: 160 });
    expect(bus.document.elements.bounded.layout).not.toHaveProperty('constraints');

    const resized = bus.execute(createMoveNodesCommand({
      layoutPatches: { bounded: { height: 80, width: 120, x: 280, y: 190 } },
      nodeIds: ['bounded'],
      parentId: 'root',
      targetLayoutMode: 'free'
    }));
    expect(resized.changed).toBe(true);
    expect(bus.document.elements.bounded.layout).toMatchObject({ height: 80, position: 'absolute', width: 120, x: 180, y: 120 });
  });

  it('migrates flex children to free geometry while preserving their measured placement', () => {
    const document = createDocument({ display: 'flex', gap: 10, layoutMode: 'flex' });
    document.elements.left.layout = { flexGrow: 1, height: 20, width: 80 };
    document.elements.right.layout = { flexGrow: 1, height: 20, width: 60 };
    const bus = new DesignerCommandBus(document);

    expect(bus.execute(createSetLayoutModeCommand('root', { mode: 'free' })).changed).toBe(true);
    expect(bus.document.elements.left.layout).toMatchObject({ container: { mode: 'free' }, placement: { kind: 'absolute', absolute: { x: 0, y: 0 } }, size: { height: 20, width: 80 } });
    expect(bus.document.elements.right.layout).toMatchObject({ container: { mode: 'free' }, placement: { kind: 'absolute', absolute: { x: 90, y: 0 } }, size: { height: 20, width: 60 } });
    expect(bus.document.elements.left.layout).not.toHaveProperty('flexGrow');
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.root.layout.layoutMode).toBe('flex');
    expect(bus.redo()?.changed).toBe(true);
    expect(bus.document.elements.right.layout.placement?.absolute?.x).toBe(90);
  });

  it('preserves canonical child-owned layouts through a flex round-trip', () => {
    const document = createDocument({
      container: { mode: 'free' },
      placement: { kind: 'absolute', absolute: { x: 0, y: 0 } },
      size: { width: 640, height: 240 }
    });
    document.elements.left.layout = { container: { mode: 'free' }, placement: { kind: 'absolute', absolute: { x: 0, y: 0 } }, size: { width: 80, height: 20 } };
    document.elements.right.layout = { container: { mode: 'free' }, placement: { kind: 'absolute', absolute: { x: 120, y: 50 } }, size: { width: 80, height: 20 } };
    const bus = new DesignerCommandBus(document);

    expect(bus.execute(createSetLayoutModeCommand('root', { mode: 'flex', flexDirection: 'row' })).changed).toBe(true);
    expect(bus.document.elements.left.layout).toMatchObject({ container: { mode: 'free' }, placement: { absolute: { x: 0, y: 0 } } });
    expect(bus.document.elements.right.layout).toMatchObject({ container: { mode: 'free' }, placement: { absolute: { x: 120, y: 50 } } });

    expect(bus.execute(createSetLayoutModeCommand('root', { mode: 'free' })).changed).toBe(true);
    expect(bus.document.elements.left.layout).toMatchObject({ placement: { absolute: { x: 0, y: 0 } }, size: { width: 80, height: 20 } });
    expect(bus.document.elements.right.layout).toMatchObject({ placement: { absolute: { x: 120, y: 50 } }, size: { width: 80, height: 20 } });
    expect(bus.document.elements.left.layout).not.toHaveProperty('anchor');
    expect(bus.document.elements.right.layout).not.toHaveProperty('anchor');
  });

  it('restores the previous flex container configuration after a persisted mode round-trip', () => {
    const document = createDocument({
      container: { mode: 'flex', flex: { direction: 'row', wrap: 'nowrap', gap: 0, alignItems: 'start', justifyContent: 'end' } },
      placement: { kind: 'flex-item', flexItem: { order: 0, grow: 0, shrink: 1, basis: 'auto' } },
      size: { width: 640, height: 240 }
    });
    document.elements.root.layout.placement = { kind: 'flex-item', flexItem: { order: 0, grow: 0, shrink: 1, basis: 'auto', alignSelf: 'auto' } };
    const bus = new DesignerCommandBus(document);

    expect(bus.execute(createSetLayoutModeCommand('root', { mode: 'free' })).changed).toBe(true);
    const migration = bus.document.elements.root.layout.migration as { previousContainers?: Record<string, unknown> } | undefined;
    expect(migration?.previousContainers?.flex).toMatchObject({ mode: 'flex', flex: { alignItems: 'start', justifyContent: 'end' } });
    expect(bus.execute(createSetLayoutModeCommand('root', { mode: 'flex', flexDirection: 'row' })).changed).toBe(true);
    expect(bus.document.elements.root.layout.container?.flex).toMatchObject({ alignItems: 'start', justifyContent: 'end' });
    expect(bus.document.elements.root.layout.placement?.flexItem).toMatchObject({ alignSelf: 'auto' });
    expect(bus.document.elements.root.layout.migration).toBeUndefined();
  });

  it('writes responsive free moves only to the override and supports undo/redo', () => {
    const bus = new DesignerCommandBus(createDocument({ layoutMode: 'free' }));
    const baseLayout = structuredClone(bus.document.elements.left.layout);
    const children = [...bus.document.elements.root.children];

    const result = bus.execute(createMoveNodesCommand({
      breakpointId: 'tablet',
      layoutPatches: { left: { height: 30, width: 100, x: 50, y: 60 } },
      nodeIds: ['left'],
      parentId: 'root',
      targetLayoutMode: 'free'
    }));

    expect(result.changed).toBe(true);
    expect(bus.document.elements.left.layout).toEqual(baseLayout);
    expect(bus.document.elements.left.responsiveOverrides?.tablet.layout).toEqual({ height: 30, width: 100, x: 50, y: 60 });
    expect(bus.document.elements.root.children).toEqual(children);
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.left.responsiveOverrides?.tablet).toBeUndefined();
    expect(bus.redo()?.changed).toBe(true);
    expect(bus.document.elements.left.layout).toEqual(baseLayout);
  });

  it('writes responsive free alignment only to overrides and keeps failed commands atomic', () => {
    const document = createDocument({ layoutMode: 'free' });
    const bus = new DesignerCommandBus(document);
    const before = bus.document;

    const aligned = bus.execute(createLayoutOperationCommand(['left', 'right'], 'align-top', undefined, 'tablet'));
    expect(aligned.changed).toBe(true);
    expect(bus.document.elements.left.layout.y).toBe(10);
    expect(bus.document.elements.right.layout.y).toBe(50);
    expect(bus.document.elements.right.responsiveOverrides?.tablet.layout).toEqual({ y: 10 });
    expect(bus.document.elements.left.responsiveOverrides?.tablet).toBeUndefined();

    const failed = bus.execute(createMoveNodesCommand({ nodeIds: ['left', 'missing'], parentId: 'root' }));
    expect(failed.changed).toBe(false);
    expect(failed.document).toBe(bus.document);
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.left.responsiveOverrides?.tablet).toBeUndefined();
    expect(bus.document.revision).toBe(before.revision + 2);
  });
});

function createDocument(layout: Record<string, unknown>): DesignerDocument {
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'layout-test', elements: {
      root: { id: 'root', parentId: null, children: ['left', 'right'], events: [], layout, props: {}, type: 'layout.container' },
      left: { id: 'left', parentId: 'root', children: [], events: [], layout: { x: 10, y: 10, width: 80, height: 20 }, props: {}, type: 'text' },
      right: { id: 'right', parentId: 'root', children: [], events: [], layout: { x: 120, y: 50, width: 80, height: 20 }, props: {}, type: 'text' }
    },
    metadata: {}, modals: [], pageParameters: [], pageType: 'page', pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }], permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}
