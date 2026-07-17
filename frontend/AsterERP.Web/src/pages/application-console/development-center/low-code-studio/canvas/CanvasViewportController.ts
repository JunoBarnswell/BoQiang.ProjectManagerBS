import type { DesignerEditorSessionStore } from '../session/DesignerEditorSessionStore';

import {
  clampZoom,
  clientToStageScreen,
  createCanvasFrame,
  createDeviceCanvasFrame,
  documentToScreen as coordinateDocumentToScreen,
  fitSelection,
  fitViewport,
  fitWidthViewport,
  frameDocumentOrigin,
  panBy,
  screenToDocument as coordinateScreenToDocument,
  zoomAtScreenPoint,
  type CanvasPoint,
  type CanvasRect,
  type CanvasFrameGeometry,
  type CanvasSize,
  type CanvasViewport
} from './coordinateSystem';

export type CanvasViewportAction =
  | { anchor?: CanvasPoint; type: 'set-zoom'; zoom: number }
  | { anchor?: CanvasPoint; delta: number; type: 'zoom-by' }
  | { delta: CanvasPoint; type: 'pan-by' }
  | { point: CanvasPoint; type: 'center-on' }
  | { type: 'fit-width' }
  | { type: 'fit-page' }
  | { rect?: CanvasRect; type: 'fit-selection' };

const DEFAULT_STAGE: CanvasSize = { height: 720, width: 1280 };
const DEFAULT_CONTENT: CanvasRect = { height: 720, width: 1280, x: 0, y: 0 };

/**
 * The only writer for Page Studio viewport pan/zoom. Keeping the stage and
 * content bounds here makes toolbar, wheel, keyboard and minimap operations
 * share exactly the same anchoring and clamping rules.
 */
export class CanvasViewportController {
  private content = DEFAULT_CONTENT;
  private selection: CanvasRect | null = null;
  private stage = DEFAULT_STAGE;
  private frame: CanvasFrameGeometry = createCanvasFrame();
  private frameOverride: CanvasFrameGeometry | null = null;
  private interactionDepth = 0;

  public constructor(private readonly sessionStore: DesignerEditorSessionStore) {}

  public configure(input: { content?: CanvasRect; frame?: CanvasFrameGeometry | null; selection?: CanvasRect | null; stage?: CanvasSize }): void {
    if (input.content) this.content = normalizeRect(input.content, DEFAULT_CONTENT);
    if (input.selection !== undefined) this.selection = input.selection ? normalizeRect(input.selection, this.content) : null;
    if (input.stage) this.stage = normalizeSize(input.stage, DEFAULT_STAGE);
    if (input.frame !== undefined) this.frameOverride = input.frame ? createCanvasFrame(input.frame) : null;
    this.frame = this.frameOverride ?? this.resolveSessionFrame();
  }

  public beginInteraction(): void { this.interactionDepth += 1; }

  public endInteraction(): void { this.interactionDepth = Math.max(0, this.interactionDepth - 1); }

  public get isInteractionLocked(): boolean { return this.interactionDepth > 0; }

  public preserveViewportDuring<T>(action: () => T): T {
    const before = this.viewport;
    try { return action(); } finally {
      const after = this.viewport;
      if (after.pan.x !== before.pan.x || after.pan.y !== before.pan.y || after.zoom !== before.zoom) this.commit(before, false);
    }
  }

  public dispatch(action: CanvasViewportAction): CanvasViewport {
    this.refreshFrame();
    if (action.type === 'set-zoom') return this.setZoom(action.zoom, action.anchor);
    if (action.type === 'zoom-by') return this.setZoom(this.viewport.zoom + action.delta, action.anchor);
    if (action.type === 'pan-by') return this.commit(panBy(this.viewport, action.delta));
    if (action.type === 'center-on') return this.centerOn(action.point);
    if (action.type === 'fit-page') return this.commit(fitViewport(this.stage, this.content, 24, this.frame), false);
    if (action.type === 'fit-selection') {
      const rect = action.rect ?? this.selection;
      return rect ? this.commit(fitSelection(this.stage, rect, 32, this.frame), false) : this.viewport;
    }
    return this.commit(fitWidthViewport(this.stage, this.content, 24, this.frame), false);
  }

  public setZoom(zoom: number, anchor?: CanvasPoint): CanvasViewport {
    const frame = this.refreshFrame();
    const point = anchor ?? { x: this.stage.width / 2, y: this.stage.height / 2 };
    return this.commit(zoomAtScreenPoint(this.viewport, clampZoom(zoom), point, frame));
  }

  public zoomBy(delta: number, anchor?: CanvasPoint): CanvasViewport {
    return this.setZoom(this.viewport.zoom + delta, anchor);
  }

  public panBy(delta: CanvasPoint): CanvasViewport {
    return this.commit(panBy(this.viewport, delta));
  }

