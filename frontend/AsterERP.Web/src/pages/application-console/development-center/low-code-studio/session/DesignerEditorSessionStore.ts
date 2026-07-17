import type { DesignerCanvasSession, DesignerDeviceSession, DesignerEditorSession, DesignerViewport, DesignerViewportPan } from '../document/DesignerEditorSession';

export type DesignerSessionSelector<T> = (session: DesignerEditorSession) => T;
export type DesignerSessionListener = (session: DesignerEditorSession) => void;

export interface DesignerViewportPatch {
  height?: number;
  pan?: Partial<DesignerViewportPan>;
  width?: number;
  zoom?: number;
}

export interface DesignerSessionPatch {
  anchorNodeId?: string | null;
  canvas?: Partial<DesignerCanvasSession>;
  panelState?: Record<string, boolean>;
  primaryNodeId?: string | null;
  selectedNodeIds?: readonly string[];
  transactionId?: string | null;
  viewport?: DesignerViewportPatch;
}

export class DesignerEditorSessionStore {
  private current: DesignerEditorSession;
  private readonly listeners = new Set<DesignerSessionListener>();

  public constructor(session: DesignerEditorSession) {
    this.current = freezeSession(normalizeSession(session));
  }

  public getSnapshot(): DesignerEditorSession { return this.current; }

  public select<T>(selector: DesignerSessionSelector<T>): T { return selector(this.current); }

  public subscribe(listener: DesignerSessionListener): () => void {
    this.listeners.add(listener);
    return () => this.listeners.delete(listener);
  }

  public patch(patch: DesignerSessionPatch): DesignerEditorSession {
    const next = normalizeSession({
      anchorNodeId: patch.anchorNodeId === undefined ? this.current.anchorNodeId : patch.anchorNodeId,
      canvas: mergeCanvas(this.current.canvas, patch.canvas),
      documentId: this.current.documentId,
      panelState: patch.panelState === undefined
        ? this.current.panelState
        : { ...this.current.panelState, ...patch.panelState },
      primaryNodeId: patch.primaryNodeId === undefined ? this.current.primaryNodeId : patch.primaryNodeId,
      selectedNodeIds: patch.selectedNodeIds === undefined ? this.current.selectedNodeIds : [...patch.selectedNodeIds],
      sessionId: this.current.sessionId,
      transactionId: patch.transactionId === undefined ? this.current.transactionId : patch.transactionId,
      viewport: mergeViewport(this.current.viewport, patch.viewport)
    });
    if (sameSession(this.current, next)) return this.current;
    this.current = freezeSession(next);
    this.listeners.forEach((listener) => listener(this.current));
    return this.current;
  }

  public replace(session: DesignerEditorSession): DesignerEditorSession {
    const next = freezeSession(normalizeSession(session));
    if (sameSession(this.current, next)) return this.current;
    this.current = next;
    this.listeners.forEach((listener) => listener(this.current));
    return this.current;
  }
}

function mergeViewport(current: DesignerViewport, patch?: DesignerViewportPatch): DesignerViewport {
  if (patch === undefined) return current;
  return {
    height: patch.height === undefined ? current.height : patch.height,
    pan: patch.pan === undefined
      ? current.pan
      : {
          x: patch.pan.x === undefined ? current.pan?.x ?? 0 : patch.pan.x,
          y: patch.pan.y === undefined ? current.pan?.y ?? 0 : patch.pan.y
        },
    width: patch.width === undefined ? current.width : patch.width,
    zoom: patch.zoom === undefined ? current.zoom : patch.zoom
  };
}

function normalizeSession(session: DesignerEditorSession): DesignerEditorSession {
  const selectedNodeIds = [...new Set(session.selectedNodeIds
    .map((id) => id.trim())
    .filter((id) => id.length > 0))];
  const primaryNodeId = selectedNodeIds.includes(session.primaryNodeId ?? '') ? session.primaryNodeId : selectedNodeIds[0] ?? null;
  const anchorNodeId = selectedNodeIds.includes(session.anchorNodeId ?? '') ? session.anchorNodeId : primaryNodeId;
  return {
    anchorNodeId,
    canvas: normalizeCanvas(session.canvas),
    documentId: session.documentId,
    panelState: { ...session.panelState },
    primaryNodeId,
    selectedNodeIds,
    sessionId: session.sessionId,
    transactionId: session.transactionId,
    viewport: normalizeViewport(session.viewport)
  };
}

