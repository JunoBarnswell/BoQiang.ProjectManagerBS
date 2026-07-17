import { describe, expect, it } from 'vitest';

import { readRowConflictPayload } from './rowConflict';

describe('readRowConflictPayload', () => {
  it('normalizes a conflict response with server and local values', () => {
    expect(readRowConflictPayload({ conflict: true, canOverwrite: true, canRetry: true, localValues: { name: 'local' }, serverValues: { name: 'server' } })).toMatchObject({
      conflict: true,
      canOverwrite: true,
      canRetry: true,
      localValues: { name: 'local' },
      serverValues: { name: 'server' }
    });
  });

  it('rejects non-conflict and malformed payloads', () => {
    expect(readRowConflictPayload({ conflict: false })).toBeNull();
    expect(readRowConflictPayload(null)).toBeNull();
    expect(readRowConflictPayload([])).toBeNull();
  });
});