  public centerOn(point: CanvasPoint): CanvasViewport {
    const frame = this.refreshFrame();
    const viewport = this.viewport;
    const origin = frameDocumentOrigin(frame);
    return this.commit({
      zoom: viewport.zoom,
      pan: {
        x: this.stage.width / 2 - frame.origin.x - (point.x + origin.x) * viewport.zoom,
        y: this.stage.height / 2 - frame.origin.y - (point.y + origin.y) * viewport.zoom
      }
    });
  }

  public fitWidth(): CanvasViewport { return this.commit(fitWidthViewport(this.stage, this.content, 24, this.refreshFrame()), false); }
  public fitPage(): CanvasViewport { return this.dispatch({ type: 'fit-page' }); }
  public fitSelection(rect?: CanvasRect): CanvasViewport { return this.dispatch({ rect, type: 'fit-selection' }); }

  public get coordinateFrame(): CanvasFrameGeometry { return this.refreshFrame(); }

  public screenToDocument(screen: CanvasPoint): CanvasPoint { return coordinateScreenToDocument(screen, this.viewport, this.refreshFrame()); }

  public documentToScreen(document: CanvasPoint): CanvasPoint { return coordinateDocumentToScreen(document, this.viewport, this.refreshFrame()); }

  public clientToDocument(client: CanvasPoint, stageRect: Pick<DOMRect, 'left' | 'top'>, scroll: CanvasPoint = { x: 0, y: 0 }): CanvasPoint {
    return this.screenToDocument(clientToStageScreen(client, stageRect, scroll));
  }

  public get viewport(): CanvasViewport {
    const session = this.sessionStore.getSnapshot();
    return { pan: session.viewport.pan ?? { x: 0, y: 0 }, zoom: session.viewport.zoom };
  }

  private commit(viewport: CanvasViewport, clampPan = true): CanvasViewport {
    const frame = this.refreshFrame();
    const normalized = clampPan ? clampViewport(viewport, this.stage, this.content, 96, frame) : { ...viewport, zoom: clampZoom(viewport.zoom) };
    const current = this.viewport;
    if (current.zoom === normalized.zoom && current.pan.x === normalized.pan.x && current.pan.y === normalized.pan.y) return current;
    this.sessionStore.patch({ viewport: { pan: normalized.pan, zoom: normalized.zoom } });
    return normalized;
  }

  private resolveSessionFrame(): CanvasFrameGeometry {
    const device = this.sessionStore.getSnapshot().canvas.device;
    return createDeviceCanvasFrame(this.stage, device);
  }

  private refreshFrame(): CanvasFrameGeometry {
    this.frame = this.frameOverride ?? this.resolveSessionFrame();
    return this.frame;
  }
}

export function clampViewport(viewport: CanvasViewport, stage: CanvasSize, content: CanvasRect, margin = 96, frame: CanvasPoint | CanvasFrameGeometry = { x: 0, y: 0 }): CanvasViewport {
  const zoom = clampZoom(viewport.zoom);
  const safeStage = normalizeSize(stage, DEFAULT_STAGE);
  const safeContent = normalizeRect(content, DEFAULT_CONTENT);
  const horizontalMargin = Math.min(margin, safeStage.width / 3);
  const verticalMargin = Math.min(margin, safeStage.height / 3);
  const origin = 'origin' in frame ? frame.origin : frame;
  const documentOffset = 'origin' in frame ? frameDocumentOrigin(frame) : { x: 0, y: 0 };
  const minX = horizontalMargin - origin.x - (safeContent.x + documentOffset.x + safeContent.width) * zoom;
  const maxX = safeStage.width - horizontalMargin - origin.x - (safeContent.x + documentOffset.x) * zoom;
  const minY = verticalMargin - origin.y - (safeContent.y + documentOffset.y + safeContent.height) * zoom;
  const maxY = safeStage.height - verticalMargin - origin.y - (safeContent.y + documentOffset.y) * zoom;
  return {
    zoom,
    pan: {
      x: clamp(viewport.pan.x, Math.min(minX, maxX), Math.max(minX, maxX)),
      y: clamp(viewport.pan.y, Math.min(minY, maxY), Math.max(minY, maxY))
    }
  };
}

function normalizeSize(value: CanvasSize, fallback: CanvasSize): CanvasSize {
  return {
    height: finitePositive(value.height, fallback.height),
    width: finitePositive(value.width, fallback.width)
  };
}

function normalizeRect(value: CanvasRect, fallback: CanvasRect): CanvasRect {
  return {
    height: finitePositive(value.height, fallback.height),
    width: finitePositive(value.width, fallback.width),
    x: Number.isFinite(value.x) ? value.x : fallback.x,
    y: Number.isFinite(value.y) ? value.y : fallback.y
  };
}

function finitePositive(value: number, fallback: number): number {
  return Number.isFinite(value) && value > 0 ? value : fallback;
}

function clamp(value: number, minimum: number, maximum: number): number {
  const normalized = Number.isFinite(value) ? value : 0;
  return Math.min(maximum, Math.max(minimum, normalized));
}
