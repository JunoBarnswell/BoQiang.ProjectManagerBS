import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore, type CSSProperties, type KeyboardEvent as ReactKeyboardEvent, type MouseEvent as ReactMouseEvent, type PointerEvent as ReactPointerEvent } from 'react';

import { createRuntimePreviewContext, RuntimeComponentTreePreview } from '../../../../../runtime-kernel/ComponentRuntimeHost';
import type { RuntimeLayoutContext } from '../../../../../runtime-kernel/LayoutResolver';
import { RuntimeKernel, type RuntimeScopeState } from '../../../../../runtime-kernel/RuntimeKernel';
import { projectRuntimeLayout, resolveRuntimeLayoutBox } from '../../../../../runtime-kernel/RuntimeLayoutProjection';
import type { RuntimeContext } from '../../../../../runtime-kernel/RuntimeTypes';
import { resourceReferenceFor } from '../binding/bindingTypes';
import { createConversionPipeline } from '../binding/conversionPipeline';
import { listStableResources } from '../binding/resourceExplorerStore';
import { RESOURCE_DROP_EVENT } from '../binding/resourcePointerDrag';
import { createBatchPatchCommand, createBindValueCommand, createDeleteNodesCommand, createInsertNodesCommand, createMoveNodesCommand, createPatchNodeCommand, createPatchResponsiveOverrideCommand } from '../commands/createDesignerCommands';
import type { DesignerCommandResult } from '../commands/DesignerCommand';
import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import { COMPONENT_POINTER_DRAG_END_EVENT, COMPONENT_POINTER_DRAG_EVENT, COMPONENT_POINTER_DROP_EVENT, createComponentNode, createComponentNodeId, type ComponentPointerDropDetail } from '../components/componentInsertion';
import { resolveComponentInsertionPlacement, resolveComponentInsertionTarget, type ComponentInsertionTarget } from '../components/componentInsertionTarget';
import { ComponentRegistry } from '../components/ComponentRegistry';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';
import type { BindingDocument, DesignerValueType } from '../expression/expressionTypes';
import { createConstraintChange, resolveLayoutMode } from '../layout/layoutOperations';
import type { ResponsiveBreakpoint } from '../responsive/responsiveModel';
import { createOverridePatch, normalizeResponsiveOverrideMap, resolveResponsiveInheritedSections, resolveResponsiveNode } from '../responsive/responsiveModel';
import { DesignerEditorSessionStore } from '../session/DesignerEditorSessionStore';
import { selectNode } from '../session/SelectionModel';
import { createPasteClipboardCommand } from '../shortcuts/clipboardCommands';
import { createDesignerClipboard } from '../shortcuts/designerClipboard';
import type { DesignerClipboardPayload } from '../shortcuts/designerClipboardPayload';
import { nudgeDistance, resolveShortcut, type ShortcutBinding } from '../shortcuts/shortcutModel';

import { CanvasContextMenu, type CanvasContextAction } from './CanvasContextMenu';
import { findCanvasElementAtPoint } from './canvasHitTesting';
import { CanvasMinimap } from './CanvasMinimap';
import { createFlexResizeLayoutPatch, planCanvasMove, resolveCanvasMoveTarget, type CanvasDropTarget, type CanvasMoveGeometry } from './canvasMovePlanner';
import { CanvasRulers } from './CanvasRulers';
import { CanvasViewportController } from './CanvasViewportController';
import { clientToStageScreen, screenToWorld, type CanvasFrameGeometry, type CanvasPoint, type CanvasRect, type CanvasViewport } from './coordinateSystem';
import { DesignerCanvasOverlay } from './DesignerCanvasOverlay';
import { beginPointerTransaction, updatePointerTransaction, type ResizeHandle } from './pointerTransaction';
import { selectByMarquee, type CanvasSelection } from './selectionModel';
import { snapRectWithOptions, type SnapGuide } from './snapping';
import { createCanvasSpatialIndex, type CanvasSpatialIndex } from './spatialIndex';
import { captureCanvasInteractionViewport, useCanvasInteractionController } from './useCanvasInteractionController';

interface DesignerCanvasProps {
  bindingDocument?: BindingDocument | null;
  commandBus: DesignerCommandBus;
  manifests: ComponentRegistry;
  onCommandResult?: (result: DesignerCommandResult) => void;
  onCanvasBlank?: () => void;
  onNodeInserted?: (nodeId: string) => void;
  onNodeSelected?: (nodeId: string) => void;
  previewError?: string | null;
  responsiveBreakpoint?: ResponsiveBreakpoint | null;
  responsiveBreakpoints?: readonly ResponsiveBreakpoint[];
  shortcutBindings?: readonly ShortcutBinding[];
  previewKernel?: RuntimeKernel | null;
  previewLayoutContext?: RuntimeLayoutContext;
  previewState?: RuntimeScopeState;
  canvasText?: { previewInvalid: string; unknownComponent: string; previewPreparing: string };
  sessionStore: DesignerEditorSessionStore;
  studioText?: (key: string) => string;
  viewportController?: CanvasViewportController;
}

interface CanvasGeometry extends CanvasMoveGeometry {
  nodes: Record<string, DesignerDocumentNode>;
  rects: Record<string, CanvasRect>;
  parentOrigins: Record<string, CanvasPoint>;
  layoutModes: Record<string, ReturnType<typeof resolveLayoutMode>>;
  spatialIndex: CanvasSpatialIndex;
}

const DEFAULT_STAGE_SIZE = { width: 1280, height: 720 };

function sameRuntimeRects(left: Readonly<Record<string, CanvasRect>>, right: Readonly<Record<string, CanvasRect>>): boolean {
  const leftKeys = Object.keys(left);
  const rightKeys = Object.keys(right);
  if (leftKeys.length !== rightKeys.length) return false;
  return leftKeys.every((key) => {
    const a = left[key]; const b = right[key];
    return Boolean(a && b && Math.abs(a.x - b.x) < 0.5 && Math.abs(a.y - b.y) < 0.5 && Math.abs(a.width - b.width) < 0.5 && Math.abs(a.height - b.height) < 0.5);
  });
}

