import { describe, expect, it } from 'vitest';

import { calculateLayoutChanges, createConstraintChange, createLayoutContainerChange, createLayoutOperationChange, isLayoutOperationSupported, resolveLayoutMode, resolveLayoutStyle } from './layoutOperations';

const nodes = [{ id: 'a', x: 10, y: 20, width: 100, height: 40 }, { id: 'b', x: 300, y: 80, width: 50, height: 20 }, { id: 'c', x: 500, y: 120, width: 75, height: 30 }];

describe('layout operations', () => {
  it('calculates alignment and equal-size changes without mutating source nodes', () => {
    const changes = calculateLayoutChanges(nodes, 'align-right');
    expect(changes.get('a')).toEqual({ x: 475 });
    expect(changes.get('b')).toEqual({ x: 525 });
    expect(nodes[0].x).toBe(10);
  });

  it('distributes nodes using sorted geometry and one deterministic change set', () => {
    const changes = calculateLayoutChanges(nodes, 'distribute-horizontal');
    expect(changes.get('a')?.x).toBe(10);
    expect(changes.get('b')?.x).toBe(280);
    expect(changes.get('c')?.x).toBe(500);
  });

  it('emits native container protocols for flex, grid, free and constraints', () => {
    expect(createLayoutContainerChange({ mode: 'flex', gap: 12, align: 'center', justify: 'space-between' })).toMatchObject({ display: 'flex', layoutMode: 'flex', gap: 12, alignItems: 'center', justifyContent: 'space-between' });
    expect(createLayoutContainerChange({ mode: 'grid', columns: 3 })).toMatchObject({ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))' });
    expect(createLayoutContainerChange({ mode: 'free' })).toEqual({ display: 'block', layoutMode: 'free' });
    expect(createConstraintChange(nodes[0], { id: 'root', x: 0, y: 0, width: 800, height: 600 }, ['right', 'bottom'])).toEqual({ constraints: { right: 690, bottom: 540 } });
    expect(createConstraintChange(nodes[0], { id: 'root', x: 0, y: 0, width: 800, height: 600 })).toEqual({ constraints: { left: 10, top: 20 } });
    expect(resolveLayoutStyle({ layoutMode: 'grid', columns: 2, gap: 16 })).toMatchObject({ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: 16 });
    expect(resolveLayoutMode({ display: 'flex' })).toBe('flex');
    expect(resolveLayoutStyle({ display: 'flex', gap: 8 })).toMatchObject({ display: 'flex', gap: 8 });
  });

  it('uses container semantics for flex and grid instead of child geometry alignment', () => {
    expect(createLayoutOperationChange('flex', 'align-center', 'row')).toEqual({ justifyContent: 'center' });
    expect(createLayoutOperationChange('flex', 'align-center', 'column')).toEqual({ alignItems: 'center' });
    expect(createLayoutOperationChange('grid', 'align-right')).toEqual({ justifyItems: 'end' });
    expect(createLayoutOperationChange('constraints', 'align-right')).toBeNull();
    expect(createLayoutOperationChange('grid', 'distribute-horizontal')).toBeNull();
  });

  it('makes unsupported mode operations explicit instead of silently changing child geometry', () => {
    expect(isLayoutOperationSupported('free', 'same-width')).toBe(true);
    expect(isLayoutOperationSupported('flex', 'same-width')).toBe(false);
    expect(isLayoutOperationSupported('grid', 'distribute-horizontal')).toBe(false);
    expect(isLayoutOperationSupported('constraints', 'align-left')).toBe(false);
  });

  it('clamps free alignment and resize geometry to explicit parent bounds', () => {
    const changes = calculateLayoutChanges([
      { id: 'a', x: -20, y: 10, width: 80, height: 40 },
      { id: 'b', x: 260, y: 80, width: 80, height: 40 }
    ], 'align-right', 'free', { width: 300, height: 120 });

    expect(changes.get('a')).toEqual({ x: 220 });
    expect(changes.get('b')).toEqual({ x: 220 });
  });
});
