import { describe, expect, it } from 'vitest';

import { planGridPlacements, resolveGridColumnCount, resolveGridTrackCounts } from './gridLayout';

describe('grid layout planning', () => {
  it('maps insertion order into two-dimensional row and column cells', () => {
    const result = planGridPlacements(
      [{ id: 'a', layout: {} }, { id: 'b', layout: {} }],
      [{ id: 'new', layout: {} }],
      1,
      2
    );

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.placements.get('new')).toEqual({ column: 2, columnSpan: 1, row: 1, rowSpan: 1 });
  });

  it('preserves valid spans and moves a spanning item to the next available cell', () => {
    const result = planGridPlacements(
      [{ id: 'wide', layout: { gridColumn: 1, gridColumnSpan: 2, gridRow: 1, gridRowSpan: 1 } }],
      [{ id: 'new', layout: { gridColumnSpan: 2, gridRowSpan: 1 } }],
      1,
      2
    );

    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.placements.get('new')).toEqual({ column: 1, columnSpan: 2, row: 2, rowSpan: 1 });
  });

  it('rejects non-positive, fractional, and over-wide spans', () => {
    expect(planGridPlacements([], [{ id: 'zero', layout: { gridRowSpan: 0 } }], 0, 2)).toMatchObject({ ok: false, diagnostic: 'Invalid grid span for zero' });
    expect(planGridPlacements([], [{ id: 'fraction', layout: { gridColumnSpan: 1.5 } }], 0, 2)).toMatchObject({ ok: false, diagnostic: 'Invalid grid span for fraction' });
    expect(planGridPlacements([], [{ id: 'wide', layout: { gridColumnSpan: 3 } }], 0, 2)).toMatchObject({ ok: false, diagnostic: 'Grid column span exceeds columns for wide' });
  });

  it('resolves explicit columns and safe defaults', () => {
    expect(resolveGridColumnCount({ columns: 3 })).toBe(3);
    expect(resolveGridTrackCounts({ columns: 3, rows: 2 })).toEqual({ columns: 3, rows: 2 });
    expect(resolveGridTrackCounts({ columns: 3 })).toEqual({ columns: 3, rows: 1 });
    expect(resolveGridColumnCount({ gridTemplateColumns: 'repeat(4, minmax(0, 1fr))' })).toBe(4);
    expect(resolveGridTrackCounts({ gridTemplateColumns: 'repeat(4, minmax(0, 1fr))', gridTemplateRows: 'repeat(2, minmax(0, 1fr))' })).toEqual({ columns: 4, rows: 2 });
    expect(resolveGridColumnCount({})).toBe(1);
  });

  it('rejects an invalid row track independently of the column track', () => {
    expect(planGridPlacements([], [{ id: 'item', layout: {} }], 0, { columns: 3, rows: 0 })).toMatchObject({ ok: false, diagnostic: 'Grid rows must be a positive integer' });
  });
});
