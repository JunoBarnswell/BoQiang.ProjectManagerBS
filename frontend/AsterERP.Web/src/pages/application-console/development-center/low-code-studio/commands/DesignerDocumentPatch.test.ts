import { describe, expect, it } from 'vitest';

import { createDefaultDesignerDocument } from '../document/DesignerDocumentCodec';

import {
  applyDesignerDocumentPatch,
  createDesignerDocumentPatch,
  invertDesignerDocumentPatch
} from './DesignerDocumentPatch';

describe('DesignerDocumentPatch', () => {
  it('records only changed nodes and content fields', () => {
    const before = createDefaultDesignerDocument({ pageCode: 'orders', pageName: 'Orders' });
    const rootId = before.pages[0].rootElementId;
    const after = {
      ...before,
      elements: {
        ...before.elements,
        [rootId]: { ...before.elements[rootId], props: { ...before.elements[rootId].props, title: 'Updated' } }
      },
      metadata: { ...before.metadata, source: 'latest', changed: true }
    };

    const patch = createDesignerDocumentPatch(before, after);

    expect(patch.nodeChanges.map((change) => change.id)).toEqual([rootId]);
    expect(patch.fieldChanges.map((change) => change.key)).toEqual(['metadata']);
    expect(patch).not.toHaveProperty('document');
    expect(JSON.stringify(patch)).not.toContain('elements');
  });

  it('applies and inverts a patch without replacing unrelated document state', () => {
    const before = createDefaultDesignerDocument({ pageCode: 'orders', pageName: 'Orders' });
    const rootId = before.pages[0].rootElementId;
    const after = {
      ...before,
      elements: {
        ...before.elements,
        [rootId]: { ...before.elements[rootId], props: { ...before.elements[rootId].props, title: 'Updated' } }
      }
    };
    const patch = createDesignerDocumentPatch(before, after);
    const restored = applyDesignerDocumentPatch(after, invertDesignerDocumentPatch(patch));

    expect(restored.changed).toBe(true);
    expect(restored.diagnostics).toEqual([]);
    expect(restored.document?.elements[rootId].props).toEqual(before.elements[rootId].props);
    expect(restored.document?.runtimeContext).toBe(after.runtimeContext);
  });

  it('records and inverts formal page presentation metadata', () => {
    const before = createDefaultDesignerDocument({ pageCode: 'orders', pageName: 'Orders' });
    const after = { ...before, pageType: 'detail' };

    const patch = createDesignerDocumentPatch(before, after);

    expect(patch.fieldChanges).toEqual([expect.objectContaining({ key: 'pageType', before: before.pageType, after: 'detail' })]);
    const restored = applyDesignerDocumentPatch(after, invertDesignerDocumentPatch(patch));
    expect(restored.diagnostics).toEqual([]);
    expect(restored.document?.pageType).toBe(before.pageType);
  });

  it('rejects a conflicting inverse atomically', () => {
    const before = createDefaultDesignerDocument({ pageCode: 'orders', pageName: 'Orders' });
    const rootId = before.pages[0].rootElementId;
    const after = {
      ...before,
      elements: {
        ...before.elements,
        [rootId]: { ...before.elements[rootId], props: { ...before.elements[rootId].props, title: 'Updated' } }
      }
    };
    const conflicting = {
      ...after,
      elements: { ...after.elements, [rootId]: { ...after.elements[rootId], type: 'text.heading' } }
    };
    const result = applyDesignerDocumentPatch(conflicting, invertDesignerDocumentPatch(createDesignerDocumentPatch(before, after)));

    expect(result.changed).toBe(false);
    expect(result.document).toBeUndefined();
    expect(result.diagnostics).toContain(`Node conflict: ${rootId}`);
  });
});