export function DesignerCanvas({ bindingDocument, canvasText = { previewInvalid: 'Preview unavailable', unknownComponent: 'Unknown component manifest', previewPreparing: 'Preparing latest runtime preview' }, commandBus, manifests, onCanvasBlank, onCommandResult, onNodeInserted, onNodeSelected, previewError = null, previewKernel = null, previewLayoutContext = {}, previewState = { scopes: {}, variables: {} }, responsiveBreakpoint = null, responsiveBreakpoints = [], shortcutBindings, sessionStore, studioText = defaultStudioText, viewportController }: DesignerCanvasProps) {
  const documentSubscribe = useCallback((listener: () => void) => commandBus.subscribe(listener), [commandBus]);
  const documentSnapshot = useCallback(() => commandBus.document, [commandBus]);
  const sessionSubscribe = useCallback((listener: () => void) => sessionStore.subscribe(() => listener()), [sessionStore]);
  const sessionSnapshot = useCallback(() => sessionStore.getSnapshot(), [sessionStore]);
  const document = useSyncExternalStore(documentSubscribe, documentSnapshot, documentSnapshot);
  const session = useSyncExternalStore(sessionSubscribe, sessionSnapshot, sessionSnapshot);
  const canvasViewport = useMemo(() => viewportController ?? new CanvasViewportController(sessionStore), [sessionStore, viewportController]);
  const stageRef = useRef<HTMLElement | null>(null);
  const clipboardRef = useRef<DesignerClipboardPayload | null>(null);
  const { activeRef: interactionRef, begin: beginInteraction, clearAll: clearAllInteractions, clearPointer: clearPointerInteraction, guides, marquee, mode: interactionMode, moveTarget, moveTargetRef, paletteTarget, previewRects, setGuides, setMarquee, setMoveTarget, setPaletteTarget, setPreviewRects } = useCanvasInteractionController();
  const [spacePressed, setSpacePressed] = useState(false);
  const [activeNodeId, setActiveNodeId] = useState<string | null>(null);
  const [collapsedNodeIds, setCollapsedNodeIds] = useState<ReadonlySet<string>>(new Set());
  const [inlineEditingNodeId, setInlineEditingNodeId] = useState<string | null>(null);
  const [contextMenu, setContextMenu] = useState<{ nodeId: string; x: number; y: number } | null>(null);
  const [stageBounds, setStageBounds] = useState({ height: 0, width: 0 });
  const initialFitRef = useRef(false);
  const previousToolRef = useRef(session.canvas.tool);
  const nodeRefs = useRef(new Map<string, HTMLDivElement>());
  const runtimePreviewRef = useRef<HTMLDivElement | null>(null);
  const [runtimeRects, setRuntimeRects] = useState<Readonly<Record<string, CanvasRect>>>({});

  useEffect(() => {
    const stage = stageRef.current;
    if (!stage) return undefined;
    const updateBounds = () => {
      const rect = stage.getBoundingClientRect();
      if (rect.width > 0 && rect.height > 0) setStageBounds({ width: rect.width, height: rect.height });
    };
    updateBounds();
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', updateBounds);
      return () => window.removeEventListener('resize', updateBounds);
    }
    const observer = new ResizeObserver(updateBounds);
    observer.observe(stage);
    return () => observer.disconnect();
  }, []);

  const rootId = document.pages.map((page) => page.rootElementId).find((id) => Boolean(document.elements[id]));
  const elementAtCanvasPoint = useCallback((clientX: number, clientY: number, excludedNodeIds?: ReadonlySet<string>): Element | null => {
    const stage = stageRef.current;
    if (!stage) return null;
    const hit = findCanvasElementAtPoint(stage, clientX, clientY, { excludedNodeIds });
    const semanticTarget = hit?.closest('[data-node-id],[data-canvas-artboard="true"]');
    if (semanticTarget) return hit;
    const rootElement = rootId ? nodeRefs.current.get(rootId) : undefined;
    if (rootElement && containsClientPoint(rootElement, clientX, clientY)) return rootElement;
    return hit;
  }, [rootId]);
  const resolvePointerInsertionTarget = useCallback((detail: ComponentPointerDropDetail): ComponentInsertionTarget | null => {
    const manifest = manifests.get(detail.type);
    if (!manifest) return null;
    const target = elementAtCanvasPoint(detail.clientX, detail.clientY);
    const targetNodeId = target?.closest<HTMLElement>('[data-node-id]')?.dataset.nodeId;
    const targetNode = targetNodeId ? document.elements[targetNodeId] : undefined;
    const targetElement = targetNodeId ? nodeRefs.current.get(targetNodeId) : undefined;
    const placement = targetNode && targetElement ? resolveComponentInsertionPlacement({ clientX: detail.clientX, clientY: detail.clientY, parentLayout: targetNode.parentId ? document.elements[targetNode.parentId]?.layout : undefined, rect: targetElement.getBoundingClientRect(), targetAcceptsChildren: manifests.get(targetNode.type)?.capability.acceptsChildren === true }) : 'inside';
    return resolveComponentInsertionTarget({ document, manifests, component: manifest, dropTargetNodeId: targetNodeId, selectedNodeId: session.primaryNodeId, placement });
  }, [document, elementAtCanvasPoint, manifests, session.primaryNodeId]);
  const handleComponentPointerDrag = useCallback((event: Event) => {
    const detail = (event as CustomEvent<ComponentPointerDropDetail>).detail;
    if (!detail?.type) return;
    setPaletteTarget(resolvePointerInsertionTarget(detail));
  }, [resolvePointerInsertionTarget, setPaletteTarget]);
  const handleComponentPointerDragEnd = useCallback(() => setPaletteTarget(null), [setPaletteTarget]);
  const handleComponentPointerDrop = useCallback((event: Event) => {
    const detail = (event as CustomEvent<ComponentPointerDropDetail>).detail;
    const type = detail?.type;
    if (!type || !rootId) return;
    if (!elementAtCanvasPoint(detail.clientX, detail.clientY)) return;
    const manifest = manifests.get(type);
    if (!manifest) return;
    const targetResult = resolvePointerInsertionTarget(detail);
    if (!targetResult) return;
    const parent = document.elements[targetResult.parentId];
    const parentMode = parent ? resolveLayoutMode(parent.layout) : 'free';
    const parentElement = nodeRefs.current.get(targetResult.parentId);
    const parentRect = parentElement?.getBoundingClientRect();
    const position = parentRect && parentMode !== 'flex' && parentMode !== 'grid'
      ? { x: (detail.clientX - parentRect.left) / session.viewport.zoom, y: (detail.clientY - parentRect.top) / session.viewport.zoom }
      : undefined;
    const node = createComponentNode(manifest, targetResult.parentId, createComponentNodeId(type, new Set(Object.keys(document.elements))), position);
    const result = commandBus.execute(createInsertNodesCommand([node], targetResult.index));
    onCommandResult?.(result);
    if (result.changed) onNodeInserted?.(node.id);
    setPaletteTarget(null);
  }, [commandBus, document, elementAtCanvasPoint, manifests, onCommandResult, onNodeInserted, resolvePointerInsertionTarget, rootId, session.viewport.zoom, setPaletteTarget]);

  useEffect(() => {
    window.addEventListener(COMPONENT_POINTER_DRAG_EVENT, handleComponentPointerDrag);
    window.addEventListener(COMPONENT_POINTER_DROP_EVENT, handleComponentPointerDrop);
    window.addEventListener(COMPONENT_POINTER_DRAG_END_EVENT, handleComponentPointerDragEnd);
    return () => { window.removeEventListener(COMPONENT_POINTER_DRAG_EVENT, handleComponentPointerDrag); window.removeEventListener(COMPONENT_POINTER_DROP_EVENT, handleComponentPointerDrop); window.removeEventListener(COMPONENT_POINTER_DRAG_END_EVENT, handleComponentPointerDragEnd); };
  }, [handleComponentPointerDrag, handleComponentPointerDragEnd, handleComponentPointerDrop]);
  const zoom = session.viewport.zoom;
  const pan: CanvasPoint = session.viewport.pan ?? { x: 0, y: 0 };
  const viewport: CanvasViewport = { zoom, pan };
  const stageWidth = session.canvas.device?.width ?? Math.max(DEFAULT_STAGE_SIZE.width, session.viewport.width);
  const stageHeight = session.canvas.device?.height ?? Math.max(DEFAULT_STAGE_SIZE.height, session.viewport.height);
  const stageSize = useMemo(() => ({ x: 0, y: 0, width: stageWidth, height: stageHeight }), [stageHeight, stageWidth]);
  const runtimeLayouts = useMemo(() => {
    if (!previewKernel) return undefined;
    return Object.fromEntries(Object.keys(document.elements).flatMap((id) => {
      const snapshot = previewKernel.snapshot(id, previewState, previewLayoutContext);
      return snapshot ? [[id, { layout: snapshot.layout }]] : [];
    }));
  }, [document.elements, previewKernel, previewLayoutContext, previewState]);
  const geometry = useMemo(() => buildGeometry(document, rootId, stageSize, responsiveBreakpoint, responsiveBreakpoints, runtimeLayouts), [document, responsiveBreakpoint, responsiveBreakpoints, rootId, runtimeLayouts, stageSize]);
  const runtimePreview = useMemo(() => previewKernel ? createRuntimePreviewContext(previewKernel.artifact, previewLayoutContext, previewState) : null, [previewKernel, previewLayoutContext, previewState]);
  const rootRect = runtimeRects[rootId ?? ''] ?? geometry.rects[rootId ?? ''] ?? stageSize;
  const selectionBounds = useMemo(() => mergeRects(session.selectedNodeIds.map((id) => runtimeRects[id] ?? geometry.rects[id]).filter((rect): rect is CanvasRect => Boolean(rect))), [geometry.rects, runtimeRects, session.selectedNodeIds]);
  const visibleNodeIds = getVisibleNodeIds(document, rootId, collapsedNodeIds).filter((id) => id !== rootId);
  const rovingNodeId = activeNodeId && visibleNodeIds.includes(activeNodeId) ? activeNodeId : session.primaryNodeId && visibleNodeIds.includes(session.primaryNodeId) ? session.primaryNodeId : visibleNodeIds[0] ?? null;

  useEffect(() => {
    const preview = runtimePreviewRef.current;
    if (!preview || !runtimePreview) {
      setRuntimeRects({});
      return undefined;
    }
    let frame = 0;
    const measure = () => {
      frame = 0;
      const origin = preview.getBoundingClientRect();
      if (origin.width < 1 || origin.height < 1) return;
      const scale = Math.max(zoom, 0.01);
      const next: Record<string, CanvasRect> = {};
      preview.querySelectorAll<HTMLElement>('[data-runtime-element-id]').forEach((element) => {
        const id = element.dataset.runtimeElementId;
        const rect = element.getBoundingClientRect();
        if (!id || rect.width < 1 || rect.height < 1) return;
        next[id] = { height: rect.height / scale, width: rect.width / scale, x: (rect.left - origin.left) / scale, y: (rect.top - origin.top) / scale };
      });
      setRuntimeRects((current) => sameRuntimeRects(current, next) ? current : next);
    };
    const schedule = () => { if (!frame) frame = requestAnimationFrame(measure); };
    schedule();
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(schedule);
    observer?.observe(preview);
    preview.querySelectorAll<HTMLElement>('[data-runtime-element-id]').forEach((element) => observer?.observe(element));
    const mutations = new MutationObserver(schedule);
    mutations.observe(preview, { attributes: true, childList: true, subtree: true });
    return () => { if (frame) cancelAnimationFrame(frame); observer?.disconnect(); mutations.disconnect(); };
  }, [document.revision, previewKernel, responsiveBreakpoint?.id, runtimePreview, zoom]);

  useEffect(() => {
    canvasViewport.configure({ content: rootRect, selection: selectionBounds, stage: stageBounds });
  }, [canvasViewport, rootRect, selectionBounds, stageBounds]);

  useEffect(() => {
    if (initialFitRef.current || stageBounds.width < 1 || stageBounds.height < 1) return;
    initialFitRef.current = true;
    canvasViewport.fitWidth();
  }, [canvasViewport, stageBounds.height, stageBounds.width]);

  const patchSelection = useCallback((selection: CanvasSelection) => {
    sessionStore.patch({ anchorNodeId: selection.anchorNodeId, primaryNodeId: selection.primaryNodeId, selectedNodeIds: selection.selectedNodeIds });
  }, [sessionStore]);

  const select = useCallback((nodeId: string, additive: boolean) => {
    const targetNodeId = resolvePenetrationTarget(nodeId, session.primaryNodeId, document.elements, false);
    if (isNodeLocked(document.elements[targetNodeId])) return;
    const next = selectNode({
      anchorNodeId: session.anchorNodeId,
      primaryNodeId: session.primaryNodeId,
      selectedNodeIds: [...session.selectedNodeIds]
    }, targetNodeId, additive);
    sessionStore.patch({ ...next, transactionId: null });
  }, [document.elements, session.anchorNodeId, session.primaryNodeId, session.selectedNodeIds, sessionStore]);

  const selectThrough = useCallback((nodeId: string, additive: boolean) => {
    const targetNodeId = resolvePenetrationTarget(nodeId, session.primaryNodeId, document.elements, true);
    if (isNodeLocked(document.elements[targetNodeId])) return;
    const next = selectNode({ anchorNodeId: session.anchorNodeId, primaryNodeId: session.primaryNodeId, selectedNodeIds: [...session.selectedNodeIds] }, targetNodeId, additive);
    sessionStore.patch({ ...next, transactionId: null });
  }, [document.elements, session.anchorNodeId, session.primaryNodeId, session.selectedNodeIds, sessionStore]);

  const focusNode = useCallback((nodeId: string) => {
    setActiveNodeId(nodeId);
    nodeRefs.current.get(nodeId)?.focus();
  }, []);

  const closeContextMenu = useCallback(() => {
    const nodeId = contextMenu?.nodeId;
    setContextMenu(null);
    if (nodeId) focusNode(nodeId);
  }, [contextMenu, focusNode]);

  const executeLayoutPatches = useCallback((patches: Record<string, Partial<DesignerDocumentNode>>) => {
    if (!responsiveBreakpoint) return commandBus.execute(createBatchPatchCommand(patches));
    const commands = Object.entries(patches).map(([nodeId, patch]) => createPatchResponsiveOverrideCommand(nodeId, responsiveBreakpoint.id, patch));
    return commandBus.executeTransaction(commands);
  }, [commandBus, responsiveBreakpoint]);

  const handleTreeKeyDown = useCallback((event: ReactKeyboardEvent<HTMLElement>) => {
    const target = event.target instanceof HTMLElement ? event.target : null;
    const nodeElement = target?.closest<HTMLElement>('[data-node-id]');
    if (!nodeElement || target?.closest('button,input,textarea,select,[contenteditable="true"]')) return;
    const currentId = nodeElement.dataset.nodeId;
    if (!currentId) return;
    const currentNode = document.elements[currentId];
    if (!currentNode) return;
    const currentIndex = visibleNodeIds.indexOf(currentId);
    if (currentIndex < 0) return;
    const children = currentNode.children.filter((childId) => Boolean(document.elements[childId]));
    const expanded = children.length > 0 && !collapsedNodeIds.has(currentId);
    let nextId: string | undefined;
    if (event.key === 'ArrowDown') nextId = visibleNodeIds[currentIndex + 1];
    if (event.key === 'ArrowUp') nextId = visibleNodeIds[currentIndex - 1];
    if (event.key === 'Home') nextId = visibleNodeIds[0];
    if (event.key === 'End') nextId = visibleNodeIds[visibleNodeIds.length - 1];
    if (event.key === 'ArrowRight') {
      if (children.length > 0 && !expanded) {
        setCollapsedNodeIds((current) => { const next = new Set(current); next.delete(currentId); return next; });
        nextId = currentId;
      } else if (children.length > 0) nextId = children[0];
    }
    if (event.key === 'ArrowLeft') {
      if (children.length > 0 && expanded) {
        setCollapsedNodeIds((current) => new Set(current).add(currentId));
        nextId = currentId;
      } else if (currentNode.parentId && document.elements[currentNode.parentId]) nextId = currentNode.parentId;
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      select(currentId, false);
      focusNode(currentId);
      return;
    }
    if (!nextId) return;
    event.preventDefault();
    if (nextId !== currentId) select(nextId, false);
    focusNode(nextId);
  }, [collapsedNodeIds, document.elements, focusNode, select, visibleNodeIds]);

  const finishInteraction = useCallback((event: PointerEvent, cancelled = false) => {
    const state = interactionRef.current;
    if (!state || state.transaction.pointerId !== event.pointerId) return;
    if (cancelled) {
      patchSelection(state.selectionAtStart);
      clearPointerInteraction();
      canvasViewport.endInteraction();
      sessionStore.patch({ transactionId: null });
      return;
    }
    const coordinates = toPointerCoordinates(event, state.stageRect, state.viewport, state.scroll);
    const point = state.transaction.kind === 'pan' ? coordinates.stageScreenPoint : coordinates.worldPoint;
    const update = updatePointerTransaction(state.transaction, point, { minWidth: 1, minHeight: 1 });
    const dragThreshold = 4 / Math.max(state.viewport.zoom, 0.01);
    if (update && state.transaction.kind === 'move' && state.pointerSelection === 'toggle-if-click' && !hasPointerMovement(update.rects, state.transaction.snapshots, dragThreshold)) {
      select(state.toggleNodeId ?? state.nodeIds[0], true);
    } else if (update?.selection) {
      patchSelection(selectByMarquee(Object.values(geometry.rects).filter((rect) => rect.id !== rootId && !isNodeLocked(rect.id ? document.elements[rect.id] : undefined)), update.selection, state.additive, state.selectionAtStart));
    } else if (update && state.transaction.kind === 'move') {
      const nextRects = state.transaction.kind === 'move' ? snapMovingRects(update.rects, geometry, state.nodeIds, { enabled: state.snapEnabled && !event.altKey, gridSize: state.snapGridSize, threshold: state.snapThreshold }) : update.rects;
      const target = moveTargetRef.current ?? (rootId ? resolveCanvasMoveTarget({
        clientX: event.clientX,
        clientY: event.clientY,
        document,
        hitElement: elementAtCanvasPoint(event.clientX, event.clientY, collectMoveSubtreeIds(document.elements, state.nodeIds)),
        manifests,
        movingNodeIds: state.nodeIds,
        nodeElements: nodeRefs.current,
        rootElement: rootId ? nodeRefs.current.get(rootId) : null,
        rootId
      }) : null);
      if (hasPointerMovement(nextRects, state.transaction.snapshots, dragThreshold)) {
        if (target) {
          const planned = planCanvasMove({ breakpointId: responsiveBreakpoint?.id, document, geometry: geometry as CanvasMoveGeometry, nodeIds: state.nodeIds, rects: nextRects, target });
          if (planned.ok && planned.changed) {
            const result = canvasViewport.preserveViewportDuring(() => commandBus.execute(createMoveNodesCommand(planned.plan)));
            onCommandResult?.(result);
          } else if (!planned.ok) {
            onCommandResult?.({ changed: false, diagnostics: planned.diagnostics, document });
          }
        } else {
          onCommandResult?.({ changed: false, diagnostics: [studioText('invalidMoveTarget')], document });
        }
      }
    } else if (update && state.transaction.kind === 'resize') {
      const patches = createGeometryPatches(update.rects, geometry.nodes, state.transaction.kind, state.transaction.snapshots, geometry.parentOrigins, geometry.layoutModes, geometry.rects);
      const commandPatches = responsiveBreakpoint
        ? toResponsiveGeometryPatches(patches, state.transaction.kind, document.elements, geometry.nodes, responsiveBreakpoint, responsiveBreakpoints)
        : patches;
      if (Object.keys(commandPatches).length > 0) onCommandResult?.(executeLayoutPatches(commandPatches));
    }
    clearPointerInteraction();
    canvasViewport.endInteraction();
    sessionStore.patch({ transactionId: null });
  }, [canvasViewport, clearPointerInteraction, commandBus, document, elementAtCanvasPoint, executeLayoutPatches, geometry, interactionRef, manifests, moveTargetRef, onCommandResult, patchSelection, responsiveBreakpoint, responsiveBreakpoints, rootId, select, sessionStore, studioText]);

  useEffect(() => {
    const move = (event: PointerEvent) => {
      const state = interactionRef.current;
      if (!state || state.transaction.pointerId !== event.pointerId) return;
      const coordinates = toPointerCoordinates(event, state.stageRect, state.viewport, state.scroll);
      const point = state.transaction.kind === 'pan' ? coordinates.stageScreenPoint : coordinates.worldPoint;
      const update = updatePointerTransaction(state.transaction, point, { minWidth: 1, minHeight: 1 });
      if (state.transaction.kind === 'pan') {
        canvasViewport.panBy(update.delta);
        state.transaction = beginPointerTransaction('pan', state.transaction.pointerId, point, []);
        return;
      }
      if (update.selection) {
        setMarquee(update.selection);
        return;
      }
      const snappingEnabled = state.snapEnabled && !event.altKey;
      const dragThreshold = 4 / Math.max(state.viewport.zoom, 0.01);
      const passedDragThreshold = state.transaction.kind !== 'move' || hasPointerMovement(update.rects, state.transaction.snapshots, dragThreshold);
      const nextRects = state.transaction.kind === 'move' && passedDragThreshold ? snapMovingRects(update.rects, geometry, state.nodeIds, { enabled: snappingEnabled, gridSize: state.snapGridSize, threshold: state.snapThreshold }) : update.rects;
      setPreviewRects(Object.fromEntries(nextRects.filter((rect) => rect.id).map((rect) => [rect.id, rect])));
      if (state.transaction.kind === 'move' && passedDragThreshold) {
        setGuides(snapMovingGuides(update.rects, geometry, state.nodeIds, { enabled: snappingEnabled, gridSize: state.snapGridSize, threshold: state.snapThreshold }));
        const excludedNodeIds = collectMoveSubtreeIds(document.elements, state.nodeIds);
        const hitElement = elementAtCanvasPoint(event.clientX, event.clientY, excludedNodeIds);
        setMoveTarget(rootId ? resolveCanvasMoveTarget({ clientX: event.clientX, clientY: event.clientY, document, hitElement, manifests, movingNodeIds: state.nodeIds, nodeElements: nodeRefs.current, rootElement: nodeRefs.current.get(rootId), rootId }) : null);
      } else if (state.transaction.kind === 'move') {
        setGuides([]);
        setMoveTarget(null);
      }
    };
    const up = (event: PointerEvent) => finishInteraction(event);
    const cancel = (event: PointerEvent) => finishInteraction(event, true);
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    window.addEventListener('pointercancel', cancel);
    window.addEventListener('lostpointercapture', cancel);
    return () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', up);
      window.removeEventListener('pointercancel', cancel);
      window.removeEventListener('lostpointercapture', cancel);
    };
  }, [canvasViewport, document, elementAtCanvasPoint, finishInteraction, geometry, interactionRef, manifests, rootId, setGuides, setMarquee, setMoveTarget, setPreviewRects]);

  useEffect(() => {
    if (previousToolRef.current === session.canvas.tool) return;
    previousToolRef.current = session.canvas.tool;
    const active = interactionRef.current;
    if (!active) return;
    patchSelection(active.selectionAtStart);
    clearAllInteractions();
    canvasViewport.endInteraction();
    sessionStore.patch({ transactionId: null });
  }, [canvasViewport, clearAllInteractions, interactionRef, patchSelection, session.canvas.tool, sessionStore]);

  useEffect(() => {
    const down = (event: KeyboardEvent) => {
      if (event.defaultPrevented) return;
      if (event.code === 'Space' && !(event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement)) setSpacePressed(true);
      if (event.target instanceof HTMLElement && event.target.closest('input,textarea,select,[contenteditable="true"]')) return;
      if (event.key.toLowerCase() === 'h') {
        event.preventDefault();
        const active = interactionRef.current;
        if (active) patchSelection(active.selectionAtStart);
        clearAllInteractions();
        if (active) canvasViewport.endInteraction();
        sessionStore.patch({ canvas: { tool: session.canvas.tool === 'hand' ? 'select' : 'hand' } });
        return;
      }
      const action = resolveShortcut(event, shortcutBindings);
      if (!action) return;
      const ids = [...session.selectedNodeIds].filter((id) => id !== rootId);
      if (action === 'copy') { if (ids.length > 0) clipboardRef.current = createDesignerClipboard(document, ids); event.preventDefault(); return; }
      if (action === 'paste' || action === 'duplicate') {
        if (action === 'duplicate' && !clipboardRef.current?.trees.length && ids.length > 0) clipboardRef.current = createDesignerClipboard(document, ids);
        if (!clipboardRef.current?.trees.length) return;
        const parentId = document.elements[session.primaryNodeId ?? '']?.parentId ?? rootId;
        if (!parentId) return;
        const result = commandBus.execute(createPasteClipboardCommand(clipboardRef.current, parentId, (sourceId, occupied) => { let index = 1; let id = `${sourceId}-copy-${index}`; while (occupied.has(id)) id = `${sourceId}-copy-${++index}`; return id; }));
        onCommandResult?.(result); event.preventDefault(); return;
      }
      if (action === 'delete') {
        if (ids.length === 0) return;
        event.preventDefault(); onCommandResult?.(commandBus.execute(createDeleteNodesCommand(ids)));
        sessionStore.patch({ anchorNodeId: null, primaryNodeId: null, selectedNodeIds: [], transactionId: null }); return;
      }
      if (action === 'select-all') { event.preventDefault(); patchSelection({ selectedNodeIds: Object.keys(document.elements).filter((id) => id !== rootId && !isNodeLocked(document.elements[id])), primaryNodeId: null, anchorNodeId: null }); return; }
      if (action === 'escape') {
        if (interactionRef.current) {
          event.preventDefault();
          patchSelection(interactionRef.current.selectionAtStart);
          clearAllInteractions();
          canvasViewport.endInteraction();
          sessionStore.patch({ transactionId: null });
          return;
        }
        if (contextMenu) { event.preventDefault(); closeContextMenu(); return; }
        if (inlineEditingNodeId) { event.preventDefault(); setInlineEditingNodeId(null); return; }
        if (session.selectedNodeIds.length > 0) { event.preventDefault(); sessionStore.patch({ anchorNodeId: null, primaryNodeId: null, selectedNodeIds: [], transactionId: null }); onCanvasBlank?.(); }
        return;
      }
      if (action === 'undo' || action === 'redo') { event.preventDefault(); const result = action === 'undo' ? commandBus.undo() : commandBus.redo(); if (result) onCommandResult?.(result); return; }
      if (action === 'zoom-in' || action === 'zoom-out') { event.preventDefault(); canvasViewport.zoomBy(action === 'zoom-in' ? 0.1 : -0.1); return; }
      if (action === 'fit-page') { event.preventDefault(); canvasViewport.fitPage(); return; }
      if (action === 'fit-selection') {
        const selectedRects = ids.map((id) => geometry.rects[id]).filter((rect): rect is CanvasRect => Boolean(rect));
        if (selectedRects.length === 0) return;
        event.preventDefault();
        const selection = selectedRects.reduce((bounds, rect) => {
          const right = Math.max(bounds.x + bounds.width, rect.x + rect.width);
          const bottom = Math.max(bounds.y + bounds.height, rect.y + rect.height);
          const x = Math.min(bounds.x, rect.x);
          const y = Math.min(bounds.y, rect.y);
          return { x, y, width: right - x, height: bottom - y };
        }, selectedRects[0]);
        canvasViewport.fitSelection(selection);
        return;
      }
      if (action.startsWith('nudge-') && ids.length > 0) {
        const distance = nudgeDistance(event); const dx = action === 'nudge-left' ? -distance : action === 'nudge-right' ? distance : 0; const dy = action === 'nudge-up' ? -distance : action === 'nudge-down' ? distance : 0;
        const patches = Object.fromEntries(ids
          .filter((id) => geometry.layoutModes[document.elements[id]?.parentId ?? ''] !== 'flex' && geometry.layoutModes[document.elements[id]?.parentId ?? ''] !== 'grid')
          .map((id) => {
            const node = geometry.nodes[id] ?? document.elements[id];
            const rect = geometry.rects[id];
            const parentOrigin = geometry.parentOrigins[id] ?? { x: 0, y: 0 };
            const local = { x: (rect?.x ?? numberValue(node.layout.x) + parentOrigin.x) - parentOrigin.x + dx, y: (rect?.y ?? numberValue(node.layout.y) + parentOrigin.y) - parentOrigin.y + dy, width: rect?.width ?? numberValue(node.layout.width, 1), height: rect?.height ?? numberValue(node.layout.height, 1) };
            const parentId = node.parentId ?? '';
            const parentMode = geometry.layoutModes[parentId];
            const constraints = parentMode === 'constraints' && geometry.rects[parentId] ? updateConstraintAnchors(node, geometry.rects[parentId], local) : undefined;
            return [id, { layout: { ...node.layout, ...(constraints ? { constraints } : { position: 'absolute', x: local.x, y: local.y }) } }];
          }));
        if (Object.keys(patches).length === 0) return;
        const commandPatches = responsiveBreakpoint
          ? toResponsiveGeometryPatches(patches, 'move', document.elements, geometry.nodes, responsiveBreakpoint, responsiveBreakpoints)
          : patches;
        event.preventDefault(); onCommandResult?.(executeLayoutPatches(commandPatches)); return;
      }
    };
    const up = (event: KeyboardEvent) => { if (event.code === 'Space') setSpacePressed(false); };
    window.addEventListener('keydown', down);
    window.addEventListener('keyup', up);
    return () => { window.removeEventListener('keydown', down); window.removeEventListener('keyup', up); };
  }, [canvasViewport, clearAllInteractions, closeContextMenu, commandBus, contextMenu, document, executeLayoutPatches, geometry, inlineEditingNodeId, interactionRef, onCanvasBlank, onCommandResult, patchSelection, responsiveBreakpoint, responsiveBreakpoints, rootId, session, sessionStore, shortcutBindings]);

  if (!rootId) return <div className="grid h-full place-items-center text-sm text-[color:var(--app-muted)]">{studioText('canvasRootMissing')}</div>;

  const activeDropTarget = moveTarget ?? paletteTarget;
  const dropOperation = moveTarget ? 'move' : 'insert';
  const handleContextAction = (action: CanvasContextAction) => {
    const current = contextMenu;
    closeContextMenu();
    if (!current) return;
    const node = document.elements[current.nodeId];
    if (!node) return;
    if (action === 'copy') {
      clipboardRef.current = createDesignerClipboard(document, [node.id]);
      return;
    }
    if (action === 'delete') {
      onCommandResult?.(commandBus.execute(createDeleteNodesCommand([node.id])));
      sessionStore.patch({ anchorNodeId: null, primaryNodeId: null, selectedNodeIds: [], transactionId: null });
      return;
    }
    if (action === 'toggle-lock') {
      onCommandResult?.(commandBus.execute(createPatchNodeCommand(node.id, { locked: !isNodeLocked(node) })));
      return;
    }
    if (action === 'bring-forward' || action === 'bring-to-front' || action === 'send-backward' || action === 'send-to-back') {
      const parent = node.parentId ? document.elements[node.parentId] : undefined;
      if (!parent) return;
      const index = parent.children.indexOf(node.id);
      if (index < 0) return;
      const remainingLength = Math.max(0, parent.children.length - 1);
      const targetIndex = action === 'bring-forward' ? Math.min(remainingLength, index + 1)
        : action === 'bring-to-front' ? remainingLength
          : action === 'send-backward' ? Math.max(0, index - 1) : 0;
      const result = commandBus.execute(createMoveNodesCommand({ insertionIndex: targetIndex, nodeIds: [node.id], parentId: parent.id }));
      if (!result.changed && result.diagnostics.length > 0) onCommandResult?.(result);
      return;
    }
    const parentId = action === 'paste' && manifests.get(node.type)?.capability.acceptsChildren ? node.id : node.parentId;
    if (!parentId) return;
    const clipboard = action === 'paste' ? clipboardRef.current : createDesignerClipboard(document, [node.id]);
    if (!clipboard) return;
    const result = commandBus.execute(createPasteClipboardCommand(clipboard, parentId, (sourceId, occupied) => {
      let index = 1;
      let id = `${sourceId}-copy-${index}`;
      while (occupied.has(id)) id = `${sourceId}-copy-${++index}`;
      return id;
    }));
    onCommandResult?.(result);
  };
  return (
    <section
      ref={stageRef}
      aria-label={studioText('canvas')}
      className={`page-studio__canvas ${interactionMode !== 'idle' ? 'is-interacting' : ''} ${session.canvas.tool === 'hand' || spacePressed ? 'cursor-grab active:cursor-grabbing' : ''}`}
      data-canvas-coordinate-system="screen-world-document-local"
      data-canvas-interaction-mode={interactionMode}
      data-device-id={session.canvas.device?.id}
      data-device-pixel-ratio={session.canvas.device?.pixelRatio}
      data-device-preview={session.canvas.device ? 'true' : undefined}
      onFocusCapture={(event) => {
        const nodeElement = (event.target as HTMLElement).closest<HTMLElement>('[data-node-id]');
        if (nodeElement?.dataset.nodeId) setActiveNodeId(nodeElement.dataset.nodeId);
      }}
      onKeyDown={handleTreeKeyDown}
      onPointerDown={(event) => beginStageInteraction(event, stageRef.current, rootId)}
      onContextMenu={(event) => { event.preventDefault(); closeContextMenu(); }}
      role="tree"
      aria-multiselectable="true"
      onWheel={(event) => {
        event.preventDefault();
        if (interactionRef.current) return;
        if (event.ctrlKey || event.metaKey) {
          const rect = stageRef.current?.getBoundingClientRect();
          if (!rect) return;
          const point = toStageScreenPoint(event, rect, getStageScroll(stageRef.current));
          canvasViewport.zoomBy(event.deltaY < 0 ? 0.1 : -0.1, point);
          return;
        }
        canvasViewport.panBy({ x: -event.deltaX, y: -event.deltaY });
      }}
      style={{ ...buildWorldStyle(session.canvas.gridVisible, session.canvas.gridSize), touchAction: 'none' }}
    >
      {session.canvas.device ? <div aria-label={`${studioText('devicePreview')} ${session.canvas.device.width}×${session.canvas.device.height}`} className="page-studio__device-status" data-device-preview-status="true">{session.canvas.device.id} · {session.canvas.device.width}×{session.canvas.device.height}</div> : null}
      <div className={`absolute inset-0 ${session.canvas.device ? 'page-studio__device-world' : ''}`} data-canvas-world="true">
        <div className={`absolute origin-top-left ${session.canvas.device ? 'page-studio__device-frame' : ''}`} style={{ left: session.canvas.device ? '50%' : 0, top: session.canvas.device ? 36 : 0, transform: `translate(${session.canvas.device ? pan.x - stageSize.width / 2 : pan.x}px, ${pan.y}px) scale(${zoom})`, width: stageSize.width, minHeight: stageSize.height }}>
          <DesignerCanvasNode activeNodeId={rovingNodeId} bindingDocument={bindingDocument} canvasText={canvasText} collapsedNodeIds={collapsedNodeIds} commandBus={commandBus} document={document} dropOperation={dropOperation} dropTarget={activeDropTarget} geometry={geometry} inlineEditingNodeId={inlineEditingNodeId} isRoot level={1} manifests={manifests} nodeId={rootId} nodeRefs={nodeRefs} onCommandResult={onCommandResult} onContextMenu={(nodeId, event) => { event.preventDefault(); event.stopPropagation(); setContextMenu({ nodeId, x: event.clientX, y: event.clientY }); }} onInlineEdit={setInlineEditingNodeId} onPointerStart={(event, nodeId) => session.canvas.tool === 'hand' ? beginStageInteraction(event, stageRef.current, rootId) : beginNodeInteraction(event, nodeId)} onResizeStart={beginResizeInteraction} onSelect={(nodeId, additive, penetrate) => { setActiveNodeId(nodeId); onNodeSelected?.(nodeId); (penetrate ? selectThrough : select)(nodeId, additive); }} onToggleExpanded={(nodeId) => setCollapsedNodeIds((current) => current.has(nodeId) ? new Set([...current].filter((id) => id !== nodeId)) : new Set(current).add(nodeId))} previewError={previewError} previewKernel={previewKernel} previewLayoutContext={previewLayoutContext} previewRects={previewRects} previewState={previewState} runtimePreview={runtimePreview} runtimePreviewRef={runtimePreviewRef} runtimeRects={runtimeRects} selectedNodeIds={session.selectedNodeIds} studioText={studioText} />
           <DesignerCanvasOverlay label={studioText('canvasOverlays')}>
             {marquee ? <div aria-label={studioText('selectionMarquee')} className="page-studio__selection-marquee" data-canvas-transient-overlay="true" data-selection-marquee="true" style={{ left: marquee.x, top: marquee.y, width: marquee.width, height: marquee.height }} /> : null}
             {guides.map((guide, index) => <div key={`${guide.axis}-${guide.position}-${index}`} className="page-studio__snap-guide" data-canvas-transient-overlay="true" data-snap-guide={guide.axis} style={guide.axis === 'x' ? { left: guide.position, top: 0, width: 1, height: '100%' } : { left: 0, top: guide.position, width: '100%', height: 1 }} />)}
             {session.canvas.guides.map((guide) => <div aria-label={`${studioText('userGuide')} ${guide.id}`} className="page-studio__user-guide" data-canvas-transient-overlay="true" data-user-guide={guide.axis} key={guide.id} style={guide.axis === 'x' ? { left: guide.position, top: 0, width: 1, height: '100%' } : { left: 0, top: guide.position, width: '100%', height: 1 }} />)}
           </DesignerCanvasOverlay>
        {session.canvas.device ? <><div aria-label={studioText('browserSimulation')} className="page-studio__browser-simulation" data-browser-bar="true" style={{ height: session.canvas.device.browserBar.top }} /><div aria-label={studioText('safeAreaSimulation')} className="page-studio__safe-area" data-safe-area="true" style={{ bottom: session.canvas.device.safeArea.bottom, left: session.canvas.device.safeArea.left, right: session.canvas.device.safeArea.right, top: session.canvas.device.safeArea.top }} /></> : null}
      </div>
      </div>
      <CanvasRulers session={session} stageSize={stageSize} text={studioText} />
      {session.canvas.minimapVisible ? <CanvasMinimap board={rootRect} controller={canvasViewport} stage={stageBounds} text={studioText} viewport={viewport} /> : null}
      {contextMenu ? <CanvasContextMenu locked={isNodeLocked(document.elements[contextMenu.nodeId])} point={{ x: contextMenu.x, y: contextMenu.y }} text={studioText} onAction={handleContextAction} onClose={closeContextMenu} /> : null}
    </section>
  );

  function beginStageInteraction(event: ReactPointerEvent<HTMLElement>, stage: HTMLElement | null, pageRootId: string) {
    if (event.target !== event.currentTarget && !(event.target as HTMLElement).closest('[data-canvas-world="true"]')) return;
    if (!stage) return;
    const rect = stage.getBoundingClientRect();
    if (!rect) return;
    event.preventDefault();
    const panMode = event.button === 1 || session.canvas.tool === 'hand' || spacePressed || event.pointerType === 'touch';
    const scroll = getStageScroll(stage);
    const coordinates = toPointerCoordinates(event, rect, viewport, scroll, canvasViewport.coordinateFrame);
    const point = panMode ? coordinates.stageScreenPoint : coordinates.worldPoint;
    const kind = panMode ? 'pan' : 'select';
    const transaction = beginPointerTransaction(kind, event.pointerId, point, []);
    const additive = event.shiftKey || event.metaKey || event.ctrlKey;
    beginInteraction({ transaction, mode: panMode ? 'pan' : 'marquee', nodeIds: [], scroll, stage, stageRect: rect, viewport, viewportSnapshot: captureCanvasInteractionViewport(viewport, rect, stageSize, scroll), additive, snapEnabled: !event.altKey, snapGridSize: session.canvas.gridSize, snapThreshold: session.canvas.snapThreshold, selectionAtStart: { selectedNodeIds: [...session.selectedNodeIds], primaryNodeId: session.primaryNodeId, anchorNodeId: session.anchorNodeId } });
    canvasViewport.beginInteraction();
    if (!panMode) { patchSelection(createSelectionForBlank(additive, session)); onCanvasBlank?.(); }
    if (pageRootId) event.currentTarget.setPointerCapture?.(event.pointerId);
    sessionStore.patch({ transactionId: `pointer:${event.pointerId}` });
  }

  function beginNodeInteraction(event: ReactPointerEvent<HTMLElement>, nodeId: string) {
    if (event.button !== 0 || (event.target as HTMLElement).closest('[data-resize-handle],[data-binding-slot],[data-canvas-interaction-control],[contenteditable="true"],textarea[data-inline-editor="true"]')) return;
    if (isNodeLocked(document.elements[nodeId])) return;
    const rect = stageRef.current?.getBoundingClientRect();
    if (!rect) return;
    event.preventDefault();
    const stage = stageRef.current;
    if (!stage) return;
    const scroll = getStageScroll(stage);
    const additive = event.shiftKey || event.metaKey || event.ctrlKey;
    const wasSelected = session.selectedNodeIds.includes(nodeId);
    const selectedIds = wasSelected
      ? [...session.selectedNodeIds]
      : additive
        ? [...new Set([...session.selectedNodeIds, nodeId])]
        : [nodeId];
    onNodeSelected?.(nodeId);
    if (!wasSelected) select(nodeId, additive);
    const movableIds = getTopLevelSelectedNodeIds(selectedIds, document.elements);
    const snapshots = movableIds.map((id) => ({ id, rect: geometry.rects[id] })).filter((snapshot): snapshot is { id: string; rect: CanvasRect } => Boolean(snapshot.rect));
    if (snapshots.length === 0) return;
    beginInteraction({ transaction: beginPointerTransaction('move', event.pointerId, toPointerCoordinates(event, rect, viewport, scroll, canvasViewport.coordinateFrame).worldPoint, snapshots), mode: 'node-move', nodeIds: movableIds, scroll, stage, stageRect: rect, viewport, viewportSnapshot: captureCanvasInteractionViewport(viewport, rect, stageSize, scroll), geometrySnapshot: { content: rootRect, parentOrigins: geometry.parentOrigins }, additive, snapEnabled: !event.altKey, snapGridSize: session.canvas.gridSize, snapThreshold: session.canvas.snapThreshold, toggleNodeId: wasSelected && additive ? nodeId : undefined, pointerSelection: wasSelected && additive ? 'toggle-if-click' : 'preserve', selectionAtStart: { selectedNodeIds: [...session.selectedNodeIds], primaryNodeId: session.primaryNodeId, anchorNodeId: session.anchorNodeId } });
    canvasViewport.beginInteraction();
    sessionStore.patch({ transactionId: `pointer:${event.pointerId}` });
    event.currentTarget.setPointerCapture?.(event.pointerId);
  }

  function beginResizeInteraction(event: ReactPointerEvent<HTMLElement>, nodeId: string, handle: ResizeHandle) {
    event.stopPropagation();
    const rect = stageRef.current?.getBoundingClientRect();
    const nodeRect = geometry.rects[nodeId];
    if (!rect || !nodeRect) return;
    event.preventDefault();
    const stage = stageRef.current;
    if (!stage) return;
    const scroll = getStageScroll(stage);
    beginInteraction({ transaction: beginPointerTransaction('resize', event.pointerId, toPointerCoordinates(event, rect, viewport, scroll, canvasViewport.coordinateFrame).worldPoint, [{ id: nodeId, rect: nodeRect }], handle), mode: 'resize', nodeIds: [nodeId], scroll, stage, stageRect: rect, viewport, viewportSnapshot: captureCanvasInteractionViewport(viewport, rect, stageSize, scroll), geometrySnapshot: { content: rootRect, parentOrigins: geometry.parentOrigins }, additive: false, snapEnabled: !event.altKey, snapGridSize: session.canvas.gridSize, snapThreshold: session.canvas.snapThreshold, selectionAtStart: { selectedNodeIds: [...session.selectedNodeIds], primaryNodeId: session.primaryNodeId, anchorNodeId: session.anchorNodeId } });
    canvasViewport.beginInteraction();
    sessionStore.patch({ transactionId: `pointer:${event.pointerId}` });
    event.currentTarget.setPointerCapture?.(event.pointerId);
  }

  function createSelectionForBlank(additive: boolean, currentSession: typeof session): CanvasSelection {
    return additive ? { selectedNodeIds: [...currentSession.selectedNodeIds], primaryNodeId: currentSession.primaryNodeId, anchorNodeId: currentSession.anchorNodeId } : { selectedNodeIds: [], primaryNodeId: null, anchorNodeId: null };
  }

}

