export interface DesignerViewportPan {
  x: number;
  y: number;
}

export interface DesignerViewport {
  height: number;
  /** The session store materializes pan when a session is loaded. */
  pan?: DesignerViewportPan;
  width: number;
  zoom: number;
}

export interface DesignerGuide {
  axis: 'x' | 'y';
  id: string;
  position: number;
}

export interface DesignerDeviceSession {
  browserBar: { bottom: number; top: number };
  breakpointId: string | null;
  height: number;
  id: string;
  orientation: 'portrait' | 'landscape';
  pixelRatio: number;
  safeArea: { bottom: number; left: number; right: number; top: number };
  width: number;
}

export interface DesignerCanvasSession {
  device: DesignerDeviceSession | null;
  gridSize: number;
  gridVisible: boolean;
  guides: readonly DesignerGuide[];
  minimapVisible: boolean;
  rulersVisible: boolean;
  snapThreshold: number;
  tool: 'hand' | 'select';
}

export interface DesignerEditorSession {
  anchorNodeId: string | null;
  canvas: DesignerCanvasSession;
  documentId: string;
  panelState: Record<string, boolean>;
  primaryNodeId: string | null;
  readonly selectedNodeIds: readonly string[];
  sessionId: string;
  transactionId: string | null;
  viewport: DesignerViewport;
}
