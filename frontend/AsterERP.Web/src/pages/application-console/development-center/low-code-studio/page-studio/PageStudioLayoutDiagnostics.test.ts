import { describe, expect, it } from 'vitest';

import type { DesignerDocument, DesignerDocumentNode } from '../document/DesignerDocument';
import { defaultContainerLayout, defaultPlacement, type LayoutProtocol } from '../layout/LayoutProtocol';

import { diagnosePageStudioLayoutNode, pageStudioLayoutDiagnosticCodes } from './PageStudioLayoutDiagnostics';

describe('Page Studio layout diagnostics', () => {
  it('returns validator and extended grid diagnostics with stable protocol paths', () => {
    const result = diagnosePageStudioLayoutNode(...fixture({
      container: { mode: 'grid', grid: { autoFlow: 'invalid', columns: ['1fr'], columnGap: -1, rowGap: Number.NaN, rows: ['auto'] } },
      placement: { kind: 'grid-item', gridItem: { columnSpan: 1, columnStart: 0, rowSpan: 0, rowStart: 'invalid' } },
      size: { aspectRatio: 0, height: 20, width: -1 }
    }));

    expect(result).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'LAYOUT_CONTAINER_PAYLOAD_INVALID', path: 'layout.container.grid.columnGap' }),
      expect.objectContaining({ code: 'LAYOUT_CONTAINER_PAYLOAD_INVALID', path: 'layout.container.grid.autoFlow' }),
      expect.objectContaining({ code: 'LAYOUT_DIMENSION_INVALID', path: 'layout.size.width' }),
      expect.objectContaining({ code: 'LAYOUT_ASPECT_RATIO_INVALID', path: 'layout.size.aspectRatio' }),
      expect.objectContaining({ code: 'LAYOUT_GRID_SPAN_INVALID', path: 'layout.placement.gridItem' }),
      expect.objectContaining({ code: 'LAYOUT_PLACEMENT_VALUE_INVALID', path: 'layout.placement.gridItem.rowStart' })
    ]));
  });

  it('rejects invalid flex and constraint placement values at error severity', () => {
    const result = diagnosePageStudioLayoutNode(...fixture({
      container: { constraints: { coordinateSpace: 'wrong', left: 'bad', stretchX: 'yes' }, mode: 'constraints' },
      placement: { constrained: { left: 'bad', stretchY: 'yes' }, flexItem: { alignSelf: 'invalid', basis: 'bad', grow: -1, order: 0, shrink: 1 }, kind: 'flex-item' },
      size: { height: 20, width: 40 }
    }));

    expect(result).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'LAYOUT_PLACEMENT_PAYLOAD_CONFLICT', path: 'layout.placement', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_PLACEMENT_VALUE_INVALID', path: 'layout.placement.flexItem.basis', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_PLACEMENT_VALUE_INVALID', path: 'layout.placement.flexItem.grow', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_CONSTRAINT_VALUE_INVALID', path: 'layout.placement.constrained.stretchY', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_CONSTRAINT_VALUE_INVALID', path: 'layout.container.constraints.left', severity: 'error' }),
      expect.objectContaining({ code: 'LAYOUT_CONSTRAINT_STRATEGY_REQUIRED', path: 'layout.container.constraints.coordinateSpace', severity: 'error' })
    ]));
  });

  it('rejects all legacy flat layout fields, including flex and grid fields', () => {
    const result = diagnosePageStudioLayoutNode(...fixture({ alignItems: 'center', display: 'flex', flexWrap: 'wrap', gap: 8, layoutMode: 'flex', order: 1, position: 'absolute', width: 80 }));

    expect(result.filter((diagnostic) => diagnostic.code === pageStudioLayoutDiagnosticCodes.legacyField).map((diagnostic) => diagnostic.path)).toEqual(expect.arrayContaining([
      'layout.alignItems', 'layout.display', 'layout.flexWrap', 'layout.gap', 'layout.layoutMode', 'layout.order', 'layout.position', 'layout.width'
    ]));
  });

  it('accepts a complete canonical protocol without legacy or validation diagnostics', () => {
    const protocol: LayoutProtocol = { container: defaultContainerLayout('free'), placement: defaultPlacement('free', 0, 0), size: { height: 20, width: 40 } };
    expect(diagnosePageStudioLayoutNode(...fixture(protocol))).toEqual([]);
  });
});

function fixture(layout: Record<string, unknown> | LayoutProtocol): [DesignerDocument, DesignerDocumentNode] {
  const node = { children: [], events: [], id: 'root', layout, parentId: null, props: {}, type: 'text' } as unknown as DesignerDocumentNode;
  const document = { elements: { root: node } } as unknown as DesignerDocument;
  return [document, node];
}