function DesignerCanvasNode({ activeNodeId, bindingDocument, canvasText, collapsedNodeIds, commandBus, document, dropOperation, dropTarget, geometry, inlineEditingNodeId, isRoot = false, level, manifests, nodeId, nodeRefs, onCommandResult, onContextMenu, onInlineEdit, onPointerStart, onResizeStart, onSelect, onToggleExpanded, previewError, previewKernel, previewLayoutContext, previewRects, previewState, runtimePreview, runtimePreviewRef, runtimeRects, selectedNodeIds, studioText }: { activeNodeId?: string; bindingDocument?: BindingDocument | null; canvasText: { previewInvalid: string; unknownComponent: string; previewPreparing: string }; collapsedNodeIds: ReadonlySet<string>; commandBus: DesignerCommandBus; document: DesignerDocument; dropOperation: 'insert' | 'move'; dropTarget: ComponentInsertionTarget | CanvasDropTarget | null; geometry: CanvasGeometry; inlineEditingNodeId: string | null; isRoot?: boolean; level: number; manifests: ComponentRegistry; nodeId: string; nodeRefs: React.MutableRefObject<Map<string, HTMLDivElement>>; onCommandResult?: (result: DesignerCommandResult) => void; onContextMenu: (nodeId: string, event: ReactMouseEvent<HTMLElement>) => void; onInlineEdit: (nodeId: string | null) => void; onPointerStart: (event: ReactPointerEvent<HTMLElement>, nodeId: string) => void; onResizeStart: (event: ReactPointerEvent<HTMLButtonElement>, nodeId: string, handle: ResizeHandle) => void; onSelect: (nodeId: string, additive: boolean, penetrate?: boolean) => void; onToggleExpanded: (nodeId: string) => void; previewError: string | null; previewKernel: RuntimeKernel | null; previewLayoutContext: RuntimeLayoutContext; previewRects: Readonly<Record<string, CanvasRect>>; previewState: RuntimeScopeState; runtimePreview: RuntimeContext | null; runtimePreviewRef: React.MutableRefObject<HTMLDivElement | null>; runtimeRects: Readonly<Record<string, CanvasRect>>; selectedNodeIds: readonly string[]; studioText: (key: string) => string }) {
  const sourceNode = document.elements[nodeId];
  const dropTargetRef = useRef<HTMLDivElement>(null);
  const node = sourceNode ? geometry.nodes[nodeId] ?? sourceNode : null;
  const manifest = node ? manifests.get(node.type) : undefined;
  const bindResourceToSlot = useCallback((resourceId: string, requestedProperty?: string) => {
    if (!node || !bindingDocument || !manifest?.binding.slots?.length) return;
    const resource = listStableResources(bindingDocument).find((item) => item.id === resourceId);
    const slot = manifest.binding.slots.find((candidate) => candidate.property === requestedProperty)
      ?? manifest.binding.slots.find((candidate) => resource && isDesignerValueType(candidate.valueType) && createConversionPipeline(resource.valueType, candidate.valueType).valid);
    if (!resource || !slot || !isDesignerValueType(slot.valueType)) return;
    const result = commandBus.execute(createBindValueCommand(node.id, slot.property, resourceReferenceFor(resource, slot.valueType)));
    onCommandResult?.(result);
  }, [bindingDocument, commandBus, manifest, node, onCommandResult]);
  useEffect(() => {
    const target = dropTargetRef.current;
    if (!target || !node || !bindingDocument || !manifest?.binding.slots?.length) return undefined;
    const handleResourceDrop = (event: Event) => {
      const detail = (event as CustomEvent<{ bindingSlot?: string; resourceId?: string }>).detail;
      if (!detail?.resourceId) return;
      const requestedProperty = detail.bindingSlot ?? (event.target as HTMLElement | null)?.closest<HTMLElement>('[data-binding-slot]')?.dataset.bindingSlot;
      if (!requestedProperty && (event.target as HTMLElement | null)?.closest('[data-binding-slot]')) return;
      event.stopPropagation();
      bindResourceToSlot(detail.resourceId, requestedProperty);
    };
    target.addEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
    return () => target.removeEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
  }, [bindResourceToSlot, bindingDocument, manifest, node]);
  if (!sourceNode || !node) return null;
  const selected = !isRoot && selectedNodeIds.includes(node.id);
  const locked = isNodeLocked(node);
  const layout = { ...(manifest?.defaults.layout ?? {}), ...node.layout };
  const resolvedStyle = node.style ?? {};
  const measuredRuntimeRect = runtimeRects[node.id];
  const runtimePreviewActive = Boolean(runtimePreview);
  const actual = measuredRuntimeRect && measuredRuntimeRect.width > 1 && measuredRuntimeRect.height > 1
    ? measuredRuntimeRect
    : runtimePreviewActive
      ? undefined
      : previewRects[node.id] ?? geometry.rects[node.id];
  const hasRuntimeRect = Object.prototype.hasOwnProperty.call(runtimeRects, node.id);
  const position = Boolean(!isRoot && actual);
  const parentRuntimeRect = node.parentId ? runtimeRects[node.parentId] : undefined;
  const parentOrigin = parentRuntimeRect ? { x: parentRuntimeRect.x, y: parentRuntimeRect.y } : geometry.parentOrigins[node.id] ?? { x: 0, y: 0 };
  const projected = projectRuntimeLayout({
    box: actual ? { height: actual.height, width: actual.width, x: actual.x - parentOrigin.x, y: actual.y - parentOrigin.y } : undefined,
    forceAbsolute: position,
    layout,
    parentLayout: node.parentId ? geometry.nodes[node.parentId]?.layout : undefined,
    style: { ...(manifest?.defaults.style ?? {}), ...resolvedStyle }
  });
  const style: CSSProperties = isRoot
    ? { height: geometry.rects[node.id]?.height ?? DEFAULT_STAGE_SIZE.height, position: 'relative', width: geometry.rects[node.id]?.width ?? DEFAULT_STAGE_SIZE.width }
    : {
      background: 'transparent',
      border: '1px solid transparent',
      boxSizing: 'border-box',
      display: 'block',
      height: actual?.height,
      left: projected.style.left as CSSProperties['left'],
      overflow: 'visible',
      padding: 0,
      pointerEvents: actual ? 'auto' : 'none',
      position: 'absolute',
      top: projected.style.top as CSSProperties['top'],
      width: actual?.width,
      zIndex: selected ? 20 : hasRuntimeRect ? 2 : undefined
    };
  const children = node.children.filter((childId) => Boolean(document.elements[childId]));
  const expanded = children.length > 0 && !collapsedNodeIds.has(node.id);
  const ownsEventTarget = (target: EventTarget | null) => {
    const targetElement = (target as HTMLElement | null)?.closest<HTMLElement>('[data-node-id],[data-runtime-element-id]');
    return targetElement?.dataset.nodeId === node.id || targetElement?.dataset.runtimeElementId === node.id;
  };
  const handleNodePointerDownCapture = (event: ReactPointerEvent<HTMLElement>) => {
    if (isRoot || !ownsEventTarget(event.target) || isCanvasInteractionControl(event.target)) return;
    event.stopPropagation();
    onPointerStart(event, node.id);
  };
  const handleNodeClickCapture = (event: ReactMouseEvent<HTMLElement>) => {
    if (isRoot || !ownsEventTarget(event.target) || isCanvasInteractionControl(event.target)) return;
    event.preventDefault();
    event.stopPropagation();
    if (event.detail === 0 && !locked) onSelect(node.id, event.shiftKey || event.metaKey || event.ctrlKey, event.altKey);
  };
  const handleNodeDoubleClickCapture = (event: ReactMouseEvent<HTMLElement>) => {
    if (isRoot || !ownsEventTarget(event.target) || isCanvasInteractionControl(event.target)) return;
    event.preventDefault();
    event.stopPropagation();
    if (children.length > 0) onToggleExpanded(node.id);
    else if (manifest?.capability.capabilities.includes('text') || manifest?.capability.capabilities.includes('content') || (manifest?.type.startsWith('action.') && typeof sourceNode.props.text === 'string')) onInlineEdit(node.id);
  };
  return (
      <div ref={(element) => { dropTargetRef.current = element; if (element) nodeRefs.current.set(node.id, element); else nodeRefs.current.delete(node.id); }} aria-disabled={!isRoot && locked ? true : undefined} aria-expanded={!isRoot && children.length > 0 ? expanded : undefined} aria-label={isRoot ? studioText('pageArtboard') : node.name || node.type} aria-level={isRoot ? undefined : level} aria-selected={isRoot ? undefined : selected} className={isRoot ? 'page-studio__artboard' : `page-studio__canvas-node ${activeNodeId === node.id ? 'is-active' : ''} ${selected ? 'is-selected' : ''} ${locked ? 'is-locked' : ''}`} data-binding-target={manifest?.binding.slots?.[0]?.property} data-canvas-artboard={isRoot || undefined} data-node-id={isRoot ? undefined : node.id} data-node-locked={!isRoot && locked ? 'true' : undefined} data-node-type={node.type} data-preview-renderer={manifest?.runtime.renderer ?? 'unknown'} data-resource-drop-target={manifest?.binding.slots?.length ? 'true' : undefined} role={isRoot ? undefined : 'treeitem'} style={style} tabIndex={isRoot ? undefined : activeNodeId === node.id ? 0 : -1} onClickCapture={handleNodeClickCapture} onContextMenuCapture={isRoot ? undefined : (event) => { if (!ownsEventTarget(event.target)) return; event.preventDefault(); event.stopPropagation(); onContextMenu(node.id, event); }} onDoubleClickCapture={handleNodeDoubleClickCapture} onPointerDownCapture={handleNodePointerDownCapture}>
      {selected && inlineEditingNodeId !== node.id && !isRoot ? <div className="page-studio__selection-label">{node.name || node.type.split('.').pop()}</div> : null}
      {inlineEditingNodeId === node.id ? <textarea autoFocus aria-label={studioText('inlineEditor')} className="page-studio__inline-editor" data-inline-editor="true" defaultValue={String(sourceNode.props.text ?? sourceNode.props.content ?? '')} onBlur={(e) => { const val = e.target.value; const prop = sourceNode.props.text !== undefined ? 'text' : 'content'; onCommandResult?.(commandBus.execute(createPatchNodeCommand(node.id, { props: { ...sourceNode.props, [prop]: val } }))); onInlineEdit(null); }} onClick={(e) => e.stopPropagation()} onDoubleClick={(e) => e.stopPropagation()} onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); e.currentTarget.blur(); } else if (e.key === 'Escape') { e.preventDefault(); onInlineEdit(null); } }} onPointerDown={(e) => e.stopPropagation()} /> : null}
      {dropTarget?.targetNodeId === node.id && dropTarget.placement === 'before' ? <div aria-label={studioText('insertBefore')} className="page-studio__drop-line page-studio__drop-line--before" data-canvas-transient-overlay="true" data-insertion-indicator="before" /> : null}
      {level === 1 && previewError ? <CanvasPreviewDiagnostic detail={previewError} label={canvasText.previewInvalid} /> : null}
      {isRoot && runtimePreview ? <div ref={runtimePreviewRef} className="page-studio__runtime-preview" data-designer-runtime-preview="true"><RuntimeComponentTreePreview kernel={previewKernel!} layoutContext={previewLayoutContext} runtime={runtimePreview} state={previewState} /></div> : null}
      {isRoot && !runtimePreview && !previewError ? <div className="page-studio__preview-preparing">{canvasText.previewPreparing}</div> : null}
      {dropTarget?.targetNodeId === node.id && (dropTarget.placement === 'inside' || dropTarget.placement === 'free-position') ? <div aria-label={studioText(dropOperation === 'move' ? 'moveInside' : 'insertInside')} className="page-studio__drop-surface" data-canvas-transient-overlay="true" data-insertion-indicator={dropTarget.placement} /> : null}
      {isRoot && dropTarget?.targetNodeId === node.id && (dropTarget.placement === 'inside' || dropTarget.placement === 'free-position') ? <div className="page-studio__drop-hint" data-canvas-transient-overlay="true">{studioText(dropOperation === 'move' ? 'dropToMove' : 'dropToAdd')}</div> : null}
      {selected && manifest?.binding.slots?.length ? <CanvasBindingSlots onResourceDrop={bindResourceToSlot} slots={manifest.binding.slots} text={studioText} /> : null}
      {dropTarget?.targetNodeId === node.id && dropTarget.placement === 'after' ? <div aria-label={studioText('insertAfter')} className="page-studio__drop-line page-studio__drop-line--after" data-canvas-transient-overlay="true" data-insertion-indicator="after" /> : null}
      {expanded ? children.map((childId) => <DesignerCanvasNode key={childId} activeNodeId={activeNodeId} bindingDocument={bindingDocument} canvasText={canvasText} collapsedNodeIds={collapsedNodeIds} commandBus={commandBus} document={document} dropOperation={dropOperation} dropTarget={dropTarget} geometry={geometry} inlineEditingNodeId={inlineEditingNodeId} level={level + 1} manifests={manifests} nodeId={childId} nodeRefs={nodeRefs} onCommandResult={onCommandResult} onContextMenu={onContextMenu} onInlineEdit={onInlineEdit} onPointerStart={onPointerStart} onResizeStart={onResizeStart} onSelect={onSelect} onToggleExpanded={onToggleExpanded} previewError={previewError} previewKernel={previewKernel} previewLayoutContext={previewLayoutContext} previewRects={previewRects} previewState={previewState} runtimePreview={runtimePreview} runtimePreviewRef={runtimePreviewRef} runtimeRects={runtimeRects} selectedNodeIds={selectedNodeIds} studioText={studioText} />) : null}
      {selected && !isRoot ? <ResizeHandles nodeId={node.id} studioText={studioText} onResize={(event, handle) => onResizeStart(event, node.id, handle)} /> : null}
    </div>
  );
}

