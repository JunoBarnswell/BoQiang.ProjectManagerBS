import { describe, expect, it } from 'vitest';

import { createBatchPatchCommand, createPatchNodeCommand } from '../commands/createDesignerCommands';

import type { DesignerDocument } from './DesignerDocument';
import { DesignerDocumentStore } from './DesignerDocumentStore';

const document: DesignerDocument = {
  actions: [],
  apiBindings: [],
  dataSources: [],
  documentId: 'page-orders',
  elements: { root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' } },
  metadata: {},
  modals: [],
  pageParameters: [],
  pages: [{ id: 'orders', name: 'Orders', rootElementId: 'root' }],
  pageType: 'standard',
  permissions: {},
  revision: 1,
  runtimeContext: {},
  styleTokens: {},
  variables: [],
  workflowBindings: []
};

describe('DesignerDocumentStore', () => {
  it('returns immutable snapshots and selector results', () => {
    const store = new DesignerDocumentStore(document);
    expect(store.select((value) => value.documentId)).toBe('page-orders');
    expect(Object.isFrozen(store.getSnapshot())).toBe(true);
    expect(store.getSnapshot()).not.toBe(document);
    expect(store.getCanonicalSnapshot()).not.toContain('documentHash');
  });

  it('rejects editor session state before it enters the document store', () => {
    const withSession = { ...document, editorSession: { sessionId: 'session-1' } };
    expect(() => new DesignerDocumentStore(withSession)).toThrow(/editorSession belongs to DesignerEditorSession/);
  });

  it('rejects an invalid latest document before it enters the store', () => {
    expect(() => new DesignerDocumentStore({ ...document, pages: [] })).toThrow(/pages must be a non-empty array/);
  });

  it('does not publish failed commands', () => {
    const store = new DesignerDocumentStore(document);
    let notifications = 0;
    store.subscribe(() => { notifications += 1; });
    const result = store.execute({
      id: 'failed',
      label: 'failed',
      execute: () => ({ changed: false, diagnostics: ['invalid'], document: { ...document, revision: 2 } })
    });
    expect(result.diagnostics).toEqual(['invalid']);
    expect(store.getSnapshot().revision).toBe(1);
    expect(notifications).toBe(0);
  });

  it('commits a transaction atomically and publishes once', () => {
    const store = new DesignerDocumentStore({
      ...document,
      elements: {
        root: { children: ['child'], events: [], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page' },
        child: { children: [], events: [], id: 'child', layout: {}, parentId: 'root', props: {}, type: 'text.paragraph' }
      },
      pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }]
    });
    let notifications = 0;
    store.subscribe(() => { notifications += 1; });
    const result = store.executeTransaction([
      createPatchNodeCommand('child', { type: 'text.heading' }),
      createBatchPatchCommand({ missing: { type: 'invalid' } })
    ]);
    expect(result.changed).toBe(false);
    expect(store.getSnapshot().elements.child.type).toBe('text.paragraph');
    expect(notifications).toBe(0);
  });

  it('deep-freezes nested node state', () => {
    const store = new DesignerDocumentStore({
      ...document,
      elements: { root: { children: [], events: [], id: 'root', layout: {}, parentId: null, props: { config: { enabled: true } }, type: 'layout.page' } },
      pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }]
    });
    expect(Object.isFrozen(store.getSnapshot().elements.root.props.config)).toBe(true);
  });
});
