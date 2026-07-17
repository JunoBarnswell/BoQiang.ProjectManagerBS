import { describe, expect, it } from 'vitest';

import { createBindValueCommand } from '../commands/createDesignerCommands';
import { DesignerCommandBus } from '../commands/DesignerCommandBus';
import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';

import { commitInspectorBinding, commitInspectorValue } from './inspectorMutations';
import { applyPropertyTransaction, createBatchPropertyTransaction, isMixedPropertyValue } from './propertyTransactions';

describe('latest property transactions', () => {
  it('detects mixed values and creates one batch transaction', () => {
    const elements = [createNode('a', 'A'), createNode('b', 'B')];
    const field = { key: 'props.text', label: 'Text' };
    expect(isMixedPropertyValue(elements, field.key)).toBe(true);
    expect(createBatchPropertyTransaction(elements, field, 'C').patches).toHaveLength(2);
  });

  it('applies nested patches atomically without mutating the latest document', () => {
    const document = createDocument({ a: createNode('a', 'A') });
    const transaction = createBatchPropertyTransaction([document.elements.a], { key: 'props.text', label: 'Text' }, 'C');
    const next = applyPropertyTransaction(document, transaction);
    expect(next).not.toBe(document);
    expect(next.elements.a.props.text).toBe('C');
    expect(document.elements.a.props.text).toBe('A');
  });

  it('keeps latest document metadata and revision intact while applying a nested patch', () => {
    const document = createDocument({ a: createNode('a', 'A') });
    const next = applyPropertyTransaction(document, createBatchPropertyTransaction([document.elements.a], { key: 'style.padding', label: 'Padding' }, 12));
    expect(next.documentId).toBe(document.documentId);
    expect(next.revision).toBe(document.revision);
    expect(next.elements.a.style?.padding).toBe(12);
  });

  it('blocks incompatible bindings and undoes a valid multi-selection binding as one change', () => {
    const document = createDocument({ a: { ...createNode('a', 'A'), children: ['b'] }, b: { ...createNode('b', 'B'), parentId: 'a' } });
    const bus = new DesignerCommandBus(document);
    const field = { path: 'props.text', label: 'Text', section: 'content' as const, editor: 'text' as const, valueType: 'string' as const, bindable: true };
    const invalid = { version: 'latest' as const, kind: 'resourceRef' as const, dataType: 'array' as const, resourceId: 'variables:items', fallback: '' };
    expect(commitInspectorBinding(document, ['a', 'b'], field, invalid, bus)).toBe(document);
    expect(bus.document.elements.a.bindings).toBeUndefined();

    const valid = { version: 'latest' as const, kind: 'conversion' as const, dataType: 'string' as const, fallback: 0, pipeline: [{ from: 'number' as const, name: 'numberToString', to: 'string' as const }], input: { version: 'latest' as const, kind: 'resourceRef' as const, dataType: 'number' as const, resourceId: 'variables:count' } };
    const committed = commitInspectorBinding(document, ['a', 'b'], field, valid, bus);
    expect(committed.elements.a.props.text).toEqual(valid);
    expect(bus.document.elements.a.props.text).toEqual(valid);
    expect(bus.document.elements.b.props.text).toEqual(valid);
    expect(bus.undo()?.document.elements.a.props.text).toBe('A');
  });

  it('does not write inspector values when no command bus is available', () => {
    const document = createDocument({ a: createNode('a', 'A') });
    const field = { path: 'props.text', label: 'Text', section: 'content' as const, editor: 'text' as const, valueType: 'string' as const };

    expect(commitInspectorValue(document, ['a'], field, 'B')).toBe(document);
    expect(document.elements.a.props.text).toBe('A');
  });

  it('merges continuous edits to the same inspector field into one undo transaction', () => {
    const document = createDocument({ a: createNode('a', 'A') });
    const bus = new DesignerCommandBus(document);
    const field = { path: 'props.text', label: 'Text', section: 'content' as const, editor: 'text' as const, valueType: 'string' as const };

    commitInspectorValue(document, ['a'], field, 'B', bus);
    commitInspectorValue(bus.document, ['a'], field, 'C', bus);
    commitInspectorValue(bus.document, ['a'], field, 'D', bus);
    bus.endTransaction();

    expect(bus.document.elements.a.props.text).toBe('D');
    expect(bus.undo()?.changed).toBe(true);
    expect(bus.document.elements.a.props.text).toBe('A');
  });

  it('keeps fixed, resource, expression, unbound, re-bound values in one canonical property slot with ordered undo/redo', () => {
    const document = createDocument({ a: createNode('a', 'fixed') });
    const bus = new DesignerCommandBus(document);
    const field = { path: 'props.text', label: 'Text', section: 'content' as const, editor: 'text' as const, valueType: 'string' as const, bindable: true };
    const resource = { conversionPipeline: [], displayName: 'Title', expectedType: 'string' as const, resourceId: 'variables:title', resourceType: 'variables', valueType: 'string' as const };
    const expression = { version: 'latest' as const, kind: 'resourceRef' as const, dataType: 'string' as const, resourceId: 'variables:title' };

    bus.execute(createBindValueCommand('a', 'text', resource));
    commitInspectorBinding(bus.document, ['a'], field, expression, bus);
    commitInspectorBinding(bus.document, ['a'], field, null, bus);
    bus.endTransaction();
    bus.execute(createBindValueCommand('a', 'text', resource));

    expect(bus.document.elements.a.props.text).toMatchObject({ resourceId: 'variables:title' });
    expect(bus.undo()?.document.elements.a.props.text).toBeNull();
    expect(bus.redo()?.document.elements.a.props.text).toMatchObject({ resourceId: 'variables:title' });
  });
});

function createNode(id: string, text: string): DesignerDocumentNode {
  return { children: [], events: [], id, layout: {}, parentId: null, props: { text }, style: {}, type: 'text' };
}
function createDocument(elements: Record<string, DesignerDocumentNode>): DesignerDocument {
  return {
    actions: [], apiBindings: [], dataSources: [], documentId: 'property-transactions', elements, metadata: {}, modals: [],
    pageParameters: [], pages: [{ id: 'property-transactions', name: 'Property transactions', rootElementId: Object.keys(elements)[0] ?? '' }], pageType: 'standard', permissions: {}, revision: 4, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
  };
}
