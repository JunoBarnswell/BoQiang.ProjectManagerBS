import { describe, expect, it } from 'vitest';

import type { DesignerEditorSession } from '../document/DesignerEditorSession';
import { DesignerEditorSessionStore } from '../session/DesignerEditorSessionStore';

import { CanvasViewportController, clampViewport } from './CanvasViewportController';
import { screenToWorld } from './coordinateSystem';

describe('CanvasViewportController', () => {
  it('keeps the requested screen anchor stable for every zoom entry point', () => {
    const store = createSessionStore();
    const controller = new CanvasViewportController(store);
    controller.configure({ content: { height: 800, width: 1200, x: 0, y: 0 }, stage: { height: 600, width: 800 } });
    const anchor = { x: 400, y: 300 };
    const before = screenToWorld(anchor, controller.viewport, { x: 0, y: 0 });

    controller.setZoom(2, anchor);

    expect(screenToWorld(anchor, controller.viewport, { x: 0, y: 0 })).toEqual(before);
    expect(controller.viewport.zoom).toBe(2);
  });

  it('shares bounded pan and explicit fit width, page and selection actions', () => {
    const store = createSessionStore();
    const controller = new CanvasViewportController(store);
    const selection = { height: 100, width: 200, x: 300, y: 200 };
    controller.configure({ content: { height: 800, width: 1200, x: 0, y: 0 }, selection, stage: { height: 600, width: 800 } });

    const width = controller.fitWidth();
    expect(width.zoom).toBeCloseTo(0.6267, 3);
    expect(controller.fitPage().zoom).toBeCloseTo(0.6267, 3);
    expect(controller.fitSelection().zoom).toBeCloseTo(3.68, 2);

    controller.panBy({ x: 100_000, y: 100_000 });
    expect(controller.viewport).toEqual(clampViewport(controller.viewport, { height: 600, width: 800 }, { height: 800, width: 1200, x: 0, y: 0 }));
  });

  it('preserves pan and zoom while a document transaction changes content geometry', () => {
    const store = createSessionStore();
    const controller = new CanvasViewportController(store);
    controller.configure({ content: { height: 800, width: 1200, x: 0, y: 0 }, stage: { height: 600, width: 800 } });
    controller.setZoom(1.5, { x: 400, y: 300 });
    controller.panBy({ x: -120, y: 45 });
    const before = controller.viewport;
    controller.beginInteraction();
    controller.configure({ content: { height: 1600, width: 2400, x: 0, y: 0 } });
    controller.preserveViewportDuring(() => store.patch({ viewport: { width: 2400, height: 1600 } }));
    controller.endInteraction();
    expect(controller.viewport).toEqual(before);
  });

  it('does not notify the session store for an unchanged viewport', () => {
    const store = createSessionStore();
    const controller = new CanvasViewportController(store);
    let notifications = 0;
    store.subscribe(() => { notifications += 1; });
    controller.panBy({ x: 0, y: 0 });
    expect(notifications).toBe(0);
  });

  it('derives the device frame origin and exposes document conversion through the controller', () => {
    const store = createSessionStore();
    store.patch({ canvas: { device: { browserBar: { bottom: 0, top: 24 }, breakpointId: 'mobile', height: 844, id: 'phone', orientation: 'portrait', pixelRatio: 3, safeArea: { bottom: 34, left: 0, right: 0, top: 47 }, width: 390 } } });
    const controller = new CanvasViewportController(store);
    controller.configure({ content: { height: 844, width: 390, x: 0, y: 0 }, stage: { height: 900, width: 800 } });
    expect(controller.coordinateFrame).toMatchObject({ origin: { x: 205, y: 36 }, border: { top: 8 }, browserBar: { top: 24 }, safeArea: { top: 47 } });
    expect(controller.screenToDocument(controller.documentToScreen({ x: 12, y: 34 }))).toEqual({ x: 12, y: 34 });
  });
});

function createSessionStore(): DesignerEditorSessionStore {
  const session: DesignerEditorSession = {
    anchorNodeId: null,
    canvas: { device: null, gridSize: 8, gridVisible: true, guides: [], minimapVisible: true, rulersVisible: false, snapThreshold: 6, tool: 'select' },
    documentId: 'viewport-test',
    panelState: {},
    primaryNodeId: null,
    selectedNodeIds: [],
    sessionId: 'viewport-test',
    transactionId: null,
    viewport: { height: 600, pan: { x: 0, y: 0 }, width: 800, zoom: 1 }
  };
  return new DesignerEditorSessionStore(session);
}
