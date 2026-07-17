import { describe, expect, it } from 'vitest';

import { createConstraintAnchors, diagnoseConstraintConflicts, normalizeConstraintSpec, resolveConstraintRect, resizeConstraintRect } from './constraintModel';

describe('constraint layout model', () => {
  it('resolves anchored and stretched dimensions when the parent changes size', () => {
    const layout = { width: 80, height: 40, constraints: { left: 20, right: 30, top: 10, bottom: 20, stretchX: true, stretchY: true, minWidth: 100, maxWidth: 300, minHeight: 50, maxHeight: 160 } };
    expect(resolveConstraintRect(layout, { width: 400, height: 300 })).toEqual({ x: 20, y: 10, width: 300, height: 160 });
    expect(resolveConstraintRect(layout, { width: 220, height: 120 })).toEqual({ x: 20, y: 10, width: 170, height: 90 });
  });

  it('resolves center constraints and rejects malformed constraint fields', () => {
    expect(resolveConstraintRect({ width: 80, height: 40, constraints: { centerX: 12, centerY: -8 } }, { width: 400, height: 300 })).toEqual({ x: 172, y: 122, width: 80, height: 40 });
    expect(normalizeConstraintSpec({ left: '20', stretchX: true, maxWidth: 'bad', bad: 1 })).toEqual({ stretchX: true });
  });

  it('generates stable anchor values for all horizontal and vertical anchor types', () => {
    expect(createConstraintAnchors({ x: 100, y: 60, width: 200, height: 80 }, { width: 800, height: 600 }, ['left', 'bottom', 'centerX'])).toEqual({ left: 100, bottom: 460, centerX: -200 });
    expect(createConstraintAnchors({ x: 100, y: 60, width: 200, height: 80 }, { width: 800, height: 600 }, [])).toEqual({ left: 100, top: 60 });
  });

  it('diagnoses incompatible anchors, incomplete stretch, and invalid ranges', () => {
    expect(diagnoseConstraintConflicts({ left: 10, right: 20, centerX: 0, stretchX: true, minWidth: 300, maxWidth: 100, top: 10, centerY: 0, stretchY: true, minHeight: 200, maxHeight: 100 })).toEqual(expect.arrayContaining([
      expect.objectContaining({ code: 'CONSTRAINT_HORIZONTAL_ANCHOR_CONFLICT', path: 'constraints.centerX' }),
      expect.objectContaining({ code: 'CONSTRAINT_VERTICAL_ANCHOR_CONFLICT', path: 'constraints.centerY' }),
      expect.objectContaining({ code: 'CONSTRAINT_WIDTH_RANGE_INVALID', path: 'constraints.minWidth' }),
      expect.objectContaining({ code: 'CONSTRAINT_HEIGHT_RANGE_INVALID', path: 'constraints.minHeight' })
    ]));
    expect(diagnoseConstraintConflicts({ stretchX: true })).toEqual([expect.objectContaining({ code: 'CONSTRAINT_STRETCH_X_ANCHORS_REQUIRED' })]);
    expect(diagnoseConstraintConflicts({ left: 'bad', stretchX: 'yes', minWidth: 'bad' })).toEqual([
      expect.objectContaining({ code: 'CONSTRAINT_VALUE_INVALID', path: 'constraints.left' }),
      expect.objectContaining({ code: 'CONSTRAINT_VALUE_INVALID', path: 'constraints.minWidth' }),
      expect.objectContaining({ code: 'CONSTRAINT_VALUE_INVALID', path: 'constraints.stretchX' })
    ]);
  });

  it('resizes north and west edges while preserving active anchors and stretch bounds', () => {
    const result = resizeConstraintRect({ x: 40, y: 50, width: 200, height: 100 }, { width: 800, height: 600 }, { left: 40, top: 50, right: 560, bottom: 450, stretchX: true, stretchY: true }, 'northwest', { x: 20, y: 30 }, { minWidth: 100, minHeight: 80 });
    expect(result.rect).toEqual({ x: 60, y: 70, width: 180, height: 80 });
    expect(result.constraints).toMatchObject({ left: 60, top: 70, right: 560, bottom: 450, stretchX: true, stretchY: true });
  });

  it.each([
    ['north', { x: 0, y: 10 }, { top: 60 }],
    ['west', { x: 10, y: 0 }, { left: 50 }],
    ['east', { x: 10, y: 0 }, { right: 550 }],
    ['south', { x: 0, y: 10 }, { bottom: 440 }],
    ['northeast', { x: 0, y: 10 }, { top: 60, right: 560 }],
    ['southwest', { x: 10, y: 10 }, { left: 50, bottom: 440 }]
  ] as const)('updates placement for %s resize without mutating the source constraint value', (edge, delta, expected) => {
    const source = { left: 40, right: 560, top: 50, bottom: 450, stretchX: true, stretchY: true };
    const before = { ...source };
    const result = resizeConstraintRect({ x: 40, y: 50, width: 200, height: 100 }, { width: 800, height: 600 }, source, edge, delta);

    expect(result.constraints).toMatchObject(expected);
    expect(source).toEqual(before);
  });

  it('falls back to the original rectangle when resize limits conflict', () => {
    const rect = { x: 40, y: 50, width: 200, height: 100 };
    const result = resizeConstraintRect(rect, { width: 800, height: 600 }, { left: 40, top: 50 }, 'northwest', { x: 20, y: 20 }, { minWidth: 300, maxWidth: 100 });

    expect(result.rect).toEqual(rect);
    expect(result.constraints).toEqual({ left: 40, top: 50 });
  });
});
