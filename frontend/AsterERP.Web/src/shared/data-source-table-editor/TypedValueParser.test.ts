import { describe, expect, it } from 'vitest';

import { parseTypedValue, validateBinaryFile } from './TypedValueParser';

describe('parseTypedValue', () => {
  it('rejects invalid numeric and JSON values instead of forwarding strings', () => {
    expect(parseTypedValue('NaN', 'DECIMAL(10,2)').ok).toBe(false);
    expect(parseTypedValue('{', 'JSON').ok).toBe(false);
    expect(parseTypedValue('12.5', 'INTEGER').ok).toBe(false);
  });

  it('enforces binary size and MIME safeguards', () => {
    expect(validateBinaryFile(new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'too-large.bin', { type: 'application/octet-stream' }))).toContain('2 MiB');
    expect(validateBinaryFile(new File(['x'], 'unsafe.exe', { type: 'application/x-msdownload' }))).toContain('MIME');
  });
});
