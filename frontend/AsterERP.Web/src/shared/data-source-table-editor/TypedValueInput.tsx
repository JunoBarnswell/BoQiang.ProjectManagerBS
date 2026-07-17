import { useMemo, type ChangeEvent, type KeyboardEventHandler } from 'react';

import { validateBinaryFile } from './TypedValueParser';

interface TypedValueInputProps {
  ariaLabel?: string;
  className?: string;
  dataType: string;
  disabled?: boolean;
  onBlur?: () => void;
  onKeyDown?: KeyboardEventHandler<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>;
  onValidationError?: (message: string) => void;
  value: string;
  onChange: (value: string) => void;
}

export function TypedValueInput({ ariaLabel, className, dataType, disabled, onBlur, onKeyDown, onValidationError, value, onChange }: TypedValueInputProps) {
  const kind = useMemo(() => resolveInputKind(dataType), [dataType]);
  if (kind === 'boolean') {
    return <input aria-label={ariaLabel} checked={value === 'true' || value === '1'} className={className} disabled={disabled} type="checkbox" onBlur={onBlur} onChange={(event) => onChange(event.target.checked ? 'true' : 'false')} onKeyDown={onKeyDown} />;
  }
  if (kind === 'enum') {
    return <select aria-label={ariaLabel} className={className} disabled={disabled} value={value} onBlur={onBlur} onChange={(event) => onChange(event.target.value)} onKeyDown={onKeyDown}>
      <option value="">{value ? value : 'Select an option'}</option>
      {resolveEnumOptions(dataType).map((option) => <option key={option} value={option}>{option}</option>)}
    </select>;
  }
  if (kind === 'json') {
    return <textarea aria-label={ariaLabel} className={className} disabled={disabled} rows={3} value={value} onBlur={onBlur} onChange={(event) => onChange(event.target.value)} onKeyDown={onKeyDown} />;
  }
  if (kind === 'binary') {
    return <div className="space-y-1">
      <input aria-label={ariaLabel} className={className} disabled={disabled} type="file" onBlur={onBlur} onChange={(event) => void readBinaryFile(event, onChange, onValidationError)} onKeyDown={onKeyDown} />
      {value ? <span className="block truncate text-[11px] text-slate-500">{value}</span> : null}
    </div>;
  }
  return <input aria-label={ariaLabel} className={className} disabled={disabled} inputMode={kind === 'number' ? 'decimal' : undefined} type={kind === 'date' ? 'date' : kind === 'datetime' ? 'datetime-local' : kind === 'number' ? 'number' : 'text'} value={normalizeDateValue(value, kind)} onBlur={onBlur} onChange={(event) => onChange(event.target.value)} onKeyDown={onKeyDown} />;
}

export function resolveInputKind(dataType: string): 'binary' | 'boolean' | 'date' | 'datetime' | 'enum' | 'json' | 'number' | 'text' {
  const normalized = dataType.trim().toLowerCase();
  if (['bool', 'boolean'].includes(normalized)) return 'boolean';
  if (normalized.includes('binary') || normalized.includes('blob') || normalized.includes('bytea') || normalized.includes('rowversion')) return 'binary';
  if (normalized.startsWith('enum(') && resolveEnumOptions(dataType).length > 0) return 'enum';
  if (normalized.includes('json') || normalized.includes('object')) return 'json';
  if (normalized.includes('datetime') || normalized.includes('timestamp')) return 'datetime';
  if (normalized === 'date' || normalized.endsWith(' date')) return 'date';
  if (['tinyint', 'smallint', 'int', 'integer', 'bigint', 'decimal', 'numeric', 'real', 'float', 'double', 'number'].some((item) => normalized.includes(item))) return 'number';
  return 'text';
}

export function resolveEnumOptions(dataType: string): string[] {
  const match = dataType.trim().match(/^enum\((.*)\)$/i);
  if (!match) return [];
  return match[1].split(',').map((item) => item.trim().replace(/^['"]|['"]$/g, '')).filter(Boolean);
}

async function readBinaryFile(event: ChangeEvent<HTMLInputElement>, onChange: (value: string) => void, onValidationError?: (message: string) => void) {
  const file = event.target.files?.[0];
  if (!file) return;
  const validationError = validateBinaryFile(file);
  if (validationError) { event.target.value = ''; onValidationError?.(validationError); return; }
  const bytes = new Uint8Array(await file.arrayBuffer());
  let binary = '';
  bytes.forEach((byte) => { binary += String.fromCharCode(byte); });
  onChange(btoa(binary));
}

function normalizeDateValue(value: string, kind: ReturnType<typeof resolveInputKind>) {
  if (kind === 'date') return value.slice(0, 10);
  if (kind === 'datetime') return value.replace(' ', 'T').slice(0, 16);
  return value;
}
