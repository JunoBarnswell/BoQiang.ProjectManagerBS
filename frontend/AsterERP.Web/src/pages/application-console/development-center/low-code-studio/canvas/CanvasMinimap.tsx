import { Map as MapIcon, PanelTopClose, PanelTopOpen } from 'lucide-react';
import { useRef, useState, type KeyboardEvent as ReactKeyboardEvent, type PointerEvent as ReactPointerEvent, type WheelEvent as ReactWheelEvent } from 'react';

import type { CanvasViewportController } from './CanvasViewportController';
import type { CanvasPoint, CanvasRect, CanvasViewport } from './coordinateSystem';

interface CanvasMinimapProps {
  board: CanvasRect;
  controller: CanvasViewportController;
  stage: { height: number; width: number };
  text: (key: string) => string;
  viewport: CanvasViewport;
}

const MINI_WIDTH = 204;
const MINI_HEIGHT = 126;
const WORLD_GUTTER = 56;

type WorldBounds = CanvasRect;
interface MinimapDragState { distance: number; kind: 'background' | 'viewport'; last: CanvasPoint; moved: boolean; pointerId: number }

export function CanvasMinimap({ board, controller, stage, text, viewport }: CanvasMinimapProps) {
  const [collapsed, setCollapsed] = useState(false);
  const visible = visibleDocumentBounds(controller, stage);
  const world = getWorldBounds(board, visible);
  const scale = Math.min(MINI_WIDTH / world.width, MINI_HEIGHT / world.height);
  const boardFrame = toMiniRect(board, world, scale);
  const viewportFrame = toMiniRect(visible, world, scale);
  const dragRef = useRef<MinimapDragState | null>(null);

  const toDocumentPoint = (event: ReactPointerEvent<HTMLDivElement>, element: HTMLDivElement): CanvasPoint => {
    const rect = element.getBoundingClientRect();
    return {
      x: world.x + clamp((event.clientX - rect.left) / scale, 0, world.width),
      y: world.y + clamp((event.clientY - rect.top) / scale, 0, world.height)
    };
  };
  const centerAtPointer = (event: ReactPointerEvent<HTMLDivElement>) => {
    event.stopPropagation();
    controller.centerOn(toDocumentPoint(event, event.currentTarget));
  };
  const panFromMiniDelta = (delta: CanvasPoint) => {
    const zoom = controller.viewport.zoom;
    controller.panBy({ x: delta.x / scale * zoom, y: delta.y / scale * zoom });
  };
  const onKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    const step = Math.max(48, Math.min(board.width, board.height) / 8);
    const delta = event.key === 'ArrowLeft' ? { x: -step * viewport.zoom, y: 0 }
      : event.key === 'ArrowRight' ? { x: step * viewport.zoom, y: 0 }
        : event.key === 'ArrowUp' ? { x: 0, y: -step * viewport.zoom }
          : event.key === 'ArrowDown' ? { x: 0, y: step * viewport.zoom } : null;
    if (!delta) return;
    event.preventDefault();
    controller.panBy(delta);
  };
  const onWheel = (event: ReactWheelEvent<HTMLDivElement>) => {
    event.preventDefault();
    event.stopPropagation();
    controller.zoomBy(event.deltaY < 0 ? 0.1 : -0.1);
  };

  return <section aria-label={text('minimap')} className={`page-studio__minimap ${collapsed ? 'page-studio__minimap--collapsed' : ''}`} data-canvas-interaction-control="true" onPointerDown={(event) => event.stopPropagation()}>
    <header className="page-studio__minimap-header">
      <span className="flex items-center gap-1.5"><MapIcon aria-hidden="true" className="h-3.5 w-3.5" />{text('minimap')}</span>
      <span className="page-studio__minimap-actions"><span aria-label={text('canvasZoom')} className="page-studio__zoom-badge">{Math.round(viewport.zoom * 100)}%</span><button aria-label={text(collapsed ? 'expandMinimap' : 'collapseMinimap')} className="page-studio__minimap-toggle" type="button" onClick={() => setCollapsed((current) => !current)}>{collapsed ? <PanelTopOpen aria-hidden="true" className="h-3.5 w-3.5" /> : <PanelTopClose aria-hidden="true" className="h-3.5 w-3.5" />}</button></span>
    </header>
    {!collapsed ? <div aria-description={text('minimapDescription')} aria-label={text('minimapNavigation')} className="page-studio__minimap-navigation" role="application" style={{ height: MINI_HEIGHT, width: MINI_WIDTH }} tabIndex={0} onKeyDown={onKeyDown} onWheel={onWheel} onPointerDown={(event) => { if (event.button !== 0) return; event.preventDefault(); event.stopPropagation(); dragRef.current = { distance: 0, kind: 'background', last: { x: event.clientX, y: event.clientY }, moved: false, pointerId: event.pointerId }; event.currentTarget.setPointerCapture(event.pointerId); }} onPointerMove={(event) => { const drag = dragRef.current; if (!drag || drag.kind !== 'background' || drag.pointerId !== event.pointerId) return; const delta = { x: event.clientX - drag.last.x, y: event.clientY - drag.last.y }; const distance = Math.hypot(delta.x, delta.y); if (distance > 0) { drag.distance += distance; drag.moved = drag.distance >= 2; drag.last = { x: event.clientX, y: event.clientY }; panFromMiniDelta(delta); } }} onPointerUp={(event) => { const drag = dragRef.current; if (drag?.kind === 'background' && drag.pointerId === event.pointerId && !drag.moved) centerAtPointer(event); dragRef.current = null; event.currentTarget.releasePointerCapture?.(event.pointerId); }} onPointerCancel={() => { dragRef.current = null; }}>
      <div aria-hidden="true" className="page-studio__minimap-board" style={{ height: Math.max(2, boardFrame.height), left: boardFrame.x, top: boardFrame.y, width: Math.max(2, boardFrame.width) }} />
      <div aria-label={text('visibleViewport')} aria-valuemax={400} aria-valuemin={10} aria-valuenow={Math.round(viewport.zoom * 100)} className="page-studio__minimap-viewport" role="slider" style={{ height: Math.max(10, viewportFrame.height), left: viewportFrame.x, top: viewportFrame.y, width: Math.max(10, viewportFrame.width) }} tabIndex={-1} onPointerDown={(event) => {
        if (event.button !== 0) return;
        event.preventDefault();
        event.stopPropagation();
        dragRef.current = { distance: 0, kind: 'viewport', last: { x: event.clientX, y: event.clientY }, moved: false, pointerId: event.pointerId };
        event.currentTarget.setPointerCapture(event.pointerId);
      }} onPointerMove={(event) => {
        const drag = dragRef.current;
        if (!drag || drag.kind !== 'viewport' || drag.pointerId !== event.pointerId) return;
        const delta = { x: event.clientX - drag.last.x, y: event.clientY - drag.last.y };
        if (Math.hypot(delta.x, delta.y) === 0) return;
        drag.distance += Math.hypot(delta.x, delta.y);
        drag.last = { x: event.clientX, y: event.clientY };
        drag.moved = true;
        panFromMiniDelta(delta);
      }} onPointerUp={(event) => { if (dragRef.current?.pointerId === event.pointerId) dragRef.current = null; event.currentTarget.releasePointerCapture?.(event.pointerId); }} onPointerCancel={() => { dragRef.current = null; }} />
    </div> : null}
  </section>;
}

