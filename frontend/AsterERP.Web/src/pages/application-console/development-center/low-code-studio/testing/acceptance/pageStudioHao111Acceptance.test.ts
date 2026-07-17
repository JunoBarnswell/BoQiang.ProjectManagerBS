import { describe, expect, it } from 'vitest';

import { BindingResolver } from '../../../../../../runtime-kernel/BindingResolver';
import { scoreTypeCompatibility } from '../../binding/typeCompatibility';
import { clampZoom, panBy, zoomAtScreenPoint } from '../../canvas/coordinateSystem';
import { beginPointerTransaction, updatePointerTransaction } from '../../canvas/pointerTransaction';
import { createSelection, selectByMarquee, toggleSelection } from '../../canvas/selectionModel';
import { createDuplicateSubtreeCommand, createInsertNodesCommand, createDeleteNodesCommand, createMoveNodesCommand, createPatchNodeCommand, createPatchResponsiveOverrideCommand, createBindValueCommand } from '../../commands/createDesignerCommands';
import { DesignerCommandBus } from '../../commands/DesignerCommandBus';
import type { DesignerDocument, DesignerDocumentNode } from '../../document/DesignerDocument';
import { canonicalizeDesignerDocument, computeDesignerDocumentHash } from '../../document/DesignerDocumentHash';
import { validateExpressionGraph } from '../../expression/expressionGraph';
import { calculateLayoutChanges } from '../../layout/layoutOperations';
import { resolveResponsiveNode } from '../../responsive/responsiveModel';
import { resolveShortcut } from '../../shortcuts/shortcutModel';