function mergeCanvas(current: DesignerCanvasSession, patch?: Partial<DesignerCanvasSession>): DesignerCanvasSession {
  if (!patch) return current;
  return normalizeCanvas({ ...current, ...patch, guides: patch.guides === undefined ? current.guides : [...patch.guides] });
}

function normalizeCanvas(canvas: DesignerCanvasSession | undefined): DesignerCanvasSession {
  return {
    device: normalizeDevice(canvas?.device),
    gridSize: normalizeFinite(canvas?.gridSize, 8, 1, 128),
    gridVisible: canvas?.gridVisible !== false,
    guides: (canvas?.guides ?? []).filter((guide) => guide && (guide.axis === 'x' || guide.axis === 'y') && Number.isFinite(guide.position)).map((guide) => ({ axis: guide.axis, id: guide.id.trim(), position: guide.position })).filter((guide) => guide.id.length > 0),
    minimapVisible: canvas?.minimapVisible !== false,
    rulersVisible: canvas?.rulersVisible !== false,
    snapThreshold: normalizeFinite(canvas?.snapThreshold, 6, 0, 64),
    tool: canvas?.tool === 'hand' ? 'hand' : 'select'
  };
}

function normalizeDevice(device: DesignerDeviceSession | null | undefined): DesignerDeviceSession | null {
  if (!device) return null;
  return {
    browserBar: { bottom: normalizeFinite(device.browserBar?.bottom, 0, 0, 400), top: normalizeFinite(device.browserBar?.top, 0, 0, 400) },
    breakpointId: typeof device.breakpointId === 'string' && device.breakpointId.trim() ? device.breakpointId.trim() : null,
    height: normalizeFinite(device.height, 720, 1, 10000),
    id: device.id.trim() || 'custom',
    orientation: device.orientation === 'portrait' ? 'portrait' : 'landscape',
    pixelRatio: normalizeFinite(device.pixelRatio, 1, 0.5, 8),
    safeArea: { bottom: normalizeFinite(device.safeArea?.bottom, 0, 0, 400), left: normalizeFinite(device.safeArea?.left, 0, 0, 400), right: normalizeFinite(device.safeArea?.right, 0, 0, 400), top: normalizeFinite(device.safeArea?.top, 0, 0, 400) },
    width: normalizeFinite(device.width, 1280, 1, 10000)
  };
}

function normalizeViewport(viewport: DesignerViewport): DesignerViewport {
  return {
    height: normalizeFinite(viewport.height, 720, 1),
    pan: {
      x: normalizeFinite(viewport.pan?.x, 0),
      y: normalizeFinite(viewport.pan?.y, 0)
    },
    width: normalizeFinite(viewport.width, 1280, 1),
    zoom: normalizeFinite(viewport.zoom, 1, 0.1, 4)
  };
}

function normalizeFinite(value: number | undefined, fallback: number, minimum?: number, maximum?: number): number {
  const normalized = typeof value === 'number' && Number.isFinite(value) ? value : fallback;
  return Math.min(maximum ?? Number.POSITIVE_INFINITY, Math.max(minimum ?? Number.NEGATIVE_INFINITY, normalized));
}

function sameSession(left: DesignerEditorSession, right: DesignerEditorSession): boolean {
  return JSON.stringify(left) === JSON.stringify(right);
}

function freezeSession(session: DesignerEditorSession): DesignerEditorSession {
  const copy = structuredClone(session);
  Object.freeze(copy.selectedNodeIds);
  Object.freeze(copy.panelState);
  Object.freeze(copy.canvas.guides);
  Object.freeze(copy.canvas);
  if (copy.viewport.pan) Object.freeze(copy.viewport.pan);
  Object.freeze(copy.viewport);
  return Object.freeze(copy);
}
