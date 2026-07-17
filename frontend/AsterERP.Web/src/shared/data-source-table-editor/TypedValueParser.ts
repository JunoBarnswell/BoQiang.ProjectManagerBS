export type TypedValueParseResult =
  | { ok: true; value: unknown }
  | { error: string; ok: false };

const numericTokens = ['tinyint', 'smallint', 'int', 'integer', 'bigint', 'decimal', 'numeric', 'real', 'float', 'double', 'number'];

export function parseTypedValue(value: string, dataType: string): TypedValueParseResult {
  if (value === '') return { ok: true, value: null };
  const normalized = dataType.trim().toLowerCase();
  if (['bool', 'boolean'].includes(normalized)) {
    if (value === 'true' || value === '1') return { ok: true, value: true };
    if (value === 'false' || value === '0') return { ok: true, value: false };
    return { error: 'Boolean values must be true or false.', ok: false };
  }
  if (numericTokens.some((token) => normalized.includes(token))) {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) return { error: 'Enter a finite numeric value.', ok: false };
    if ((normalized.includes('int') || normalized.includes('bigint')) && !Number.isInteger(parsed)) return { error: 'Enter an integer value.', ok: false };
    return { ok: true, value: parsed };
  }
  if (normalized.includes('json') || normalized.includes('object')) {
    try { return { ok: true, value: JSON.parse(value) as unknown }; } catch { return { error: 'Enter valid JSON.', ok: false }; }
  }
  if (normalized.includes('binary') || normalized.includes('blob') || normalized.includes('bytea')) {
    if (!/^[A-Za-z0-9+/]*={0,2}$/.test(value) || value.length % 4 !== 0) return { error: 'Enter a valid Base64 binary value.', ok: false };
  }
  return { ok: true, value };
}

export function validateBinaryFile(file: File): string | null {
  if (file.size > 2 * 1024 * 1024) return 'Binary files must not exceed 2 MiB.';
  if (file.type && !(/^(application\/octet-stream|application\/pdf|image\/(png|jpeg|gif|webp)|text\/plain)$/).test(file.type)) return 'This binary MIME type is not allowed.';
  return null;
}
