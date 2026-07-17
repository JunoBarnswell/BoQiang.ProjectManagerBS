import { describe, expect, it } from 'vitest';

import { latestComponentRegistry } from '../components/latestComponentManifestCatalog';
import type { DesignerDocument } from '../document/DesignerDocument';

import { diagnosePageStudioDocument } from './PageStudioDiagnostics';

describe('Page Studio diagnostics', () => {
  it('accepts a registered resource with a canonical conversion pipeline', () => {
    const document = createDocument({
      variables: [{ id: 'customerName', name: 'Customer name', source: 'variables', valueType: 'string' }],
      binding: { kind: 'expression', expectedType: 'string', graph: { root: { kind: 'resourceRef', resourceId: 'variables:customerName', valueType: 'string' } } }
    });

    expect(diagnosePageStudioDocument(document, latestComponentRegistry)).toEqual([]);
  });

  it('reports missing resources, incompatible types, and unregistered converters with node and property paths', () => {
    const document = createDocument({
      binding: {
        conversionPipeline: [{ from: 'number', name: 'missingConverter', to: 'string' }],
        expectedType: 'string',
        graph: { root: { kind: 'resourceRef', resourceId: 'variables:missing', valueType: 'number' } },
        kind: 'expression'
      }
    });

    const diagnostics = diagnosePageStudioDocument(document, latestComponentRegistry);
    expect(diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'missing-resource', elementId: 'root', path: 'elements.root.props.text' }),
      expect.objectContaining({ code: 'missing-converter', path: 'elements.root.props.text.conversionPipeline.0.name' }),
      expect.objectContaining({ code: 'incompatible-binding', path: 'elements.root.props.text' })
    ]));
  });

  it('reports dependency cycles between variable bindings', () => {
    const document = createDocument({
      variables: [
        { id: 'first', expression: { kind: 'expression', expectedType: 'string', graph: { root: { kind: 'resourceRef', resourceId: 'variables:second', valueType: 'string' } } }, name: 'First', source: 'variables', valueType: 'string' },
        { id: 'second', expression: { kind: 'expression', expectedType: 'string', graph: { root: { kind: 'resourceRef', resourceId: 'variables:first', valueType: 'string' } } }, name: 'Second', source: 'variables', valueType: 'string' }
      ]
    });

    expect(diagnosePageStudioDocument(document, latestComponentRegistry)).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'cyclic-binding', path: 'variables.0' })
    ]));
  });

  it('uses the canonical action manifest catalog for designer diagnostics', () => {
    const document = createDocument({
      actions: [{ id: 'load', steps: [{ id: 'known', type: 'setVariable', config: { target: 'ready' } }, { id: 'unknown', type: 'missing.action', config: {} }] }]
    });

    expect(diagnosePageStudioDocument(document, latestComponentRegistry)).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'unknown-action', path: 'actions.0.steps.1.type' })
    ]));
    expect(diagnosePageStudioDocument(createDocument({ actions: [{ id: 'load', steps: [{ id: 'known', type: 'setVariable', config: {} }] }] }), latestComponentRegistry))
      .not.toEqual(expect.arrayContaining([expect.objectContaining({ code: 'unknown-action' }), expect.objectContaining({ code: 'missing-action-manifest' })]));
  });

  it('blocks preview before runtime compilation when an action has no steps', () => {
    const document = createDocument({
      actions: [{ id: 'load', type: 'setVariable' }]
    });

    expect(diagnosePageStudioDocument(document, latestComponentRegistry)).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'action-steps-required', messageKey: 'lowCode.pageStudio.diagnostic.action-steps-required', path: 'actions.0' })
    ]));
  });

  it('rejects invalid interaction state without regressing valid action and binding diagnostics', () => {
    const document = createDocument({
      actions: [{ id: 'load', steps: [{ id: 'known', type: 'setVariable', config: {} }] }],
      elements: {
        root: {
          bindings: {}, children: [], events: [], id: 'root', layout: {}, parentId: null, props: { disabled: 'yes', text: { expectedType: 'string', graph: { root: { kind: 'literal', value: 'hello', valueType: 'string' } }, kind: 'expression' } }, style: {}, type: 'text'
        }
      }
    });

    const diagnostics = diagnosePageStudioDocument(document, latestComponentRegistry);
    expect(diagnostics).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'interaction-state-invalid', path: 'elements.root.props.disabled', severity: 'error' })
    ]));
    expect(diagnostics).not.toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'unknown-action' }),
      expect.objectContaining({ code: 'missing-resource' })
    ]));
  });

  it('rejects invalid layout protocol values with stable error codes and document paths', () => {
    const document = createDocument({
      elements: {
        root: {
          bindings: {},
          children: [],
          events: [],
          id: 'root',
          layout: {
            container: { mode: 'grid', grid: { columns: [], rows: [], columnGap: 0, rowGap: 0, autoFlow: 'row' } },
            placement: { kind: 'grid-item', gridItem: { rowStart: 'auto', rowSpan: 0, columnStart: 'auto', columnSpan: 1 } },
            size: { width: -1, height: 20, aspectRatio: 0 }
          },
          parentId: null,
          props: { text: 'hello' },
          style: {},
          type: 'text'
        }
      }
    });

    expect(diagnosePageStudioDocument(document, latestComponentRegistry)).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'LAYOUT_CONTAINER_PAYLOAD_INVALID', path: 'elements.root.layout.container.grid', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_DIMENSION_INVALID', path: 'elements.root.layout.size.width', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_ASPECT_RATIO_INVALID', path: 'elements.root.layout.size.aspectRatio', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_GRID_SPAN_INVALID', path: 'elements.root.layout.placement.gridItem', severity: 'error' })
    ]));
  });

  it('rejects parent-child mismatches and legacy layout fields as errors', () => {
    const document = createDocument({
      elements: {
        root: {
          bindings: {}, children: [], events: [], id: 'root', layout: { position: 'absolute' }, parentId: null, props: { text: 'hello' }, style: {}, type: 'text'
        },
        child: {
          bindings: {}, children: [], events: [], id: 'child', layout: {}, parentId: 'root', props: { text: 'child' }, style: {}, type: 'text'
        }
      }
    });

    expect(diagnosePageStudioDocument(document, latestComponentRegistry)).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'LAYOUT_LEGACY_FIELD', path: 'elements.root.layout.position', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_PARENT_CHILD_MISMATCH', path: 'elements.child.parentId', severity: 'error' })
    ]));
  });
});

function createDocument(overrides: Partial<DesignerDocument> & { binding?: Record<string, unknown> } = {}): DesignerDocument {
  const { binding, ...documentOverrides } = overrides;
  return {
    actions: [],
    apiBindings: [],
    dataSources: [],
    documentId: 'diagnostics',
    documentHash: 'hash',
    elements: {
      root: {
        bindings: {},
        children: [],
        events: [],
        id: 'root',
        layout: {},
        parentId: null,
        props: binding ? { text: binding } : { text: 'hello' },
        style: {},
        type: 'text'
      }
    },
    metadata: {},
    modals: [],
    pageParameters: [],
    pages: [{ id: 'page', name: 'Diagnostics', rootElementId: 'root' }],
    pageType: 'standard',
    permissions: {},
    runtimeContext: {},
    revision: 1,
    styleTokens: {},
    variables: [],
    workflowBindings: [],
    ...documentOverrides
  };
}
