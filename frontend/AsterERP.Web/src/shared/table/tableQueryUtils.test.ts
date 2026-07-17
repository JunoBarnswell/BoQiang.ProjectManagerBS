import { describe, expect, it } from 'vitest';

import { evaluateQueryRows, extractFieldValue, sortRows } from './tableQueryUtils';

interface TestRow {
  amount: number;
  id: number;
  meta: {
    code: string;
  };
  status: 'disabled' | 'enabled';
}

function createRows(count: number): TestRow[] {
  return Array.from({ length: count }, (_, index) => ({
    amount: index % 200,
    id: index,
    meta: {
      code: `code-${index}`
    },
    status: index % 3 === 0 ? 'disabled' : 'enabled'
  }));
}

describe('tableQueryUtils', () => {
  it('filters large row sets with typed range and text conditions', () => {
    const rows = createRows(5_000);
    const result = evaluateQueryRows(
      rows,
      {
        conditions: [
          { field: 'amount', operator: 'between', value: 20, valueTo: 30 },
          { field: 'status', operator: 'equals', value: 'enabled' }
        ],
        matchMode: 'and'
      },
      new Map([
        ['amount', 'number'],
        ['status', 'text']
      ])
    );

    expect(result.length).toBeGreaterThan(0);
    expect(result.every((row) => row.amount >= 20 && row.amount <= 30 && row.status === 'enabled')).toBe(true);
  });

  it('supports OR multi-condition matching', () => {
    const rows = createRows(100);
    const result = evaluateQueryRows(
      rows,
      {
        conditions: [
          { field: 'meta.code', operator: 'equals', value: 'code-1' },
          { field: 'meta.code', operator: 'equals', value: 'code-99' }
        ],
        matchMode: 'or'
      },
      new Map([['meta.code', 'text']])
    );

    expect(result.map((row) => row.id)).toEqual([1, 99]);
  });

  it('sorts by nested fields and preserves source rows', () => {
    const rows: TestRow[] = [
      { amount: 2, id: 1, meta: { code: 'B-10' }, status: 'enabled' },
      { amount: 1, id: 2, meta: { code: 'B-2' }, status: 'enabled' },
      { amount: 1, id: 3, meta: { code: 'A-1' }, status: 'disabled' }
    ];

    const sorted = sortRows(
      rows,
      [
        { direction: 'asc', field: 'amount' },
        { direction: 'desc', field: 'meta.code' }
      ],
      (row, rule) => extractFieldValue(row, rule.field)
    );

    expect(sorted.map((row) => row.id)).toEqual([2, 3, 1]);
    expect(rows.map((row) => row.id)).toEqual([1, 2, 3]);
  });
});
