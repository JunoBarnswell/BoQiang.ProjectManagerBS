export interface CanvasPoint { x: number; y: number }
export interface CanvasSize { width: number; height: number }
export interface CanvasRect extends CanvasPoint, CanvasSize { id?: string }
export interface CanvasViewport { zoom: number; pan: CanvasPoint }
export interface CanvasInsets { top: number; right: number; bottom: number; left: number }
export interface CanvasDocumentOrigin extends CanvasPoint {
  border?: CanvasInsets | number;
  browserBar?: CanvasInsets | number;
  safeArea?: CanvasInsets | number;
}
export interface CanvasFrameGeometry {
  origin: CanvasPoint;
  border: CanvasInsets;
  browserBar: CanvasInsets;
  safeArea: CanvasInsets;
}
export interface CanvasDeviceFrameInput {
  browserBar?: Partial<CanvasInsets>;
  height: number;
  safeArea?: Partial<CanvasInsets>;
  width: number;
}

export const CANVAS_ZOOM_LIMITS = { min: 0.1, max: 4 } as const;

export function createCanvasFrame(input: Partial<CanvasFrameGeometry> = {}): CanvasFrameGeometry {
  return {
    origin: normalizePoint(input.origin, { x: 0, y: 0 }),
    border: normalizeInsets(input.border),
    browserBar: normalizeInsets(input.browserBar),
    safeArea: normalizeInsets(input.safeArea)
  };
}

export function createDeviceCanvasFrame(stage: CanvasSize, device: CanvasDeviceFrameInput | null | undefined): CanvasFrameGeometry {
  if (!device) return createCanvasFrame();
  const width = finitePositive(device.width, stage.width);
  return createCanvasFrame({
    origin: { x: (finitePositive(stage.width, width) - width) / 2, y: 36 },
    border: { top: 8, right: 8, bottom: 8, left: 8 },
    browserBar: normalizeInsets(device.browserBar),
    safeArea: normalizeInsets(device.safeArea)
  });
}

export function frameDocumentOrigin(frame: CanvasFrameGeometry): CanvasDocumentOrigin {
  return { x: frame.border.left + frame.browserBar.left + frame.safeArea.left, y: frame.border.top + frame.browserBar.top + frame.safeArea.top };
}

export function clientToStageScreen(client: CanvasPoint, stageRect: Pick<DOMRect, 'left' | 'top'>, scroll: CanvasPoint = { x: 0, y: 0 }): CanvasPoint {
  const clientX = Number.isFinite(client.x) ? client.x : 0;
  const clientY = Number.isFinite(client.y) ? client.y : 0;
  const left = Number.isFinite(stageRect.left) ? stageRect.left : 0;
  const top = Number.isFinite(stageRect.top) ? stageRect.top : 0;
  const scrollX = Number.isFinite(scroll.x) ? scroll.x : 0;
  const scrollY = Number.isFinite(scroll.y) ? scroll.y : 0;
  return { x: clientX - left + scrollX, y: clientY - top + scrollY };
}

export function clampZoom(zoom: number): number {
  return Math.min(CANVAS_ZOOM_LIMITS.max, Math.max(CANVAS_ZOOM_LIMITS.min, Number.isFinite(zoom) ? zoom : 1));
}

export function screenToWorld(screen: CanvasPoint, viewport: CanvasViewport, stage: CanvasPoint | CanvasFrameGeometry): CanvasPoint {
  const zoom = clampZoom(viewport.zoom);
  const origin = frameOrigin(stage);
  return { x: (screen.x - origin.x - viewport.pan.x) / zoom, y: (screen.y - origin.y - viewport.pan.y) / zoom };
}

export function worldToScreen(world: CanvasPoint, viewport: CanvasViewport, stage: CanvasPoint | CanvasFrameGeometry): CanvasPoint {
  const zoom = clampZoom(viewport.zoom);
  const origin = frameOrigin(stage);
  return { x: origin.x + viewport.pan.x + world.x * zoom, y: origin.y + viewport.pan.y + world.y * zoom };
}

export function worldToDocument(world: CanvasPoint, origin: CanvasDocumentOrigin | CanvasFrameGeometry): CanvasPoint {
  const offset = documentOrigin(origin);
  return { x: world.x - offset.x, y: world.y - offset.y };
}

export function documentToWorld(document: CanvasPoint, origin: CanvasDocumentOrigin | CanvasFrameGeometry): CanvasPoint {
  const offset = documentOrigin(origin);
  return { x: document.x + offset.x, y: document.y + offset.y };
}

export function documentToLocal(document: CanvasPoint, parentOrigin: CanvasPoint): CanvasPoint {
  return { x: document.x - parentOrigin.x, y: document.y - parentOrigin.y };
}

export function localToDocument(local: CanvasPoint, parentOrigin: CanvasPoint): CanvasPoint {
  return { x: local.x + parentOrigin.x, y: local.y + parentOrigin.y };
}