describe('HAO-111 Page Studio acceptance', () => {
  it('inserts, moves, deletes, duplicates, edits, binds, and restores a document through CommandBus', () => {
    const initial = createDocument();
    const bus = new DesignerCommandBus(initial);
    const inserted = node('card', 'root', ['label']);

    expect(bus.execute(createInsertNodesCommand([inserted])).changed).toBe(true);
    expect(bus.execute(createMoveNodesCommand(['label'], 'card')).changed).toBe(false);
    expect(bus.document.elements.label.parentId).toBe('card');
    expect(bus.execute(createPatchNodeCommand('label', { props: { text: 'Edited' } }, 'inspector:label')).changed).toBe(true);
    expect(bus.execute(createBindValueCommand('label', 'text', { conversionPipeline: [], displayName: 'Customer name', expectedType: 'string', resourceId: 'page.customerName', resourceType: 'page', valueType: 'string', fallback: { kind: 'string', value: '' } })).changed).toBe(true);
    bus.endTransaction();
    expect(bus.document.elements.label.props.text).toEqual({ conversionPipeline: [], displayName: 'Customer name', expectedType: 'string', resourceId: 'page.customerName', resourceType: 'page', valueType: 'string', fallback: { kind: 'string', value: '' } });

    expect(bus.execute(createDuplicateSubtreeCommand('card', 'card-copy')).changed).toBe(true);
    expect(bus.document.elements.card_copy).toBeUndefined();
    expect(bus.document.elements['card-copy_1']).toBeDefined();
    expect(bus.execute(createDeleteNodesCommand(['card-copy'])).changed).toBe(true);
    expect(bus.document.elements['card-copy_1']).toBeUndefined();

    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements['card-copy_1']).toBeDefined();
    expect(bus.redo()?.changed).toBe(true);
    expect(bus.document.elements['card-copy_1']).toBeUndefined();
  });

  it('groups inspector edits, supports multi-selection, and undoes the complete transaction', () => {
    const bus = new DesignerCommandBus(createDocument());
    const before = bus.document;
    bus.execute(createPatchNodeCommand('label', { props: { text: 'A' } }, 'inspector:label'));
    bus.execute(createPatchNodeCommand('label', { style: { color: 'red' } }, 'inspector:label'));
    bus.endTransaction();
    expect(bus.document.elements.label.props.text).toBe('A');
    expect(bus.document.elements.label.style).toEqual({ color: 'red' });
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.label).toEqual(before.elements.label);

    const selection = toggleSelection(toggleSelection(createSelection(['label']), 'other', true), 'label', true);
    expect(selection.selectedNodeIds).toEqual(['other']);
    expect(selectByMarquee([{ id: 'label', x: 10, y: 10, width: 80, height: 20 }], { x: 0, y: 0, width: 100, height: 100 })).toEqual({ selectedNodeIds: ['label'], primaryNodeId: 'label', anchorNodeId: 'label' });
  });

  it('covers canvas zoom, pan, move, resize, layout, and responsive breakpoint behavior', () => {
    const viewport = zoomAtScreenPoint({ zoom: 1, pan: { x: 0, y: 0 } }, 2, { x: 100, y: 100 }, { x: 0, y: 0 });
    expect(viewport).toEqual({ zoom: 2, pan: { x: -100, y: -100 } });
    expect(panBy(viewport, { x: 12, y: -4 }).pan).toEqual({ x: -88, y: -104 });
    expect(clampZoom(99)).toBe(4);

    const move = updatePointerTransaction(beginPointerTransaction('move', 1, { x: 10, y: 10 }, [{ id: 'label', rect: { id: 'label', x: 20, y: 30, width: 80, height: 20 } }]), { x: 25, y: 5 });
    expect(move.rects[0]).toMatchObject({ x: 35, y: 25, width: 80, height: 20 });
    const resize = updatePointerTransaction(beginPointerTransaction('resize', 2, { x: 0, y: 0 }, [{ id: 'label', rect: { x: 20, y: 30, width: 80, height: 20 } }], 'southeast'), { x: 15, y: 10 });
    expect(resize.rects[0]).toMatchObject({ width: 95, height: 30 });

    expect(calculateLayoutChanges([
      { id: 'a', x: 10, y: 20, width: 100, height: 40 },
      { id: 'b', x: 300, y: 80, width: 50, height: 20 }
    ], 'align-top').get('b')).toEqual({ y: 20 });
    const responsiveNode = { base: { layout: { width: 320, display: 'block' } }, responsiveOverrides: { tablet: { layout: { width: 640 } } } };
    expect(resolveResponsiveNode(responsiveNode, { id: 'tablet', minWidth: 480 }, [{ id: 'mobile', minWidth: 0 }, { id: 'tablet', minWidth: 480 }])).toEqual({ layout: { width: 640, display: 'block' } });
  });

  it('accepts valid bindings and expressions, and blocks incompatible or invalid ones', () => {
    const resolver = new BindingResolver();
    expect(resolver.resolve({ version: 'latest', kind: 'resourceRef', dataType: 'string', resourceId: 'page:customer.name' }, { resources: { 'page:customer.name': 'Ada' }, scopes: {}, variables: {} })).toEqual({ resourceId: 'page:customer.name', source: 'resource', value: 'Ada' });
    expect(resolver.resolve({ resourceId: 'customer-name', fallback: { value: 'fallback' } }, { resources: {}, scopes: {}, variables: {} })).toEqual({ source: 'resource', resourceId: 'customer-name', value: 'fallback' });
    expect(scoreTypeCompatibility('number', 'string').compatibility).toBe('safe');
    expect(scoreTypeCompatibility('object', 'number').compatibility).toBe('incompatible');
    expect(validateExpressionGraph({ root: { kind: 'literal', value: 1, valueType: 'number' } }, 'string')).not.toEqual([]);
    expect(validateExpressionGraph({ root: null }, 'string')).not.toEqual([]);
  });

  it('resolves shortcut actions and preserves the saved document identity after reload', () => {
    expect(resolveShortcut({ key: 'c', ctrlKey: true })).toBe('copy');
    expect(resolveShortcut({ key: 'z', ctrlKey: true })).toBe('undo');
    expect(resolveShortcut({ key: 'z', ctrlKey: true, shiftKey: true })).toBe('redo');
    expect(resolveShortcut({ key: 'ArrowRight' })).toBe('nudge-right');
    expect(resolveShortcut({ key: 'Delete' })).toBe('delete');

    const bus = new DesignerCommandBus(createDocument());
    bus.execute(createPatchResponsiveOverrideCommand('label', 'tablet', { layout: { minWidth: 480, width: 640 } }));
    const saved = JSON.parse(canonicalizeDesignerDocument(bus.document)) as DesignerDocument;
    const reloaded = new DesignerCommandBus(saved).document;
    expect(computeDesignerDocumentHash(reloaded)).toBe(bus.document.documentHash);
    expect(reloaded.elements.label.responsiveOverrides?.tablet).toEqual({ layout: { minWidth: 480, width: 640 } });
  });
});

function createDocument(): DesignerDocument {
  const root = node('root', null, ['label', 'other']);
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'hao-111', elements: {
      root,
      label: node('label', 'root', []),
      other: node('other', 'root', [])
    }, metadata: {}, modals: [], pageParameters: [], pages: [{ id: 'page', name: 'Page Studio acceptance', rootElementId: 'root' }], pageType: 'standard', permissions: { view: true, edit: true }, runtimeContext: {}, revision: 1, styleTokens: {}, variables: [], workflowBindings: []
  };
}

function node(id: string, parentId: string | null, children: string[]): DesignerDocumentNode {
  return { bindings: {}, children, events: [], id, layout: { constraints: {}, height: 20, width: 80 }, parentId, props: { text: id }, style: {}, type: 'text' };
}