function getVisibleNodeIds(document: DesignerDocument, rootId: string | undefined, collapsedNodeIds: ReadonlySet<string>): string[] {
  const ids: string[] = [];
  const visit = (nodeId: string) => {
    const node = document.elements[nodeId];
    if (!node) return;
    ids.push(nodeId);
    if (!collapsedNodeIds.has(nodeId)) node.children.forEach((childId) => visit(childId));
  };
  if (rootId) visit(rootId);
  return ids;
}

function buildWorldStyle(gridVisible: boolean, gridSize: number): CSSProperties {
  if (!gridVisible) return {};
  return { backgroundImage: 'linear-gradient(to right, color-mix(in srgb, var(--app-border) 55%, transparent) 1px, transparent 1px), linear-gradient(to bottom, color-mix(in srgb, var(--app-border) 55%, transparent) 1px, transparent 1px)', backgroundSize: `${gridSize}px ${gridSize}px` };
}

function isNodeLocked(node: DesignerDocumentNode | undefined): boolean {
  return node?.locked === true || node?.layout.locked === true || node?.props.locked === true;
}

function isCanvasInteractionControl(target: EventTarget | null): boolean {
  return Boolean((target as HTMLElement | null)?.closest('[data-resize-handle],[data-binding-slot],[data-canvas-interaction-control],[contenteditable="true"],textarea[data-inline-editor="true"]'));
}