export function screenToDocument(screen: CanvasPoint, viewport: CanvasViewport, stage: CanvasPoint | CanvasFrameGeometry, origin?: CanvasDocumentOrigin): CanvasPoint {
  return worldToDocument(screenToWorld(screen, viewport, stage), origin ?? (isFrame(stage) ? frameDocumentOrigin(stage) : { x: 0, y: 0 }));
}

export function documentToScreen(document: CanvasPoint, viewport: CanvasViewport, stage: CanvasPoint | CanvasFrameGeometry, origin?: CanvasDocumentOrigin): CanvasPoint {
  return worldToScreen(documentToWorld(document, origin ?? (isFrame(stage) ? frameDocumentOrigin(stage) : { x: 0, y: 0 })), viewport, stage);
}

export function zoomAtScreenPoint(viewport: CanvasViewport, nextZoom: number, screen: CanvasPoint, stage: CanvasPoint | CanvasFrameGeometry): CanvasViewport {
  const before = screenToWorld(screen, viewport, stage);
  const zoom = clampZoom(nextZoom);
  const origin = frameOrigin(stage);
  return { zoom, pan: { x: screen.x - origin.x - before.x * zoom, y: screen.y - origin.y - before.y * zoom } };
}

export function panBy(viewport: CanvasViewport, screenDelta: CanvasPoint): CanvasViewport {
  return { ...viewport, pan: { x: viewport.pan.x + screenDelta.x, y: viewport.pan.y + screenDelta.y } };
}

export function fitViewport(stage: CanvasSize, content: CanvasRect, padding = 32, frame: CanvasPoint | CanvasFrameGeometry = { x: 0, y: 0 }): CanvasViewport {
  const availableWidth = Math.max(1, stage.width - padding * 2);
  const availableHeight = Math.max(1, stage.height - padding * 2);
  const zoom = clampZoom(Math.min(availableWidth / Math.max(1, content.width), availableHeight / Math.max(1, content.height)));
  const origin = frameOrigin(frame);
  const documentOffset = isFrame(frame) ? frameDocumentOrigin(frame) : { x: 0, y: 0 };
  return { zoom, pan: { x: (stage.width - content.width * zoom) / 2 - origin.x - (content.x + documentOffset.x) * zoom, y: (stage.height - content.height * zoom) / 2 - origin.y - (content.y + documentOffset.y) * zoom } };
}

/** Fits the page board horizontally while retaining a deliberate editor margin. */
export function fitWidthViewport(stage: CanvasSize, content: CanvasRect, padding = 16, frame: CanvasPoint | CanvasFrameGeometry = { x: 0, y: 0 }): CanvasViewport {
  const zoom = clampZoom(Math.max(0.1, (Math.max(1, stage.width) - padding * 2) / Math.max(1, content.width)));
  const origin = frameOrigin(frame);
  const documentOffset = isFrame(frame) ? frameDocumentOrigin(frame) : { x: 0, y: 0 };
  return {
    zoom,
    pan: {
      x: (stage.width - content.width * zoom) / 2 - origin.x - (content.x + documentOffset.x) * zoom,
      y: Math.max(padding, (stage.height - content.height * zoom) / 2) - origin.y - (content.y + documentOffset.y) * zoom
    }
  };
}

export function fitSelection(stage: CanvasSize, selection: CanvasRect, padding = 32, frame: CanvasPoint | CanvasFrameGeometry = { x: 0, y: 0 }): CanvasViewport {
  return fitViewport(stage, selection, padding, frame);
}

function frameOrigin(value: CanvasPoint | CanvasFrameGeometry): CanvasPoint { return isFrame(value) ? value.origin : value; }
function documentOrigin(value: CanvasDocumentOrigin | CanvasFrameGeometry): CanvasPoint {
  if (isFrame(value)) return frameDocumentOrigin(value);
  const border = normalizeInsets(value.border); const browserBar = normalizeInsets(value.browserBar); const safeArea = normalizeInsets(value.safeArea);
  return { x: finite(value.x) + border.left + browserBar.left + safeArea.left, y: finite(value.y) + border.top + browserBar.top + safeArea.top };
}
function isFrame(value: CanvasPoint | CanvasFrameGeometry | CanvasDocumentOrigin): value is CanvasFrameGeometry { return 'origin' in value && 'border' in value && 'browserBar' in value && 'safeArea' in value; }
function normalizePoint(value: CanvasPoint | undefined, fallback: CanvasPoint): CanvasPoint { return { x: finite(value?.x, fallback.x), y: finite(value?.y, fallback.y) }; }
function normalizeInsets(value: CanvasInsets | Partial<CanvasInsets> | number | undefined): CanvasInsets { const n = typeof value === 'number' ? value : undefined; return { top: finite(value && typeof value !== 'number' ? value.top : n), right: finite(value && typeof value !== 'number' ? value.right : n), bottom: finite(value && typeof value !== 'number' ? value.bottom : n), left: finite(value && typeof value !== 'number' ? value.left : n) }; }
function finitePositive(value: unknown, fallback: number): number { return typeof value === 'number' && Number.isFinite(value) && value > 0 ? value : fallback; }
function finite(value: unknown, fallback = 0): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
