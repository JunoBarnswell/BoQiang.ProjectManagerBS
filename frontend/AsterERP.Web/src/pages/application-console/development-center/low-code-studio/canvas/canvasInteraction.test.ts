import { describe, expect, it } from 'vitest';

import { clientToStageScreen, createCanvasFrame, createDeviceCanvasFrame, documentToLocal, documentToScreen, fitViewport, fitWidthViewport, screenToDocument, screenToWorld, zoomAtScreenPoint } from './coordinateSystem';
import { beginPointerTransaction, finishPointerTransaction, updatePointerTransaction } from './pointerTransaction';
import { createSelection, selectByMarquee, toggleSelection } from './selectionModel';
import { snapRect, snapRectWithOptions } from './snapping';
import { createCanvasSpatialIndex } from './spatialIndex';

describe('latest canvas interaction model', () => {
  it('round-trips screen/world/document/local without zoom drift', () => {
    const viewport = { zoom: 1.25, pan: { x: 30, y: -10 } };
    const documentPoint = { x: 180, y: 90 };
    const screen = documentToScreen(documentPoint, viewport, { x: 100, y: 50 }, { x: 20, y: 10 });
    expect(screenToDocument(screen, viewport, { x: 100, y: 50 }, { x: 20, y: 10 })).toEqual(documentPoint);
    expect(documentToLocal(documentPoint, { x: 30, y: 20 })).toEqual({ x: 150, y: 70 });
  });

  it('round-trips device frame coordinates including border, browser bar, safe area, zoom, pan and scroll', () => {
    const frame = createCanvasFrame({ origin: { x: 80, y: 40 }, border: { top: 8, right: 8, bottom: 8, left: 8 }, browserBar: { top: 24, right: 0, bottom: 0, left: 0 }, safeArea: { top: 47, right: 0, bottom: 34, left: 0 } });
    const viewport = { zoom: 2, pan: { x: 15, y: -5 } };
    const stagePoint = clientToStageScreen({ x: 400, y: 300 }, { left: 100, top: 200 }, { x: 60, y: 20 });
    const documentPoint = screenToDocument(stagePoint, viewport, frame);
    expect(documentToScreen(documentPoint, viewport, frame)).toEqual(stagePoint);
    expect(documentPoint).toEqual({ x: 124.5, y: -36.5 });
  });

  it('keeps the cursor world point fixed while zooming', () => {
    const before = { zoom: 1, pan: { x: 0, y: 0 } };
    const next = zoomAtScreenPoint(before, 2, { x: 400, y: 200 }, { x: 0, y: 0 });
    expect(screenToDocument({ x: 400, y: 200 }, next, { x: 0, y: 0 }, { x: 0, y: 0 })).toEqual({ x: 400, y: 200 });
    expect(fitViewport({ width: 800, height: 600 }, { x: 0, y: 0, width: 400, height: 300 }).zoom).toBe(1.7866666666666666);
    expect(fitWidthViewport({ width: 800, height: 600 }, { x: 0, y: 0, width: 400, height: 300 }, 16)).toEqual({ zoom: 1.92, pan: { x: 16, y: 16 } });
  });

  it('uses direct stage coordinates after removing the scroll-container inset', () => {
    expect(clientToStageScreen({ x: 124, y: 224 }, { left: 100, top: 200 })).toEqual({ x: 24, y: 24 });
    expect(clientToStageScreen({ x: 124, y: 224 }, { left: 100, top: 200 }, { x: 48, y: 32 })).toEqual({ x: 72, y: 56 });
  });

  it('keeps world coordinates stable when the stage is scrolled at a non-unit zoom', () => {
    const stagePoint = clientToStageScreen({ x: 364, y: 296 }, { left: 100, top: 200 }, { x: 120, y: 80 });
    expect(screenToWorld(stagePoint, { zoom: 2, pan: { x: 20, y: -10 } }, { x: 0, y: 0 })).toEqual({ x: 182, y: 93 });
  });

  it.each([0.48, 0.95, 1, 2])('round-trips transformed points at %s zoom', (zoom) => {
    const viewport = { zoom, pan: { x: -137, y: 84 } };
    const origin = { x: 24, y: 18 };
    const documentPoint = { x: 412.5, y: 287.25 };
    const screen = documentToScreen(documentPoint, viewport, { x: 0, y: 0 }, origin);
    expect(screenToDocument(screen, viewport, { x: 0, y: 0 }, origin)).toEqual(documentPoint);
  });

  it.each([0.1, 0.2, 0.4, 0.6, 0.8, 1, 1.25, 1.5, 2, 4])('round-trips device document coordinates deterministically at zoom %s', (zoom) => {
    const frame = createDeviceCanvasFrame(
      { height: 900, width: 1280 },
      { browserBar: { bottom: 0, top: 24 }, height: 844, safeArea: { bottom: 34, left: 0, right: 0, top: 47 }, width: 390 }
    );
    const viewport = { zoom, pan: { x: -37, y: 22 } };
    const point = { x: 123.5, y: 456.25 };
    const screen = documentToScreen(point, viewport, frame);
    expect(screenToDocument(screen, viewport, frame).x).toBeCloseTo(point.x, 9);
    expect(screenToDocument(screen, viewport, frame).y).toBeCloseTo(point.y, 9);
    expect(screenToDocument(screen, zoomAtScreenPoint(viewport, zoom, screen, frame), frame).x).toBeCloseTo(point.x, 9);
    expect(screenToDocument(screen, zoomAtScreenPoint(viewport, zoom, screen, frame), frame).y).toBeCloseTo(point.y, 9);
  });

  it.each([{ height: 844, width: 390 }, { height: 390, width: 844 }])('keeps device frame origin deterministic for orientation %#', (device) => {
    const frame = createDeviceCanvasFrame({ height: 900, width: 1280 }, device);
    expect(frame.origin).toEqual({ x: (1280 - device.width) / 2, y: 36 });
  });

  it('updates transiently and emits the final move only at pointer-up', () => {
    const tx = beginPointerTransaction('move', 7, { x: 10, y: 20 }, [{ id: 'a', rect: { id: 'a', x: 0, y: 0, width: 100, height: 40 } }]);
    expect(updatePointerTransaction(tx, { x: 30, y: 50 }).rects[0]).toMatchObject({ x: 20, y: 30 });
    expect(finishPointerTransaction(tx, { x: 40, y: 70 }).rects[0]).toMatchObject({ x: 30, y: 50 });
  });

  it('selects marquee intersections and snaps to grid/peer edges', () => {
    const selection = selectByMarquee([{ id: 'a', x: 10, y: 10, width: 20, height: 20 }, { id: 'b', x: 100, y: 100, width: 20, height: 20 }], { x: 0, y: 0, width: 40, height: 40 }, false, createSelection());
    expect(selection.selectedNodeIds).toEqual(['a']);
    expect(snapRect({ x: 43, y: 10, width: 20, height: 20 }, [{ id: 'b', x: 64, y: 100, width: 20, height: 20 }], 8, 4).point.x).toBe(44);
  });

  it('keeps the anchor inside the selected set after additive deselection', () => {
    expect(toggleSelection({ selectedNodeIds: ['a', 'b'], primaryNodeId: 'b', anchorNodeId: 'a' }, 'a', true)).toEqual({ selectedNodeIds: ['b'], primaryNodeId: 'b', anchorNodeId: 'b' });
  });

  it('resizes from every edge using the initial snapshot and respects minimums', () => {
    const tx = beginPointerTransaction('resize', 9, { x: 100, y: 100 }, [{ id: 'a', rect: { id: 'a', x: 10, y: 20, width: 100, height: 80 } }], 'northwest');
    expect(updatePointerTransaction(tx, { x: 180, y: 180 }, { minWidth: 40, minHeight: 30 }).rects[0]).toEqual({ id: 'a', x: 70, y: 70, width: 40, height: 30 });
  });

  it('reports peer guide sources and can disable snapping', () => {
    const result = snapRectWithOptions({ x: 43, y: 10, width: 20, height: 20 }, [{ id: 'peer', x: 64, y: 100, width: 20, height: 20 }], { gridSize: 8, threshold: 4 });
    expect(result.guides.some((guide) => guide.sourceId === 'peer')).toBe(true);
    expect(snapRectWithOptions({ x: 43, y: 10, width: 20, height: 20 }, [], { enabled: false }).guides).toEqual([]);
  });

  it('queries only nearby peers through the canvas spatial index', () => {
    const index = createCanvasSpatialIndex([
      { id: 'near', x: 110, y: 100, width: 20, height: 20 },
      { id: 'far', x: 1_000, y: 1_000, width: 20, height: 20 }
    ]);
    expect(index.query({ x: 100, y: 100, width: 20, height: 20 }, 8).map((rect) => rect.id)).toEqual(['near']);
    expect(index.size).toBe(2);
  });

  it('measures nearby peer edges, centerlines, and axis gaps from the indexed candidates', () => {
    const index = createCanvasSpatialIndex([{ id: 'near', x: 140, y: 100, width: 20, height: 20 }]);
    expect(index.measure({ x: 100, y: 100, width: 20, height: 20 }, 24)).toEqual([expect.objectContaining({
      centers: { x: 150, y: 110 },
      edges: { left: 140, right: 160, top: 100, bottom: 120 },
      gap: { x: 20, y: 0 }
    })]);
  });
});
