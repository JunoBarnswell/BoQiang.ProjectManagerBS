import { describe, expect, it } from 'vitest';

import { defaultContainerLayout, defaultPlacement } from '../layout/LayoutProtocol';

import { canonicalizeDesignerDocument, createDefaultDesignerDocument, parseDesignerDocument, serializeDesignerDocument } from './DesignerDocumentCodec';
import { DesignerDocumentParseError } from './DesignerDocumentParseError';

const seed = { pageCode: 'Orders Page', pageName: 'Orders' };

describe('DesignerDocumentCodec', () => {
  it('creates and round-trips a latest document without editor state', () => {
    const document = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(document.elements)[0];
    expect(document.elements[rootId].layout).toEqual({
      container: { mode: 'free' },
      placement: { kind: 'absolute', absolute: { x: 0, y: 0 } },
      size: { height: 720, minHeight: 720, width: 1280 }
    });
    const reloaded = parseDesignerDocument(serializeDesignerDocument(document), seed);
    expect(reloaded.documentId).toBe('orders_page');
    expect(reloaded.documentHash).toBe(document.documentHash);
    expect(canonicalizeDesignerDocument(reloaded)).toBe(canonicalizeDesignerDocument(document));
  });

  it('normalizes persisted node values while preserving latest document fields', () => {
    const document = parseDesignerDocument(JSON.stringify({
      documentId: 'orders', revision: 4, elements: {
        root: { id: 'root', type: 'layout.page', children: ['label'], parentId: null, layout: {}, props: {}, events: [] },
        label: { id: 'label', type: 'text.paragraph', children: [], parentId: 'root', layout: {}, props: { text: 'Hello' }, events: [] }
      }, pages: [{ id: 'orders', name: 'Orders', rootElementId: 'root' }], actions: [], apiBindings: [], dataSources: [], modals: [], pageParameters: [], permissions: {}, runtimeContext: {}, styleTokens: {}, variables: [], workflowBindings: []
    }), seed);
    expect(document.elements.label.parentId).toBe('root');
    expect(document.revision).toBe(4);
  });

  it('parses and canonicalizes a direct LayoutProtocol without retaining wrapper or legacy placement payloads', () => {
    const source = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(source.elements)[0];
    const nodeId = 'card';
    source.elements[rootId].children = [nodeId];
    source.elements[nodeId] = {
      bindings: {}, children: [], events: [], id: nodeId, layout: {
      protocol: {
        container: { mode: 'flex', flex: { direction: 'column', wrap: 'wrap', gap: 8, alignItems: 'center', justifyContent: 'end' } },
        placement: { kind: 'flex-item', flexItem: { order: 3, grow: 1, shrink: 0, basis: 120 } },
        size: { width: 120, height: 40 }
      }
      }, parentId: rootId, props: {}, type: 'layout.container'
    };

    const document = parseDesignerDocument(JSON.stringify(source), seed);
    expect(document.elements[nodeId].layout).toEqual({
      container: { mode: 'flex', flex: { direction: 'column', wrap: 'wrap', gap: 8, alignItems: 'center', justifyContent: 'end' } },
      placement: { kind: 'flex-item', flexItem: { order: 3, grow: 1, shrink: 0, basis: 120 } },
      size: { width: 120, height: 40 }
    });
    expect(JSON.parse(serializeDesignerDocument(document)).elements[nodeId].layout).toEqual({ protocol: document.elements[nodeId].layout });
  });

  it('rejects an incomplete or invalid LayoutProtocol at the document boundary', () => {
    const source = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(source.elements)[0];
    source.elements[rootId].layout = {
      container: { mode: 'grid', grid: { columns: [], rows: [], columnGap: 0, rowGap: 0, autoFlow: 'row' } },
      placement: { kind: 'grid-item', gridItem: { rowStart: 'auto', rowSpan: 0, columnStart: 'auto', columnSpan: 1 } },
      size: { width: -1, height: 40 }
    };

    expect(() => parseDesignerDocument(JSON.stringify(source), seed)).toThrow(DesignerDocumentParseError);
    expect(() => serializeDesignerDocument(source)).toThrow(DesignerDocumentParseError);
  });

  it('migrates legacy page roots to canonical free artboards and serializes them canonically', () => {
    const base = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(base.elements)[0];
    const legacySource = {
      ...base,
      elements: { ...base.elements, [rootId]: { ...base.elements[rootId], layout: { display: 'flex', width: '100%' } } }
    };
    const legacy = parseDesignerDocument(JSON.stringify(legacySource), seed);
    expect(legacy.elements[rootId].layout).toEqual({
      container: { mode: 'free' },
      placement: { kind: 'absolute', absolute: { x: 0, y: 0 } },
      size: { height: 720, width: '100%' }
    });
    expect(JSON.parse(serializeDesignerDocument(legacySource)).elements[rootId].layout).toEqual({ protocol: legacy.elements[rootId].layout });
  });

  it('preserves explicit legacy flex, grid, and constraints root modes and available fields', () => {
    const base = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(base.elements)[0];
    const configured = parseDesignerDocument(JSON.stringify({
      ...base,
      elements: { ...base.elements, [rootId]: { ...base.elements[rootId], layout: { display: 'flex', flexDirection: 'column', gap: 12, layoutMode: 'flex', width: 960, height: 540 } } }
    }), seed);
    expect(configured.elements[rootId].layout).toEqual({
      container: { mode: 'flex', flex: { alignItems: 'stretch', direction: 'column', gap: 12, justifyContent: 'start', wrap: 'nowrap' } },
      placement: { kind: 'flex-item', flexItem: { basis: 'auto', grow: 0, order: 0, shrink: 1 } },
      size: { height: 540, width: 960 }
    });

    const grid = parseDesignerDocument(JSON.stringify({
      ...base,
      elements: { ...base.elements, [rootId]: { ...base.elements[rootId], layout: { columns: 2, display: 'grid', gap: 8, layoutMode: 'grid', rows: 3, width: 800 } } }
    }), seed);
    expect(grid.elements[rootId].layout).toMatchObject({
      container: { mode: 'grid', grid: { columnGap: 8, columns: ['1fr', '1fr'], rowGap: 8, rows: ['1fr', '1fr', '1fr'] } },
      size: { height: 720, width: 800 }
    });

    const constraints = parseDesignerDocument(JSON.stringify({
      ...base,
      elements: { ...base.elements, [rootId]: { ...base.elements[rootId], layout: { display: 'block', layoutMode: 'constraints', minHeight: 600, width: 1024 } } }
    }), seed);
    expect(constraints.elements[rootId].layout).toEqual({
      container: { mode: 'constraints', constraints: { coordinateSpace: 'parent-padding-box' } },
      placement: { kind: 'constrained', constrained: { left: 0, top: 0 } },
      size: { height: 600, minHeight: 600, width: 1024 }
    });
  });

  it('does not rewrite an already canonical page root during parse or serialization', () => {
    const base = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(base.elements)[0];
    const canonicalRoot = {
      container: defaultContainerLayout('grid'),
      placement: defaultPlacement('grid'),
      size: { height: 720, width: 960 }
    };
    const source = { ...base, elements: { ...base.elements, [rootId]: { ...base.elements[rootId], layout: canonicalRoot } } };
    const parsed = parseDesignerDocument(JSON.stringify(source), seed);
    expect(parsed.elements[rootId].layout).toEqual(canonicalRoot);
    expect(JSON.parse(serializeDesignerDocument(parsed)).elements[rootId].layout).toEqual({ protocol: canonicalRoot });
  });

  it('canonicalizes persisted ResourceRefs on API read and preserves identity across display-name changes', () => {
    const source = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(source.elements)[0];
    source.elements[rootId].props.text = { displayName: 'Customer name', resourceId: 'variables::customer.name', valueType: 'string' };
    const reloaded = parseDesignerDocument(JSON.stringify(source), seed);
    const reloadedRoot = Object.values(reloaded.elements)[0];
    if (!reloadedRoot) throw new Error('Expected a root element after reload');
    expect(reloadedRoot.props.text).toMatchObject({ resourceId: 'variables:customer.name', resourceType: 'variables', expectedType: 'string', valueType: 'string' });

    const renamed = { ...reloaded, variables: [{ id: 'customer.name', name: '客户名称（已重命名）', valueType: 'string' }] };
    const roundTrip = parseDesignerDocument(serializeDesignerDocument(renamed), seed);
    const roundTripRoot = Object.values(roundTrip.elements)[0];
    if (!roundTripRoot) throw new Error('Expected a root element after round trip');
    expect((roundTripRoot.props.text as Record<string, unknown>).resourceId).toBe('variables:customer.name');
  });

  it('rejects legacy property-binding locations and source/path values at the latest boundary', () => {
    const document = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(document.elements)[0];
    expect(() => parseDesignerDocument(JSON.stringify({
      ...document,
      elements: { ...document.elements, [rootId]: { ...document.elements[rootId], bindings: { props: { text: { source: 'variables', path: 'title' } } } } }
    }), seed)).toThrow(/legacy property-binding location/);
    expect(() => parseDesignerDocument(JSON.stringify({
      ...document,
      elements: { ...document.elements, [rootId]: { ...document.elements[rootId], props: { text: { source: 'variables', path: 'title' } } } }
    }), seed)).toThrow(/legacy source\/path binding fields/);
  });

  it('normalizes legacy breakpoint layout fields once into layout/props/style sections', () => {
    const source = JSON.stringify({
      ...createDefaultDesignerDocument(seed),
      elements: { ...createDefaultDesignerDocument(seed).elements, [Object.keys(createDefaultDesignerDocument(seed).elements)[0]]: { ...createDefaultDesignerDocument(seed).elements[Object.keys(createDefaultDesignerDocument(seed).elements)[0]], responsiveOverrides: { tablet: { width: 640, x: 12 } } } }
    });
    const document = parseDesignerDocument(source, seed);
    const rootId = Object.keys(document.elements)[0];
    expect(document.elements[rootId].responsiveOverrides).toEqual({ tablet: { layout: { width: 640, x: 12 } } });
    expect(JSON.parse(serializeDesignerDocument(document)).elements[rootId].responsiveOverrides).toEqual({ tablet: { layout: { width: 640, x: 12 } } });
  });

  it('rejects mixed or malformed responsive override sections', () => {
    const base = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(base.elements)[0];
    for (const responsiveOverrides of [
      { tablet: { layout: { width: 640 }, width: 320 } },
      { tablet: { props: 'invalid' } },
      { tablet: ['invalid'] }
    ]) {
      const document = { ...base, elements: { ...base.elements, [rootId]: { ...base.elements[rootId], responsiveOverrides } } };
      expect(() => parseDesignerDocument(JSON.stringify(document), seed)).toThrow(DesignerDocumentParseError);
    }
  });

  it('rejects session state, graph mismatch, forbidden keys, and numeric schema versions', () => {
    for (const value of [
      { ...createDefaultDesignerDocument(seed), selectedNodeIds: ['root'] },
      { ...createDefaultDesignerDocument(seed), editorSession: { sessionId: 'session-1' } },
      { ...createDefaultDesignerDocument(seed), schemaVersion: 3 },
      { ...createDefaultDesignerDocument(seed), elements: { root: { children: ['missing'], id: 'root', layout: {}, parentId: null, props: {}, type: 'layout.page', events: [] } } },
      { ...createDefaultDesignerDocument(seed), props: { constructor: true } }
    ]) {
      expect(() => parseDesignerDocument(JSON.stringify(value), seed)).toThrow(DesignerDocumentParseError);
    }
  });

  it('rejects duplicate JSON keys before normalization can overwrite them', () => {
    const source = '{"documentId":"orders","documentId":"overwritten"}';
    expect(() => parseDesignerDocument(source, seed)).toThrow(/duplicate JSON key at \$\."documentId"/);
  });

  it('rejects legacy node dataBinding instead of silently reintroducing the old binding semantic', () => {
    const document = createDefaultDesignerDocument(seed);
    const rootId = Object.keys(document.elements)[0];
    const legacy = { ...document, elements: { ...document.elements, [rootId]: { ...document.elements[rootId], dataBinding: { source: 'page', path: 'orders' } } } };
    expect(() => parseDesignerDocument(JSON.stringify(legacy), seed)).toThrow(/dataBinding is a legacy binding entry/);
  });

  it('rejects editor session state during serialization as well as parsing', () => {
    const document = createDefaultDesignerDocument(seed);
    const withSession = { ...document, editorSession: { sessionId: 'session-1', selectedNodeIds: ['root'] } };
    expect(() => serializeDesignerDocument(withSession)).toThrow(/editorSession belongs to DesignerEditorSession/);
  });
});
