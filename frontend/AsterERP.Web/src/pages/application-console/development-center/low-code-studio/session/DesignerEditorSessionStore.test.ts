import { describe, expect, it } from 'vitest';

import { serializeDesignerDocument } from '../document/DesignerDocumentHash';

import { DesignerEditorSessionStore } from './DesignerEditorSessionStore';

const session = { anchorNodeId: null, canvas: { device: null, gridSize: 8, gridVisible: true, guides: [], minimapVisible: true, rulersVisible: true, snapThreshold: 6, tool: 'select' as const }, documentId: 'orders', panelState: { inspector: true }, primaryNodeId: null, selectedNodeIds: [], sessionId: 'session-1', transactionId: null, viewport: { height: 720, pan: { x: 0, y: 0 }, width: 1280, zoom: 1 } };

describe('DesignerEditorSessionStore', () => {
  it('keeps only the session contract and normalizes selection, pan, and viewport', () => {
    const source = { ...session, editorState: { dirty: true }, selectedNodeIds: ['a', 'a', ''], primaryNodeId: 'missing', viewport: { height: 0, pan: { x: Number.NaN, y: Number.POSITIVE_INFINITY }, width: Number.NaN, zoom: 9 } };
    const store = new DesignerEditorSessionStore(source);
    expect(Object.keys(store.getSnapshot()).sort()).toEqual(['anchorNodeId', 'canvas', 'documentId', 'panelState', 'primaryNodeId', 'selectedNodeIds', 'sessionId', 'transactionId', 'viewport']);
    expect(store.getSnapshot().selectedNodeIds).toEqual(['a']);
    expect(store.getSnapshot().primaryNodeId).toBe('a');
    expect(store.getSnapshot().viewport).toEqual({ height: 1, pan: { x: 0, y: 0 }, width: 1280, zoom: 4 });
    expect(source.selectedNodeIds).toEqual(['a', 'a', '']);
    expect(source.viewport.pan).toEqual({ x: Number.NaN, y: Number.POSITIVE_INFINITY });
    expect(Object.isFrozen(store.getSnapshot())).toBe(true);
    expect(Object.isFrozen(store.getSnapshot().viewport.pan)).toBe(true);
  });

  it('keeps canvas overlays in the editor session and normalizes their bounds', () => {
    const store = new DesignerEditorSessionStore(session);
    store.patch({ canvas: { gridSize: 0, snapThreshold: 99, guides: [{ axis: 'x', id: ' guide ', position: 120 }, { axis: 'z' as never, id: 'invalid', position: 1 }] } });
    expect(store.getSnapshot().canvas).toMatchObject({ gridSize: 1, snapThreshold: 64, guides: [{ axis: 'x', id: 'guide', position: 120 }] });
    store.patch({ canvas: { gridVisible: false, rulersVisible: false } });
    expect(store.getSnapshot().canvas).toMatchObject({ gridVisible: false, rulersVisible: false });
  });

  it('normalizes a custom device without leaking invalid viewport values', () => {
    const store = new DesignerEditorSessionStore(session);
    store.patch({ canvas: { device: { browserBar: { bottom: -1, top: 12 }, breakpointId: ' mobile ', height: 0, id: ' custom ', orientation: 'portrait', pixelRatio: 20, safeArea: { bottom: 500, left: 1, right: 2, top: 3 }, width: 390 } } });
    expect(store.getSnapshot().canvas.device).toMatchObject({ id: 'custom', breakpointId: 'mobile', width: 390, height: 1, pixelRatio: 8, browserBar: { bottom: 0, top: 12 }, safeArea: { bottom: 400, left: 1, right: 2, top: 3 } });
  });

  it('publishes only real changes and merges panel/viewport patches', () => {
    const store = new DesignerEditorSessionStore(session);
    const notifications: string[] = [];
    store.subscribe((next) => notifications.push(next.transactionId ?? 'none'));
    store.patch({ transactionId: 'tx-1', panelState: { canvas: true }, viewport: { zoom: 1.25 } });
    store.patch({ transactionId: 'tx-1' });
    expect(notifications).toEqual(['tx-1']);
    expect(store.getSnapshot().panelState).toEqual({ inspector: true, canvas: true });
    expect(store.getSnapshot().viewport.zoom).toBe(1.25);
  });

  it('copies patch inputs and replaces sessions immutably', () => {
    const store = new DesignerEditorSessionStore(session);
    const selectedNodeIds = ['node-1'];
    const viewportPatch = { pan: { x: 20, y: -10 } };
    const snapshot = store.patch({ selectedNodeIds, viewport: viewportPatch });
    selectedNodeIds.push('node-2');
    viewportPatch.pan.x = 99;
    expect(snapshot.selectedNodeIds).toEqual(['node-1']);
    expect(snapshot.viewport.pan).toEqual({ x: 20, y: -10 });
    expect(store.select((value) => value.primaryNodeId)).toBe('node-1');

    const replacement = { ...session, selectedNodeIds: ['node-3'], primaryNodeId: 'node-3', anchorNodeId: 'node-3', sessionId: 'session-3' };
    const replaced = store.replace(replacement);
    replacement.selectedNodeIds.push('node-4');
    expect(replaced.sessionId).toBe('session-3');
    expect(replaced.selectedNodeIds).toEqual(['node-3']);
    expect(store.select((value) => value.primaryNodeId)).toBe('node-3');
    expect(() => { (store.getSnapshot().viewport as { zoom: number }).zoom = 2; }).toThrow();
  });

  it('keeps editor state outside the document persistence boundary', () => {
    const document = { actions: [], apiBindings: [], dataSources: [], documentId: 'orders', elements: {}, metadata: {}, modals: [], pageParameters: [], pages: [], permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: [] };
    const editorSession = new DesignerEditorSessionStore(session).getSnapshot();
    expect(Object.keys(editorSession)).not.toContain('editorState');
    expect(() => serializeDesignerDocument({ ...document, editorState: editorSession } as typeof document & { editorState: unknown })).toThrow(/editorState belongs to DesignerEditorSession/);
  });
});
