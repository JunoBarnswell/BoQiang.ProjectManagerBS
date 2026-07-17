import { describe, expect, it } from 'vitest';

import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerPatchCommand } from '../commands/DesignerDocumentPatch';
import type { DesignerDocument } from '../document/DesignerDocument';

import { createPasteClipboardCommand } from './clipboardCommands';
import { createDesignerClipboard } from './designerClipboard';

describe('latest designer clipboard command', () => {
  it('copies a selected subtree and pastes it once with rewired parent and child IDs', () => {
    const document = createDocument();
    const bus = new DesignerCommandBus(document);
    const trees = createDesignerClipboard(bus.document, ['child']);
    const result = bus.execute(createPasteClipboardCommand(trees, 'root', (sourceId, occupied) => {
      let index = 1;
      let next = `${sourceId}-copy-${index}`;
      while (occupied.has(next)) next = `${sourceId}-copy-${++index}`;
      return next;
    }));
    expect(result.changed).toBe(true);
    expect((result.inverse as DesignerPatchCommand).patch).toBeDefined();
    expect(bus.document.elements.root.children).toEqual(['child', 'child-copy-1']);
    expect(bus.document.elements['child-copy-1'].children).toEqual(['grandchild-copy-1']);
    expect(bus.document.elements['grandchild-copy-1'].parentId).toBe('child-copy-1');
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements['child-copy-1']).toBeUndefined();
  });

  it('carries document resources/actions, preserves responsive overrides, and reports unresolved external resources', () => {
    const document = createDocument();
    document.actions = [{ id: 'action-1', type: 'navigate', target: 'orders' }];
    document.variables = [{ id: 'customer.name', valueType: 'string' }];
    document.metadata.manifestVersions = { 'action.button': '1.2.0' };
    document.elements.child.props.text = { resourceId: 'variables:customer.name', resourceType: 'variables', valueType: 'string' };
    document.elements.child.events = [{ actionId: 'action-1' }];
    document.elements.child.responsiveOverrides = { tablet: { layout: { width: 640 }, props: { text: 'Tablet' } } };
    document.elements.child.props.missing = { resourceId: 'page:missing', resourceType: 'page', valueType: 'string' };
    const payload = createDesignerClipboard(document, ['child']);
    expect(payload.actions).toEqual(document.actions);
    expect(payload.variables).toEqual(document.variables);
    expect(payload.manifestTypes).toEqual(['action.button', 'text']);
    expect(payload.manifestVersions).toEqual({ 'action.button': '1.2.0' });
    expect(payload.trees[0].root.responsiveOverrides).toEqual(document.elements.child.responsiveOverrides);

    const result = new DesignerCommandBus(createDocument()).execute(createPasteClipboardCommand(payload, 'root', (sourceId) => `${sourceId}-copy`));
    expect(result.changed).toBe(true);
    expect(result.diagnostics).toContain('warning: External resource unresolved after paste: page:missing');
    expect(result.document.actions).toEqual([{ id: 'action-1-copy', type: 'navigate', target: 'orders' }]);
    expect(result.document.elements['child-copy'].events).toEqual([{ actionId: 'action-1-copy' }]);
  });
});

function createDocument(): DesignerDocument {
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'clipboard-test', elements: {
      root: { id: 'root', parentId: null, children: ['child'], events: [], layout: {}, props: {}, style: {}, type: 'layout.page' },
      child: { id: 'child', parentId: 'root', children: ['grandchild'], events: [{ name: 'click' }], layout: {}, props: { label: 'Keep' }, style: {}, type: 'action.button' },
      grandchild: { id: 'grandchild', parentId: 'child', children: [], events: [], layout: {}, props: {}, style: {}, type: 'text' }
    }, metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'page', name: 'Page', rootElementId: 'root' }], pageType: 'standard', permissions: {}, revision: 1, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}