function getWorldBounds(board: CanvasRect, visible: CanvasRect): WorldBounds {
  const minX = Math.min(board.x, visible.x) - WORLD_GUTTER;
  const minY = Math.min(board.y, visible.y) - WORLD_GUTTER;
  const maxX = Math.max(board.x + board.width, visible.x + visible.width) + WORLD_GUTTER;
  const maxY = Math.max(board.y + board.height, visible.y + visible.height) + WORLD_GUTTER;
  return { height: Math.max(1, maxY - minY), width: Math.max(1, maxX - minX), x: minX, y: minY };
}

function visibleDocumentBounds(controller: CanvasViewportController, stage: { height: number; width: number }): CanvasRect {
  const topLeft = controller.screenToDocument({ x: 0, y: 0 });
  const bottomRight = controller.screenToDocument({ x: stage.width, y: stage.height });
  return {
    height: Math.max(1, bottomRight.y - topLeft.y),
    width: Math.max(1, bottomRight.x - topLeft.x),
    x: topLeft.x,
    y: topLeft.y
  };
}

function toMiniRect(rect: CanvasRect, world: WorldBounds, scale: number): CanvasRect {
  return { height: rect.height * scale, width: rect.width * scale, x: (rect.x - world.x) * scale, y: (rect.y - world.y) * scale };
}

function clamp(value: number, min: number, max: number): number { return Math.max(min, Math.min(max, value)); }
