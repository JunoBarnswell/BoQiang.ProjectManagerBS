import { describe, expect, it } from 'vitest';

import { latestComponentRegistry } from '../components/latestComponentManifestCatalog';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import { createFlexResizeLayoutPatch, planCanvasMove, resolveCanvasMoveTarget, type CanvasMoveGeometry } from './canvasMovePlanner';

describe('canvas move planner', () => {
  it('recomputes free-layout coordinates from the final target parent', () => {
    const document = createMoveDocument();
    const result = planCanvasMove({
      document,
      geometry: geometryFor(document),
      nodeIds: ['a'],
      rects: [{ height: 40, id: 'a', width: 80, x: 260, y: 110 }],
      target: { index: 0, parentId: 'right', placement: 'free-position', targetNodeId: 'right' }
    });

    expect(result).toMatchObject({ changed: true, ok: true });
    if (!result.ok) return;
    expect(result.plan).toMatchObject({ insertionIndex: 0, nodeIds: ['a'], parentId: 'right', targetLayoutMode: 'free' });
    expect(result.plan.layoutPatches?.a).toMatchObject({ position: 'absolute', x: 60, y: 90 });
  });

  it('uses after-detach indices and preserves document order for a batch move', () => {
    const document = createMoveDocument();
    document.elements.left.children = ['a', 'b'];
    const result = planCanvasMove({
      document,
      geometry: geometryFor(document),
      nodeIds: ['b', 'a'],
      rects: [
        { height: 40, id: 'a', width: 80, x: 20, y: 30 },
        { height: 40, id: 'b', width: 80, x: 20, y: 80 }
      ],
      target: { index: 0, parentId: 'right', placement: 'inside', targetNodeId: 'right' }
    });

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.plan.nodeIds).toEqual(['a', 'b']);
  });

  it('recognizes same parent, order and coordinates as a no-op', () => {
    const document = createMoveDocument();
    const result = planCanvasMove({
      document,
      geometry: geometryFor(document),
      nodeIds: ['a'],
      rects: [{ height: 40, id: 'a', width: 80, x: 20, y: 30 }],
      target: { index: 0, parentId: 'left', placement: 'free-position', targetNodeId: 'left' }
    });

    expect(result).toMatchObject({ changed: false, diagnostics: [], ok: true });
  });

  it('keeps one active anchor on each axis when moving into constraints layout', () => {
    const document = createMoveDocument();
    document.elements.a.layout = { ...document.elements.a.layout, constraints: { right: 70 } };
    document.elements.right.layout = { ...document.elements.right.layout, layoutMode: 'constraints' };
    const sourceGeometry = geometryFor(document);
    const result = planCanvasMove({
      document,
      geometry: { ...sourceGeometry, layoutModes: { ...sourceGeometry.layoutModes, right: 'constraints' } },
      nodeIds: ['a'],
      rects: [{ height: 40, id: 'a', width: 80, x: 260, y: 110 }],
      target: { index: 0, parentId: 'right', placement: 'free-position', targetNodeId: 'right' }
    });

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.plan.layoutPatches?.a.constraints).toMatchObject({ right: 20, top: 90 });
  });

  it('removes absolute coordinates when moving into flex and rejects cycles', () => {
    const document = createMoveDocument();
    document.elements.right.layout = { display: 'flex', layoutMode: 'flex' };
    const sourceGeometry = geometryFor(document);
    const geometry: CanvasMoveGeometry = { ...sourceGeometry, layoutModes: { ...sourceGeometry.layoutModes, right: 'flex' } };
    const flexResult = planCanvasMove({
      document,
      geometry,
      nodeIds: ['a'],
      rects: [{ height: 40, id: 'a', width: 80, x: 260, y: 110 }],
      target: { index: 0, parentId: 'right', placement: 'inside', targetNodeId: 'right' }
    });
    expect(flexResult.ok).toBe(true);
    if (flexResult.ok) expect(flexResult.plan.layoutPatches?.a).toEqual({ constraints: undefined, position: undefined, x: undefined, y: undefined });

    const cycleResult = planCanvasMove({
      document,
      geometry,
      nodeIds: ['left'],
      rects: [{ height: 200, id: 'left', width: 160, x: 10, y: 20 }],
      target: { index: 0, parentId: 'a', placement: 'inside', targetNodeId: 'a' }
    });
    expect(cycleResult).toMatchObject({ changed: false, ok: false });
    expect(cycleResult.diagnostics[0]).toContain('cycle');
  });

  it('hits the correct line and order when reordering a wrapped flex parent', () => {
    const document = createMoveDocument();
    document.elements.left.children = ['a', 'b', 'c'];
    document.elements.left.layout = { display: 'flex', flexDirection: 'row', flexWrap: 'wrap', layoutMode: 'flex' };
    document.elements.c = { ...document.elements.b, id: 'c', parentId: 'left', type: 'test.leaf' };
    const target = mockElement({ left: 0, top: 60, width: 80, height: 40 }, false, 'c');
    const result = resolveCanvasMoveTarget({
      clientX: 150,
      clientY: 10,
      document,
      hitElement: target,
      manifests: latestComponentRegistry,
      movingNodeIds: ['a'],
      nodeElements: new Map([
        ['b', mockElement({ left: 100, top: 0, width: 80, height: 40 }, false, 'b')],
        ['c', target]
      ]),
      rootElement: null,
      rootId: 'root'
    });

    expect(result).toMatchObject({ index: 1, parentId: 'left', placement: 'before', targetNodeId: 'c' });
  });

  it('resizes flex items without persisting absolute placement fields', () => {
    expect(createFlexResizeLayoutPatch({ position: 'absolute', x: 12, y: 18, constraints: { left: 12 }, width: 80, height: 40 }, 120, 60)).toEqual({ width: 120, height: 60 });
  });

  it('creates a two-dimensional grid cell patch for an inserted node', () => {
    const document = createMoveDocument();
    document.elements.right.children = ['b'];
    document.elements.b.parentId = 'right';
    document.elements.right.layout = { display: 'grid', layoutMode: 'grid', columns: 2 };
    const sourceGeometry = geometryFor(document);
    const result = planCanvasMove({
      document,
      geometry: { ...sourceGeometry, layoutModes: { ...sourceGeometry.layoutModes, right: 'grid' } },
      nodeIds: ['a'],
      rects: [{ height: 40, id: 'a', width: 80, x: 260, y: 110 }],
      target: { index: 1, parentId: 'right', placement: 'inside', targetNodeId: 'right' }
    });

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.plan.layoutPatches?.a).toMatchObject({ gridColumn: 2, gridColumnSpan: 1, gridRow: 1, gridRowSpan: 1 });
  });

  it('rejects an invalid grid span before producing a move plan', () => {
    const document = createMoveDocument();
    document.elements.a.layout = { ...document.elements.a.layout, gridColumnSpan: 0 };
    document.elements.right.layout = { display: 'grid', layoutMode: 'grid', columns: 2 };
    const sourceGeometry = geometryFor(document);
    const result = planCanvasMove({
      document,
      geometry: { ...sourceGeometry, layoutModes: { ...sourceGeometry.layoutModes, right: 'grid' } },
      nodeIds: ['a'],
      rects: [{ height: 40, id: 'a', width: 80, x: 260, y: 110 }],
      target: { index: 0, parentId: 'right', placement: 'inside', targetNodeId: 'right' }
    });

    expect(result).toMatchObject({ ok: false, changed: false });
    expect(result.diagnostics[0]).toContain('Invalid grid span');
  });

  it('keeps grid rows independent from columns when planning a move', () => {
    const document = createMoveDocument();
    document.elements.c = { ...document.elements.b, id: 'c', parentId: 'right', type: 'test.leaf' };
    document.elements.right.children = ['b', 'c'];
    document.elements.b.parentId = 'right';
    document.elements.right.layout = { display: 'grid', layoutMode: 'grid', columns: 2, rows: 3 };
    const sourceGeometry = geometryFor(document);
    const result = planCanvasMove({
      document,
      geometry: { ...sourceGeometry, layoutModes: { ...sourceGeometry.layoutModes, right: 'grid' } },
      nodeIds: ['a'],
      rects: [{ height: 40, id: 'a', width: 80, x: 260, y: 110 }],
      target: { index: 2, parentId: 'right', placement: 'inside', targetNodeId: 'right' }
    });

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.plan.layoutPatches?.a).toMatchObject({ gridColumn: 1, gridColumnSpan: 1, gridRow: 2, gridRowSpan: 1 });
  });

  it('treats every point inside the root artboard as a free-position target', () => {
    const document = createMoveDocument();
    const root = mockElement({ left: 100, top: 80, width: 500, height: 400 }, true);
    const points = [
      [100, 80], [599, 80], [100, 479], [599, 479], [350, 280]
    ];

    for (const [clientX, clientY] of points) {
      const result = resolveCanvasMoveTarget({
        clientX,
        clientY,
        document,
        hitElement: root,
        manifests: latestComponentRegistry,
        movingNodeIds: ['a'],
        nodeElements: new Map(),
        rootElement: root,
        rootId: 'root'
      });
      expect(result).toMatchObject({ parentId: 'root', placement: 'free-position', targetNodeId: 'root' });
    }
  });

  it('falls back from a blank world hit to the root artboard', () => {
    const document = createMoveDocument();
    const root = mockElement({ left: 100, top: 80, width: 500, height: 400 });
    const blankWorld = mockElement({ left: 0, top: 0, width: 1, height: 1 });
    const result = resolveCanvasMoveTarget({
      clientX: 350,
      clientY: 280,
      document,
      hitElement: blankWorld,
      manifests: latestComponentRegistry,
      movingNodeIds: ['a'],
      nodeElements: new Map(),
      rootElement: root,
      rootId: 'root'
    });

    expect(result).toMatchObject({ parentId: 'root', placement: 'free-position', targetNodeId: 'root' });
  });

  it('rejects a point outside the artboard without creating a target', () => {
    const document = createMoveDocument();
    const root = mockElement({ left: 100, top: 80, width: 500, height: 400 });
    expect(resolveCanvasMoveTarget({
      clientX: 50,
      clientY: 50,
      document,
      hitElement: mockElement({ left: 0, top: 0, width: 1, height: 1 }),
      manifests: latestComponentRegistry,
      movingNodeIds: ['a'],
      nodeElements: new Map(),
      rootElement: root,
      rootId: 'root'
    })).toBeNull();
  });
});

