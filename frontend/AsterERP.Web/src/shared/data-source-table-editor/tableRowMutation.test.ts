import { describe, expect, it } from 'vitest';

import {
  buildTableRowDeleteRequest,
  buildTableRowInsertRequest,
  buildTableRowUpdateRequest
} from './tableRowMutation';

describe('table row mutation requests', () => {
  it('sends version-based concurrency and the exact affected-row confirmation', () => {
    const request = buildTableRowUpdateRequest(
      { id: 7, name: 'Alice', version: 4 },
      { id: 7, name: 'Alicia', version: 4 },
      { concurrencyColumn: 'version', primaryKeys: ['id'] }
    );

    expect(request).toMatchObject({
      confirmed: true,
      expectedAffectedRows: 1,
      keyValues: { id: 7 },
      originalValues: { name: 'Alice', version: 4 },
      versionValue: 4,
      values: { id: 7, name: 'Alicia', version: 4 }
    });
  });

  it('uses all non-key values as the original-value predicate when no version column exists', () => {
    const request = buildTableRowDeleteRequest(
      { ID: 'A-1', status: null, quantity: 2 },
      { primaryKeys: ['id'] }
    );

    expect(request).toEqual({
      confirmed: true,
      expectedAffectedRows: 1,
      keyValues: { ID: 'A-1' },
      originalValues: { status: null, quantity: 2 }
    });
    expect(request.versionValue).toBeUndefined();
  });

  it('does not attach concurrency predicates to an insert', () => {
    expect(buildTableRowInsertRequest({ id: 8, name: 'Bob' })).toEqual({
      confirmed: true,
      values: { id: 8, name: 'Bob' }
    });
  });
});