function resolvePenetrationTarget(nodeId: string, currentNodeId: string | null, elements: Readonly<Record<string, DesignerDocumentNode>>, cycle: boolean): string {
  const candidates: string[] = [];
  let current: DesignerDocumentNode | undefined = elements[nodeId];
  while (current) {
    if (!isNodeLocked(current)) candidates.push(current.id);
    current = current.parentId ? elements[current.parentId] : undefined;
  }
  if (candidates.length === 0) return nodeId;
  if (!cycle) return candidates[0];
  const currentIndex = currentNodeId ? candidates.indexOf(currentNodeId) : -1;
  return candidates[(currentIndex + 1 + candidates.length) % candidates.length];
}

function ResizeHandles({ nodeId, onResize, studioText }: { nodeId: string; onResize: (event: ReactPointerEvent<HTMLButtonElement>, handle: ResizeHandle) => void; studioText: (key: string) => string }) {
  const handles: ResizeHandle[] = ['northwest', 'north', 'northeast', 'west', 'east', 'southwest', 'south', 'southeast'];
  return <div className="page-studio__resize-handles" data-canvas-transient-overlay="true" data-selection-overlay="true" style={{ inset: 0, pointerEvents: 'none', position: 'absolute' }}>{handles.map((handle) => <button key={handle} aria-label={`${studioText('resizeHandle')} ${nodeId} ${handle}`} className="page-studio__resize-handle" data-canvas-interaction-control="true" data-resize-handle={handle} style={{ ...handleStyle(handle), pointerEvents: 'auto' }} type="button" onPointerDown={(event) => onResize(event, handle)} />)}</div>;
}

