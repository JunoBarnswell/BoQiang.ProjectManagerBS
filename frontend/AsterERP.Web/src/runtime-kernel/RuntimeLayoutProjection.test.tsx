// @vitest-environment jsdom

import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { describe, expect, it } from 'vitest';

import { projectRuntimeLayout, resolveRuntimeLayoutBox, type RuntimeLayoutRecord } from './RuntimeLayoutProjection';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

describe('RuntimeLayoutProjection', () => {
  it('keeps a free child absolute in a real DOM render', () => {
    const layout: RuntimeLayoutRecord = { height: 40, width: 120, x: 24, y: 16 };
    const box = resolveRuntimeLayoutBox(layout, { height: 300, width: 400 }, { layoutMode: 'free' });
    const projected = projectRuntimeLayout({ box, layout, parentLayout: { layoutMode: 'free' } });
    const view = applyRuntimeNodePresentation({
      componentType: 'text',
      element: { children: [], id: 'child', layout, name: 'Child', parentId: 'parent', props: {}, style: {}, type: 'text' },
      layout,
      loading: false,
      props: {},
      readOnly: false,
      runtime: { document: { elements: { parent: { children: [], id: 'parent', layout: { layoutMode: 'free' }, name: 'Parent', props: {}, type: 'layout.container' } }, modals: [], pages: [] } } as never,
      style: {},
      visible: true,
      disabled: false
    } as never, <div data-testid="runtime-free" />);
    const { getByTestId } = render(view as ReactElement);
    const element = getByTestId('runtime-free');

    expect(projected.style).toMatchObject({ position: 'absolute', left: 24, top: 16, width: 120, height: 40 });
    expect(element.style.position).toBe('absolute');
    expect(element.style.left).toBe('24px');
    expect(element.style.top).toBe('16px');
  });

  it('projects canonical four-layout protocols at the runtime CSS boundary', () => {
    const layout: RuntimeLayoutRecord = {
      container: { mode: 'free' },
      placement: { kind: 'absolute', absolute: { x: 24, y: 16 } },
      size: { height: 40, width: 120 }
    };
    const projected = projectRuntimeLayout({ layout, parentLayout: { container: { mode: 'free' }, placement: { kind: 'absolute', absolute: { x: 0, y: 0 } }, size: { height: 300, width: 400 } } });

    expect(projected.diagnostics).toEqual([]);
    expect(projected.style).toMatchObject({ height: 40, left: 24, position: 'absolute', top: 16, width: 120 });
  });

  it('treats canonical auto dimensions as runtime fallbacks instead of invalid legacy values', () => {
    const projected = projectRuntimeLayout({
      layout: { container: { mode: 'free' }, placement: { kind: 'absolute', absolute: { x: 0, y: 0 } }, size: { height: 'auto', width: 'auto' } }
    });

    expect(projected.diagnostics).toEqual([]);
  });

  it('projects anchor, stretch, center, z-index, and transform semantics without a whitelist', () => {
    const anchored = projectRuntimeLayout({ layout: { constraints: { bottom: 20, left: 12, right: 18, stretchX: true, top: 10, stretchY: true }, height: 40, width: 100, zIndex: 4, transform: 'rotate(2deg)' } });
    const centered = projectRuntimeLayout({ layout: { constraints: { centerX: 8, centerY: -4 }, height: 40, width: 100 }, style: { filter: 'blur(0)', opacity: 0.8 } });

    expect(anchored.style).toMatchObject({ bottom: 20, left: 12, position: 'absolute', right: 18, top: 10, zIndex: 4, transform: 'rotate(2deg)' });
    expect(anchored.style.width).toBeUndefined();
    expect(anchored.style.height).toBeUndefined();
    expect(centered.style).toMatchObject({ left: 'calc(50% + 8px)', top: 'calc(50% + -4px)', transform: 'translateX(-50%) translateY(-50%)', filter: 'blur(0)', opacity: 0.8 });
  });

  it('uses the same resolved boxes for flex and grid siblings and reports invalid constraints', () => {
    const flexParent = { display: 'flex', gap: 12, height: 100, width: 300 };
    const flexSiblings = [{ height: 24, width: 80 }, { height: 24, width: 60 }];
    const flexSecond = resolveRuntimeLayoutBox(flexSiblings[1], { height: 100, width: 300 }, flexParent, flexSiblings, 1);
    const gridParent = { columns: 2, gap: 10, height: 100, layoutMode: 'grid', width: 300 };
    const gridSecond = resolveRuntimeLayoutBox({}, { height: 100, width: 300 }, gridParent, [{}, {}], 1);
    const invalid = projectRuntimeLayout({ layout: { constraints: { left: 'bad', stretchX: 'yes' }, width: 'auto' } });

    expect(flexSecond).toMatchObject({ height: 24, width: 60, x: 92, y: 0 });
    expect(gridSecond.x).toBeGreaterThan(0);
    expect(invalid.diagnostics.map((diagnostic) => diagnostic.field)).toEqual(expect.arrayContaining(['width', 'constraints.left', 'constraints.stretchX']));
    expect(invalid.style.position).toBe('absolute');
  });

  it('accepts and normalizes legacy unitless numeric dimension strings in preview projection', () => {
    const layout: RuntimeLayoutRecord = { height: '120', minHeight: '0', minWidth: '0', width: '320', x: '0', y: '16' };
    const projected = projectRuntimeLayout({ layout, parentLayout: { layoutMode: 'free' } });

    expect(projected.diagnostics).toEqual([]);
    expect(projected.style).toMatchObject({ height: 120, left: 0, minHeight: 0, minWidth: 0, position: 'absolute', top: 16, width: 320 });
  });

  it('keeps independent grid tracks, gaps, spans, and CSS placement aligned', () => {
    const parentLayout: RuntimeLayoutRecord = { columns: 4, display: 'grid', columnGap: 10, layoutMode: 'grid', rowGap: 20, rows: 3 };
    const childLayout: RuntimeLayoutRecord = { columnSpan: 2, gridColumn: 2, gridRow: 2, rowSpan: 2 };
    const box = resolveRuntimeLayoutBox(childLayout, { height: 300, width: 410 }, parentLayout, [{}, {}, {}, {}, {}, {}], 5);
    const parent = projectRuntimeLayout({ layout: parentLayout });
    const child = projectRuntimeLayout({ layout: childLayout, parentLayout });

    expect(box.x).toBeCloseTo(105);
    expect(box.y).toBeCloseTo(106.6667);
    expect(box.width).toBeCloseTo(200);
    expect(box.height).toBeCloseTo(193.3333);
    expect(parent.style).toMatchObject({
      columnGap: 10,
      gridTemplateColumns: 'repeat(4, minmax(0, 1fr))',
      gridTemplateRows: 'repeat(3, minmax(0, 1fr))',
      rowGap: 20
    });
    expect(parent.style.gap).toBeUndefined();
    expect(child.style).toMatchObject({ gridColumn: '2 / span 2', gridRow: '2 / span 2' });
  });

  it('resolves explicit track templates and repeat/minmax tracks for grid boxes', () => {
    const parentLayout: RuntimeLayoutRecord = {
      display: 'grid',
      gridTemplateColumns: '100px 1fr 2fr',
      gridTemplateRows: '40px repeat(2, minmax(0, 1fr))',
      layoutMode: 'grid',
      columnGap: 8,
      rowGap: 12
    };
    const childLayout: RuntimeLayoutRecord = { gridColumn: '2', gridColumnSpan: '2', gridRow: 3, gridRowSpan: 1 };
    const box = resolveRuntimeLayoutBox(childLayout, { height: 300, width: 500 }, parentLayout, [{}, {}, {}, {}, {}, {}], 5);
    const child = projectRuntimeLayout({ layout: childLayout, parentLayout });

    expect(box).toMatchObject({ x: 108, y: 182 });
    expect(box.width).toBeCloseTo(392);
    expect(box.height).toBeCloseTo(118);
    expect(child.style).toMatchObject({ gridColumn: '2 / span 2', gridRow: '3 / span 1' });
  });

  it('uses independent rows and columns track arrays for auto placement', () => {
    const parentLayout: RuntimeLayoutRecord = {
      columns: ['80px', '1fr'],
      display: 'grid',
      layoutMode: 'grid',
      rows: ['40px', '1fr']
    };
    const siblings = [{}, {}, {}];
    const box = resolveRuntimeLayoutBox({}, { height: 200, width: 300 }, parentLayout, siblings, 2);
    const projected = projectRuntimeLayout({ layout: {}, parentLayout, siblingLayouts: siblings, siblingIndex: 2 });

    expect(box).toMatchObject({ x: 0, y: 40 });
    expect(projected.style).toMatchObject({ gridColumn: '1 / span 1', gridRow: '2 / span 1' });
    expect(projectRuntimeLayout({ layout: parentLayout }).style).toMatchObject({ gridTemplateColumns: '80px 1fr', gridTemplateRows: '40px 1fr' });
  });

  it('falls back safely for invalid grid counts, placement, gaps, and spans', () => {
    const parentLayout: RuntimeLayoutRecord = { columns: 0, display: 'grid', gap: 12, layoutMode: 'grid', rowGap: -4, rows: 'bad' };
    const childLayout: RuntimeLayoutRecord = { gridColumn: 'bad', gridColumnSpan: 0, gridRow: -3, gridRowSpan: 'bad' };
    const box = resolveRuntimeLayoutBox(childLayout, { height: 100, width: 200 }, parentLayout, [{}, {}], 1);
    const child = projectRuntimeLayout({ layout: childLayout, parentLayout });

    expect([box.x, box.y, box.width, box.height].every(Number.isFinite)).toBe(true);
    expect(child.style).toMatchObject({ gridColumn: '1 / span 1', gridRow: '1 / span 1' });
  });

  it('wraps row flex items and maps row/column gaps to projected styles', () => {
    const parentLayout: RuntimeLayoutRecord = { display: 'flex', flexWrap: 'wrap', gap: 4, height: 100, layoutMode: 'flex', rowGap: 10, width: 220 };
    const siblings = [{ height: 20, width: 100 }, { height: 20, width: 80 }, { height: 20, width: 60 }];
    const second = resolveRuntimeLayoutBox(siblings[1], { height: 100, width: 220 }, parentLayout, siblings, 1);
    const third = resolveRuntimeLayoutBox(siblings[2], { height: 100, width: 220 }, parentLayout, siblings, 2);
    const reverseThird = resolveRuntimeLayoutBox(siblings[2], { height: 100, width: 220 }, { ...parentLayout, flexWrap: 'wrap-reverse' }, siblings, 2);
    const projected = projectRuntimeLayout({ layout: parentLayout });

    expect(second).toMatchObject({ height: 20, width: 80, x: 104, y: 0 });
    expect(third).toMatchObject({ height: 20, width: 60, x: 0, y: 30 });
    expect(reverseThird).toMatchObject({ x: 0, y: 50 });
    expect(projected.style).toMatchObject({ columnGap: 4, flexWrap: 'wrap', rowGap: 10 });
  });

  it('wraps column flex items and mirrors line placement for wrap-reverse', () => {
    const parentLayout: RuntimeLayoutRecord = { display: 'flex', flexDirection: 'column', flexWrap: 'wrap', gap: 4, height: 220, layoutMode: 'flex', columnGap: 10, width: 100 };
    const siblings = [{ height: 100, width: 20 }, { height: 80, width: 20 }, { height: 60, width: 20 }];
    const third = resolveRuntimeLayoutBox(siblings[2], { height: 220, width: 100 }, parentLayout, siblings, 2);
    const reverseParent = { ...parentLayout, flexWrap: 'wrap-reverse' };
    const firstReverse = resolveRuntimeLayoutBox(siblings[0], { height: 220, width: 100 }, reverseParent, siblings, 0);
    const thirdReverse = resolveRuntimeLayoutBox(siblings[2], { height: 220, width: 100 }, reverseParent, siblings, 2);

    expect(third).toMatchObject({ height: 60, width: 20, x: 30, y: 0 });
    expect(firstReverse).toMatchObject({ x: 80, y: 0 });
    expect(thirdReverse).toMatchObject({ x: 50, y: 0 });
  });

  it('uses stable CSS order while preserving sibling index lookup semantics', () => {
    const parentLayout: RuntimeLayoutRecord = { display: 'flex', height: 80, layoutMode: 'flex', width: 240 };
    const siblings = [{ height: 20, order: 2, width: 40 }, { height: 20, order: 0, width: 60 }, { height: 20, order: 1, width: 80 }];

    expect(resolveRuntimeLayoutBox(siblings[0], { height: 80, width: 240 }, parentLayout, siblings, 0)).toMatchObject({ x: 140, y: 0 });
    expect(resolveRuntimeLayoutBox(siblings[1], { height: 80, width: 240 }, parentLayout, siblings, 1)).toMatchObject({ x: 0, y: 0 });
    expect(resolveRuntimeLayoutBox(siblings[2], { height: 80, width: 240 }, parentLayout, siblings, 2)).toMatchObject({ x: 60, y: 0 });
    expect(projectRuntimeLayout({ layout: siblings[0], parentLayout }).style.order).toBe(2);
  });

  it('lets alignSelf override parent alignment for start, center, end, and stretch', () => {
    const parentLayout: RuntimeLayoutRecord = { alignItems: 'center', display: 'flex', height: 100, layoutMode: 'flex', width: 240 };
    const siblings = [
      { alignSelf: 'start', height: 20, width: 40 },
      { height: 20, width: 40 },
      { alignSelf: 'end', height: 20, width: 40 },
      { alignSelf: 'stretch', width: 40 }
    ];

    expect(resolveRuntimeLayoutBox(siblings[0], { height: 100, width: 240 }, parentLayout, siblings, 0)).toMatchObject({ x: 0, y: 0 });
    expect(resolveRuntimeLayoutBox(siblings[1], { height: 100, width: 240 }, parentLayout, siblings, 1)).toMatchObject({ x: 40, y: 40 });
    expect(resolveRuntimeLayoutBox(siblings[2], { height: 100, width: 240 }, parentLayout, siblings, 2)).toMatchObject({ x: 80, y: 80 });
    expect(resolveRuntimeLayoutBox(siblings[3], { height: 100, width: 240 }, parentLayout, siblings, 3)).toMatchObject({ height: 100, x: 120, y: 0 });
    expect(projectRuntimeLayout({ layout: { alignSelf: 'end', order: 3 }, parentLayout }).style).toMatchObject({ alignSelf: 'flex-end', order: 3 });
  });
});