function mockElement(rect: { left: number; top: number; width: number; height: number }, isArtboard = false, nodeId?: string): HTMLElement {
  const element = {
    closest: (selector: string) => isArtboard && selector === '[data-canvas-artboard="true"]' || nodeId && selector === '[data-node-id]' ? element : null,
    dataset: nodeId ? { nodeId } : {},
    getBoundingClientRect: () => ({ bottom: rect.top + rect.height, height: rect.height, left: rect.left, right: rect.left + rect.width, top: rect.top, width: rect.width, x: rect.left, y: rect.top })
  } as unknown as HTMLElement;
  return element;
}

function geometryFor(document: DesignerDocument): CanvasMoveGeometry {
  return {
    layoutModes: { a: 'free', b: 'free', left: 'free', right: 'free', root: 'free' },
    nodes: document.elements,
    rects: {
      a: { height: 40, id: 'a', width: 80, x: 20, y: 30 },
      b: { height: 40, id: 'b', width: 80, x: 20, y: 80 },
      left: { height: 200, id: 'left', width: 160, x: 10, y: 20 },
      right: { height: 200, id: 'right', width: 160, x: 200, y: 20 },
      root: { height: 400, id: 'root', width: 500, x: 0, y: 0 }
    }
  };
}

function createMoveDocument(): DesignerDocument {
  const node = (value: Partial<DesignerDocumentNode> & Pick<DesignerDocumentNode, 'id' | 'type'>): DesignerDocumentNode => ({
    children: [], events: [], layout: {}, parentId: null, props: {}, ...value
  });
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'move-planner', elements: {
      a: node({ id: 'a', layout: { height: 40, position: 'absolute', width: 80, x: 10, y: 10 }, parentId: 'left', type: 'text.paragraph' }),
      b: node({ id: 'b', layout: { height: 40, position: 'absolute', width: 80, x: 10, y: 60 }, parentId: 'left', type: 'text.paragraph' }),
      left: node({ children: ['a'], id: 'left', layout: { height: 200, layoutMode: 'free', position: 'absolute', width: 160, x: 10, y: 20 }, parentId: 'root', type: 'layout.container' }),
      right: node({ children: [], id: 'right', layout: { height: 200, layoutMode: 'free', position: 'absolute', width: 160, x: 200, y: 20 }, parentId: 'root', type: 'layout.container' }),
      root: node({ children: ['left', 'right'], id: 'root', layout: { height: 400, layoutMode: 'free', width: 500 }, type: 'layout.page' })
    }, metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }], pageType: 'standard', permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}