function CanvasPreviewDiagnostic({ detail, label }: { detail: string; label: string }) {
  return <div className="page-studio__preview-diagnostic" role="status"><span className="font-semibold">{label}</span><span className="min-w-0 truncate" title={detail}>{detail}</span></div>;
}

function isDesignerValueType(value: string): value is DesignerValueType {
  return ['array', 'boolean', 'date', 'json', 'number', 'object', 'string'].includes(value);
}

function CanvasBindingSlots({ onResourceDrop, slots, text }: { onResourceDrop: (resourceId: string, bindingSlot?: string) => void; slots: ReadonlyArray<{ label: string; property: string; valueType: string }>; text: (key: string) => string }) {
  const targetRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const target = targetRef.current;
    if (!target) return undefined;
    const handleResourceDrop = (event: Event) => {
      const detail = (event as CustomEvent<{ bindingSlot?: string; resourceId?: string }>).detail;
      const slot = (event.target as HTMLElement | null)?.closest<HTMLElement>('[data-binding-slot]')?.dataset.bindingSlot;
      if (!detail?.resourceId || !slot) return;
      event.stopPropagation();
      onResourceDrop(detail.resourceId, slot);
    };
    target.addEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
    return () => target.removeEventListener(RESOURCE_DROP_EVENT, handleResourceDrop);
  }, [onResourceDrop]);
  return <div ref={targetRef} aria-label={text('bindingSlots')} className="page-studio__binding-slots" role="group">
    {slots.map((slot) => <span key={slot.property} aria-label={`${text('bindSlot')} ${slot.label}`} className="page-studio__binding-slot" data-binding-slot={slot.property} data-resource-drop-target="true" role="button" tabIndex={0}>{slot.label}</span>)}
  </div>;
}
function handleStyle(handle: ResizeHandle): CSSProperties {
  const center = { transform: 'translate(-50%, -50%)' };
  if (handle === 'northwest') return { left: 0, top: 0, ...center }; if (handle === 'north') return { left: '50%', top: 0, ...center }; if (handle === 'northeast') return { right: 0, top: 0, ...center }; if (handle === 'west') return { left: 0, top: '50%', ...center }; if (handle === 'east') return { right: 0, top: '50%', ...center }; if (handle === 'southwest') return { left: 0, bottom: 0, ...center }; if (handle === 'south') return { left: '50%', bottom: 0, ...center }; return { right: 0, bottom: 0, ...center };
}
function toStageScreenPoint(event: { clientX: number; clientY: number }, stageRect: DOMRect, scroll: CanvasPoint = { x: 0, y: 0 }): CanvasPoint { return clientToStageScreen({ x: event.clientX, y: event.clientY }, stageRect, scroll); }
function toPointerCoordinates(event: { clientX: number; clientY: number }, stageRect: DOMRect, viewport: CanvasViewport, scroll: CanvasPoint = { x: 0, y: 0 }, frame: CanvasFrameGeometry = { origin: { x: 0, y: 0 }, border: { top: 0, right: 0, bottom: 0, left: 0 }, browserBar: { top: 0, right: 0, bottom: 0, left: 0 }, safeArea: { top: 0, right: 0, bottom: 0, left: 0 } }): { clientPoint: CanvasPoint; stageScreenPoint: CanvasPoint; worldPoint: CanvasPoint } {
  const clientPoint = { x: event.clientX, y: event.clientY };
  const stageScreenPoint = toStageScreenPoint(event, stageRect, scroll);
  return { clientPoint, stageScreenPoint, worldPoint: screenToWorld(stageScreenPoint, viewport, frame) };
}
function containsClientPoint(element: HTMLElement, clientX: number, clientY: number): boolean {
  const rect = element.getBoundingClientRect();
  return clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom;
}
function getStageScroll(stage: HTMLElement | null): CanvasPoint { return { x: stage && Number.isFinite(stage.scrollLeft) ? stage.scrollLeft : 0, y: stage && Number.isFinite(stage.scrollTop) ? stage.scrollTop : 0 }; }
function hasPointerMovement(rects: readonly CanvasRect[], snapshots: readonly { id: string; rect: CanvasRect }[], threshold = 0): boolean {
  const initial = new Map(snapshots.map((snapshot) => [snapshot.id, snapshot.rect]));
  return rects.some((rect) => {
    const before = rect.id ? initial.get(rect.id) : undefined;
    return Boolean(before && Math.hypot(before.x - rect.x, before.y - rect.y) > threshold);
  });
}

