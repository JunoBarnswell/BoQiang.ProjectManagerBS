// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import type { DesignerEditorSession } from '../document/DesignerEditorSession';
import { DesignerEditorSessionStore } from '../session/DesignerEditorSessionStore';

import { CanvasMinimap } from './CanvasMinimap';
import { CanvasViewportController } from './CanvasViewportController';

afterEach(cleanup);

describe('CanvasMinimap', () => {
  it('uses the same content-following drag direction as the main canvas', () => {
    const store = createSessionStore();
    const controller = new CanvasViewportController(store);
    controller.configure({ content: { height: 800, width: 1200, x: 0, y: 0 }, stage: { height: 600, width: 800 } });
    render(<CanvasMinimap board={{ height: 800, width: 1200, x: 0, y: 0 }} controller={controller} stage={{ height: 600, width: 800 }} text={(key) => key} viewport={controller.viewport} />);
    const viewport = screen.getByRole('slider', { name: 'visibleViewport' });
    const captured = new Set<number>();
    Object.defineProperties(viewport, {
      releasePointerCapture: { configurable: true, value: vi.fn((pointerId: number) => captured.delete(pointerId)) },
      setPointerCapture: { configurable: true, value: vi.fn((pointerId: number) => captured.add(pointerId)) }
    });

    fireEvent.pointerDown(viewport, { button: 0, clientX: 50, clientY: 50, pointerId: 17 });
    fireEvent.pointerMove(viewport, { clientX: 70, clientY: 50, pointerId: 17 });
    fireEvent.pointerUp(viewport, { clientX: 70, clientY: 50, pointerId: 17 });

    expect(store.getSnapshot().viewport.pan?.x).toBeGreaterThan(0);
  });

  it('keeps background click centering and wheel zoom on the shared controller', () => {
    const store = createSessionStore();
    const controller = new CanvasViewportController(store);
    controller.configure({ content: { height: 800, width: 1200, x: 0, y: 0 }, stage: { height: 600, width: 800 } });
    render(<CanvasMinimap board={{ height: 800, width: 1200, x: 0, y: 0 }} controller={controller} stage={{ height: 600, width: 800 }} text={(key) => key} viewport={controller.viewport} />);
    const navigation = screen.getByRole('application', { name: 'minimapNavigation' });
    Object.defineProperties(navigation, {
      getBoundingClientRect: { configurable: true, value: () => ({ bottom: 126, height: 126, left: 0, right: 204, top: 0, width: 204, x: 0, y: 0, toJSON: () => ({}) }) },
      releasePointerCapture: { configurable: true, value: vi.fn() },
      setPointerCapture: { configurable: true, value: vi.fn() }
    });

    fireEvent.pointerDown(navigation, { button: 0, clientX: 160, clientY: 63, pointerId: 18 });
    fireEvent.pointerUp(navigation, { clientX: 160, clientY: 63, pointerId: 18 });
    const centeredPan = store.getSnapshot().viewport.pan?.x;
    fireEvent.wheel(navigation, { deltaY: -1 });

    expect(centeredPan).not.toBe(0);
    expect(store.getSnapshot().viewport.zoom).toBeGreaterThan(1);
  });
});

function createSessionStore(): DesignerEditorSessionStore {
  const session: DesignerEditorSession = {
    anchorNodeId: null,
    canvas: { device: null, gridSize: 8, gridVisible: true, guides: [], minimapVisible: true, rulersVisible: false, snapThreshold: 6, tool: 'select' },
    documentId: 'minimap-test', panelState: {}, primaryNodeId: null, selectedNodeIds: [], sessionId: 'minimap-test', transactionId: null,
    viewport: { height: 600, pan: { x: 0, y: 0 }, width: 800, zoom: 1 }
  };
  return new DesignerEditorSessionStore(session);
}
