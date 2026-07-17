import { useCallback, useRef, useState } from 'react';

import type { ComponentInsertionTarget } from '../components/componentInsertionTarget';

import type { CanvasDropTarget } from './canvasMovePlanner';
import type { CanvasPoint, CanvasRect, CanvasSize, CanvasViewport } from './coordinateSystem';
import type { PointerTransaction } from './pointerTransaction';
import type { CanvasSelection } from './selectionModel';
import type { SnapGuide } from './snapping';

export type CanvasInteractionMode = 'idle' | 'palette-insert' | 'node-move' | 'resize' | 'marquee' | 'pan' | 'minimap-nav' | 'inline-edit';

export interface CanvasInteractionState {
  additive: boolean;
  mode: Exclude<CanvasInteractionMode, 'idle' | 'palette-insert' | 'minimap-nav' | 'inline-edit'>;
  nodeIds: readonly string[];
  pointerSelection?: 'preserve' | 'toggle-if-click';
  selection?: CanvasRect;
  selectionAtStart: CanvasSelection;
  scroll: CanvasPoint;
  snapEnabled: boolean;
  snapGridSize: number;
  snapThreshold: number;
  stage: HTMLElement;
  stageRect: DOMRect;
  toggleNodeId?: string;
  transaction: PointerTransaction;
  viewport: CanvasViewport;
  viewportSnapshot?: CanvasInteractionViewportSnapshot;
  geometrySnapshot?: CanvasGeometrySnapshot;
}

export interface CanvasInteractionViewportSnapshot {
  pan: CanvasPoint;
  scroll: CanvasPoint;
  stageRect: DOMRect;
  stageSize: CanvasSize;
  zoom: number;
}

export interface CanvasGeometrySnapshot {
  content: CanvasRect;
  parentOrigins: Readonly<Record<string, CanvasPoint>>;
}

export function captureCanvasInteractionViewport(viewport: CanvasViewport, stageRect: DOMRect, stageSize: CanvasSize, scroll: CanvasPoint): CanvasInteractionViewportSnapshot {
  return { pan: { ...viewport.pan }, scroll: { ...scroll }, stageRect, stageSize: { ...stageSize }, zoom: viewport.zoom };
}

export function useCanvasInteractionController() {
  const activeRef = useRef<CanvasInteractionState | null>(null);
  const moveTargetRef = useRef<CanvasDropTarget | null>(null);
  const [mode, setMode] = useState<CanvasInteractionMode>('idle');
  const [previewRects, setPreviewRects] = useState<Record<string, CanvasRect>>({});
  const [guides, setGuides] = useState<readonly SnapGuide[]>([]);
  const [marquee, setMarquee] = useState<CanvasRect | null>(null);
  const [paletteTarget, setPaletteTargetState] = useState<ComponentInsertionTarget | null>(null);
  const [moveTarget, setMoveTargetState] = useState<CanvasDropTarget | null>(null);

  const begin = useCallback((state: CanvasInteractionState) => {
    activeRef.current = state;
    setMode(state.mode);
  }, []);

  const setPaletteTarget = useCallback((target: ComponentInsertionTarget | null) => {
    setPaletteTargetState(target);
    if (target) setMode('palette-insert');
    else if (!activeRef.current) setMode('idle');
  }, []);

  const setMoveTarget = useCallback((target: CanvasDropTarget | null) => {
    moveTargetRef.current = target;
    setMoveTargetState(target);
  }, []);

  const clearPointer = useCallback(() => {
    activeRef.current = null;
    moveTargetRef.current = null;
    setMoveTargetState(null);
    setPreviewRects({});
    setGuides([]);
    setMarquee(null);
    setMode(paletteTarget ? 'palette-insert' : 'idle');
  }, [paletteTarget]);

  const clearAll = useCallback(() => {
    activeRef.current = null;
    moveTargetRef.current = null;
    setMoveTargetState(null);
    setPaletteTargetState(null);
    setPreviewRects({});
    setGuides([]);
    setMarquee(null);
    setMode('idle');
  }, []);

  return {
    activeRef,
    begin,
    clearAll,
    clearPointer,
    guides,
    marquee,
    mode,
    moveTarget,
    moveTargetRef,
    paletteTarget,
    previewRects,
    setGuides,
    setMarquee,
    setMoveTarget,
    setPaletteTarget,
    setPreviewRects
  };
}

export function screenPoint(client: { clientX: number; clientY: number }, rect: Pick<DOMRect, 'left' | 'top'>): CanvasPoint {
  return { x: client.clientX - rect.left, y: client.clientY - rect.top };
}