function mergeRects(rects: readonly CanvasRect[]): CanvasRect | null {
  if (rects.length === 0) return null;
  const left = Math.min(...rects.map((rect) => rect.x));
  const top = Math.min(...rects.map((rect) => rect.y));
  const right = Math.max(...rects.map((rect) => rect.x + rect.width));
  const bottom = Math.max(...rects.map((rect) => rect.y + rect.height));
  return { height: bottom - top, width: right - left, x: left, y: top };
}

function collectMoveSubtreeIds(elements: Readonly<Record<string, DesignerDocumentNode>>, nodeIds: readonly string[]): ReadonlySet<string> {
  const result = new Set<string>();
  const visit = (nodeId: string) => {
    if (result.has(nodeId)) return;
    result.add(nodeId);
    elements[nodeId]?.children.forEach(visit);
  };
  nodeIds.forEach(visit);
  return result;
}

function getTopLevelSelectedNodeIds(nodeIds: readonly string[], elements: Readonly<Record<string, DesignerDocumentNode>>): string[] {
  const selected = new Set(nodeIds);
  return nodeIds.filter((nodeId) => !hasSelectedAncestor(nodeId, selected, elements));
}

function hasSelectedAncestor(nodeId: string, selected: ReadonlySet<string>, elements: Readonly<Record<string, DesignerDocumentNode>>): boolean {
  const visited = new Set<string>();
  let parentId = elements[nodeId]?.parentId ?? null;
  while (parentId) {
    if (selected.has(parentId)) return true;
    if (visited.has(parentId)) return true;
    visited.add(parentId);
    parentId = elements[parentId]?.parentId ?? null;
  }
  return false;
}

export function buildGeometry(document: DesignerDocument, rootId?: string, canvasSize: CanvasRect = { x: 0, y: 0, width: DEFAULT_STAGE_SIZE.width, height: DEFAULT_STAGE_SIZE.height }, responsiveBreakpoint: ResponsiveBreakpoint | null = null, responsiveBreakpoints: readonly ResponsiveBreakpoint[] = [], runtimeElements?: Readonly<Record<string, { layout?: Record<string, unknown> }>>): CanvasGeometry {
  const nodes: Record<string, DesignerDocumentNode> = {}; const rects: Record<string, CanvasRect> = {}; const parentOrigins: Record<string, CanvasPoint> = {}; const layoutModes: Record<string, ReturnType<typeof resolveLayoutMode>> = {};
  if (!rootId) return { nodes, rects, parentOrigins, layoutModes, spatialIndex: createCanvasSpatialIndex([]) };
  const visit = (id: string, parentRect: CanvasRect, parentOrigin: CanvasPoint, siblingIndex = 0, siblings: readonly DesignerDocumentNode[] = [], parentNode?: DesignerDocumentNode) => {
    const sourceNode = document.elements[id];
    if (!sourceNode) return;
    const node = resolveCanvasNode(sourceNode, responsiveBreakpoint, responsiveBreakpoints);
    const runtimeLayout = runtimeElements?.[id]?.layout;
    const resolvedNode = runtimeLayout ? { ...node, layout: runtimeLayout } : node;
    nodes[id] = resolvedNode;
    const local = siblings.length > 0
      ? resolveRuntimeLayoutBox(resolvedNode.layout, parentRect, parentNode?.layout, siblings.map((sibling) => runtimeElements?.[sibling.id]?.layout ?? sibling.layout), siblingIndex)
      : resolveRuntimeLayoutBox(resolvedNode.layout, parentRect, undefined, [], 0, true);
    const rect = { id, x: parentRect.x + local.x, y: parentRect.y + local.y, width: local.width, height: local.height };
    rects[id] = rect;
    parentOrigins[id] = parentOrigin;
    layoutModes[id] = resolveLayoutMode(resolvedNode.layout);
    const children = resolvedNode.children.map((childId) => document.elements[childId]).filter((child): child is DesignerDocumentNode => Boolean(child)).map((child) => {
      const resolvedChild = resolveCanvasNode(child, responsiveBreakpoint, responsiveBreakpoints);
      const childRuntimeLayout = runtimeElements?.[child.id]?.layout;
      return childRuntimeLayout ? { ...resolvedChild, layout: childRuntimeLayout } : resolvedChild;
    });
    children.forEach((child, index) => visit(child.id, rect, { x: rect.x, y: rect.y }, index, children, resolvedNode));
  };
  visit(rootId, canvasSize, { x: canvasSize.x, y: canvasSize.y }, 0, [], undefined);
  return { nodes, rects, parentOrigins, layoutModes, spatialIndex: createCanvasSpatialIndex(Object.values(rects)) };
}

