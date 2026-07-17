import { describe, expect, it } from 'vitest';

import type { DesignerDocumentNode } from '../pages/application-console/development-center/low-code-studio/document/DesignerDocument';

import { LayoutResolver } from './LayoutResolver';

const node: DesignerDocumentNode = {
  children: [],
  events: [],
  id: 'root',
  layout: { height: '100vh', width: '100vw' },
  parentId: null,
  props: {},
  responsiveOverrides: { tablet: { layout: { width: 640 }, style: { minHeight: 240 } } },
  type: 'layout.page'
};

describe('LayoutResolver', () => {
  it('applies the selected breakpoint override and viewport dimensions', () => {
    expect(new LayoutResolver().resolveSections(node, { breakpoint: 'tablet', viewport: { height: 720, width: 1280 } })).toEqual({
      layout: { height: 720, width: 640 },
      props: {},
      style: { minHeight: 240 }
    });
  });

  it('does not mutate the node or resolve viewport tokens without a viewport context', () => {
    const resolved = new LayoutResolver().resolve(node, { breakpoint: 'tablet' });

    expect(resolved).toEqual({ height: '100vh', width: 640 });
    expect(node.layout).toEqual({ height: '100vh', width: '100vw' });
  });
});