function resolveCanvasNode(node: DesignerDocumentNode, breakpoint: ResponsiveBreakpoint | null, breakpoints: readonly ResponsiveBreakpoint[]): DesignerDocumentNode {
  if (!breakpoint) return node;
  const resolved = resolveResponsiveNode({ base: { layout: node.layout, props: node.props, style: node.style ?? {} }, responsiveOverrides: node.responsiveOverrides ?? {} }, breakpoint, breakpoints);
  return { ...node, layout: isRecord(resolved.layout) ? resolved.layout : node.layout, props: isRecord(resolved.props) ? resolved.props : node.props, style: isRecord(resolved.style) ? resolved.style : node.style };
}
export function toResponsiveGeometryPatches(
  patches: Record<string, Partial<DesignerDocumentNode>>,
  kind: 'move' | 'resize' | 'pan' | 'select',
  sourceElements: Record<string, DesignerDocumentNode>,
  resolvedElements: Record<string, DesignerDocumentNode>,
  breakpoint: ResponsiveBreakpoint,
  breakpoints: readonly ResponsiveBreakpoint[]
): Record<string, Partial<DesignerDocumentNode>> {
  const fields = kind === 'resize' ? ['position', 'x', 'y', 'width', 'height', 'constraints'] : ['position', 'x', 'y', 'constraints'];
  return Object.fromEntries(Object.entries(patches).flatMap(([id, patch]) => {
    const source = sourceElements[id];
    const resolved = resolvedElements[id] ?? source;
    if (!source || !resolved || !isRecord(patch.layout)) return [];
    const normalized = normalizeResponsiveOverrideMap(source.responsiveOverrides).overrides;
    const existingLayout = normalized[breakpoint.id]?.layout;
    const existing = isRecord(existingLayout) ? existingLayout : {};
    const inherited = resolveResponsiveInheritedSections({
      base: { layout: source.layout, props: source.props, style: source.style ?? {} },
      responsiveOverrides: normalized
    }, breakpoint, breakpoints).layout ?? {};
    const target = Object.fromEntries(fields
      .filter((field) => field in patch.layout! || field in existing)
      .map((field) => [field, field in patch.layout! ? patch.layout![field] : undefined]));
    const base = Object.fromEntries(fields.filter((field) => field in inherited).map((field) => [field, inherited[field]]));
    const layout = createOverridePatch(existing, base, target);
    return Object.keys(layout).length > 0 ? [[id, { layout } as Partial<DesignerDocumentNode>]] : [];
  }));
}
function numberValue(value: unknown, fallback = 0): number { return typeof value === 'number' && Number.isFinite(value) ? value : fallback; }
function snapMovingRects(rects: readonly CanvasRect[], geometry: CanvasGeometry, selected: readonly string[], options: { enabled: boolean; gridSize: number; threshold: number }): CanvasRect[] { const primary = rects[0]; if (!primary) return []; const peerRects = geometry.spatialIndex.query(primary, options.threshold + options.gridSize).filter((peer) => !selected.includes(peer.id ?? '')); const snap = snapRectWithOptions(primary, peerRects, options); const dx = snap.point.x - primary.x; const dy = snap.point.y - primary.y; return rects.map((rect) => ({ ...rect, x: rect.x + dx, y: rect.y + dy })); }
function snapMovingGuides(rects: readonly CanvasRect[], geometry: CanvasGeometry, selected: readonly string[], options: { enabled: boolean; gridSize: number; threshold: number }): SnapGuide[] { const primary = rects[0]; if (!primary) return []; return snapRectWithOptions(primary, geometry.spatialIndex.query(primary, options.threshold + options.gridSize).filter((peer) => !selected.includes(peer.id ?? '')), options).guides; }
export function createGeometryPatches(rects: readonly CanvasRect[], elements: Record<string, DesignerDocumentNode>, kind: 'move' | 'resize' | 'pan' | 'select', snapshots: readonly { id: string; rect: CanvasRect }[], parentOrigins: Readonly<Record<string, CanvasPoint>> = {}, layoutModes: Readonly<Record<string, ReturnType<typeof resolveLayoutMode>>> = {}, parentRects: Readonly<Record<string, CanvasRect>> = {}): Record<string, Partial<DesignerDocumentNode>> {
  const initial = new Map(snapshots.map((snapshot) => [snapshot.id, snapshot.rect]));
  const entries: Array<[string, Partial<DesignerDocumentNode>]> = rects.filter((rect) => {
    const before = rect.id ? initial.get(rect.id) : undefined;
    if (!rect.id || !elements[rect.id] || !before) return false;
    return kind === 'resize' ? before.x !== rect.x || before.y !== rect.y || before.width !== rect.width || before.height !== rect.height : before.x !== rect.x || before.y !== rect.y;
  }).map((rect): [string, Partial<DesignerDocumentNode>] => {
    const node = elements[rect.id!];
    if (!node) return [rect.id!, {}] as [string, Partial<DesignerDocumentNode>];
    const parentMode = node.parentId ? layoutModes[node.parentId] ?? resolveLayoutMode(elements[node.parentId]?.layout ?? {}) : undefined;
    if (kind === 'move' && (parentMode === 'flex' || parentMode === 'grid')) return [rect.id!, {}] as [string, Partial<DesignerDocumentNode>];
    const parentOrigin = parentOrigins[node.id] ?? (elements[node.parentId ?? ''] ? resolveParentOrigin(node.parentId, elements) : { x: 0, y: 0 });
    const parentRect = node.parentId ? parentRects[node.parentId] : undefined;
    const localRect = { x: rect.x - parentOrigin.x, y: rect.y - parentOrigin.y, width: rect.width, height: rect.height };
    const constraints = parentMode === 'constraints' && parentRect ? updateConstraintAnchors(node, parentRect, localRect) : undefined;
    const layout = kind === 'resize'
      ? (parentMode === 'flex' || parentMode === 'grid' ? createFlexResizeLayoutPatch(node.layout, rect.width, rect.height) : { ...node.layout, ...(parentMode === 'constraints' ? {} : { position: 'absolute' }), ...(constraints ? {} : { x: localRect.x, y: localRect.y }), ...(constraints ? { constraints } : {}), width: rect.width, height: rect.height })
      : { ...node.layout, ...(parentMode === 'constraints' ? {} : { position: 'absolute' }), ...(parentMode === 'constraints' && constraints ? { constraints } : { x: localRect.x, y: localRect.y }) };
    return [rect.id!, { layout }] as [string, Partial<DesignerDocumentNode>];
  }).filter((entry: [string, Partial<DesignerDocumentNode>]) => Object.keys(entry[1].layout ?? {}).length > 0);
  return Object.fromEntries(entries);
}
function updateConstraintAnchors(node: DesignerDocumentNode, parent: CanvasRect, local: CanvasRect): Record<string, unknown> | undefined {
  const current = isRecord(node.layout.constraints) ? node.layout.constraints : {};
  const anchors = ['left', 'right', 'top', 'bottom', 'centerX', 'centerY'].filter((anchor) => anchor in current) as Array<'left' | 'right' | 'top' | 'bottom' | 'centerX' | 'centerY'>;
  if (anchors.length === 0) return undefined;
  return { ...current, ...createConstraintChange({ id: 'node', ...local }, { id: 'parent', x: 0, y: 0, width: parent.width, height: parent.height }, anchors).constraints };
}
function resolveParentOrigin(parentId: string | null, elements: Record<string, DesignerDocumentNode>): CanvasPoint {
  let x = 0;
  let y = 0;
  const visited = new Set<string>();
  let current = parentId;
  while (current && !visited.has(current)) {
    visited.add(current);
    const node = elements[current];
    if (!node) break;
    x += numberValue(node.layout.x);
    y += numberValue(node.layout.y);
    current = node.parentId;
  }
  return { x, y };
}

function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value) && typeof value === 'object' && !Array.isArray(value); }

function defaultStudioText(key: string): string {
  const labels: Record<string, string> = {
    bindSlot: 'Bind', bindingSlots: 'Component binding slots', browserSimulation: 'Browser simulation', canvas: 'Page Studio canvas', canvasRootMissing: 'Page artboard root is missing', canvasRulers: 'Canvas rulers', canvasZoom: 'Canvas zoom', collapseMinimap: 'Collapse minimap', contextBringForward: 'Bring forward', contextBringToFront: 'Bring to front', contextCopy: 'Copy', contextDelete: 'Delete', contextDuplicate: 'Duplicate', contextLock: 'Lock', contextMenu: 'Canvas component menu', contextPaste: 'Paste inside', contextSendBackward: 'Send backward', contextSendToBack: 'Send to back', contextUnlock: 'Unlock', dropToAdd: 'Release to add component', dropToMove: 'Release to move component', expandMinimap: 'Expand minimap', horizontalRuler: 'Horizontal ruler', inlineEditor: 'Component inline editor', insertAfter: 'Insert after', insertBefore: 'Insert before', insertInside: 'Insert inside container', invalidMoveTarget: 'No valid move target at this position', minimap: 'Canvas minimap', minimapDescription: 'Click to center, drag the viewport to pan, or use the wheel to zoom.', minimapNavigation: 'Minimap navigation area', moveInside: 'Move inside container', pageArtboard: 'Page artboard', resizeHandle: 'Resize', safeAreaSimulation: 'Safe area simulation', selectionMarquee: 'Selection marquee', userGuide: 'User guide', verticalRuler: 'Vertical ruler', visibleViewport: 'Visible viewport'
  };
  return labels[key] ?? key;
}
