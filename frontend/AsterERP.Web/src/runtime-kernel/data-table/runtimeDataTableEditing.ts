export type RuntimeDataTableEditStatus = 'idle' | 'editing' | 'dirty' | 'validating' | 'saving' | 'conflict' | 'failed';

export interface RuntimeDataTableColumnDefinition {
  dataType?: string;
  editorType?: string;
  editable?: boolean;
  fieldCode: string;
  fieldName: string;
  permissionDenied?: boolean;
  primaryKey?: boolean;
  readOnly?: boolean;
  required?: boolean;
  validation?: Record<string, unknown>;
  writable?: boolean;
}

export interface RuntimeDataTableEditingOptions {
  commitOnBlur: boolean;
  enabled: boolean;
  keyField: string;
}

export interface RuntimeDataTableConflict {
  canOverwrite: boolean;
  canRetry: boolean;
  message: string;
  localRow?: Record<string, unknown>;
  localValue?: unknown;
  serverRow?: Record<string, unknown>;
  serverValue?: unknown;
}

export interface RuntimeDataTableEditState {
  conflict?: RuntimeDataTableConflict;
  draftValue: unknown;
  error?: string;
  originalValue: unknown;
  status: RuntimeDataTableEditStatus;
}

export type RuntimeDataTableEditEvent =
  | { type: 'begin' }
  | { draftValue: unknown; type: 'change' }
  | { type: 'validate' }
  | { error: string; type: 'validationFailed' }
  | { type: 'validationPassed' }
  | { type: 'saveStarted' }
  | { type: 'saveSucceeded'; value: unknown }
  | { error: string; type: 'saveFailed' }
  | { conflict: RuntimeDataTableConflict; type: 'conflict' }
  | { type: 'cancel' }
  | { originalValue: unknown; type: 'rebase' }
  | { value: unknown; type: 'sync' };

export interface RuntimeDataTableCommitRequest {
  conflictResolution?: 'overwrite' | 'retry';
  fieldCode: string;
  localRow: Record<string, unknown>;
  originalRow: Record<string, unknown>;
  originalValue: unknown;
  rowIndex: number;
  rowKey: string;
  value: unknown;
  nextRow: Record<string, unknown>;
}

export interface RuntimeDataTableCommitResult {
  row?: Record<string, unknown>;
  value?: unknown;
}

export type RuntimeDataTableCommit = (request: RuntimeDataTableCommitRequest) => Promise<RuntimeDataTableCommitResult | void>;

export function createRuntimeDataTableEditState(value: unknown): RuntimeDataTableEditState {
  return { draftValue: value, originalValue: value, status: 'idle' };
}

export function transitionRuntimeDataTableEditState(
  state: RuntimeDataTableEditState,
  event: RuntimeDataTableEditEvent
): RuntimeDataTableEditState {
  switch (event.type) {
    case 'begin':
      return state.status === 'saving' || state.status === 'validating'
        ? state
        : { ...state, error: undefined, status: 'editing' };
    case 'change':
      return state.status === 'saving' || state.status === 'validating'
        ? state
        : { ...state, conflict: undefined, draftValue: event.draftValue, error: undefined, status: 'dirty' };
    case 'validate':
      return state.status === 'editing' || state.status === 'dirty' || state.status === 'failed'
        ? { ...state, error: undefined, status: 'validating' }
        : state;
    case 'validationFailed':
      return state.status === 'validating' ? { ...state, error: event.error, status: 'failed' } : state;
    case 'validationPassed':
      return state.status === 'validating' ? { ...state, error: undefined } : state;
    case 'saveStarted':
      return state.status === 'validating' ? { ...state, error: undefined, status: 'saving' } : state;
    case 'saveSucceeded':
      return state.status === 'saving'
        ? { draftValue: event.value, originalValue: event.value, status: 'idle' }
        : state;
    case 'saveFailed':
      return state.status === 'saving' ? { ...state, error: event.error, status: 'failed' } : state;
    case 'conflict':
      return state.status === 'saving' ? { ...state, conflict: event.conflict, error: event.conflict.message, status: 'conflict' } : state;
    case 'cancel':
      return state.status === 'saving' || state.status === 'validating'
        ? state
        : { draftValue: state.originalValue, originalValue: state.originalValue, status: 'idle' };
    case 'rebase':
      return state.status === 'conflict'
        ? { ...state, error: undefined, originalValue: event.originalValue, status: 'dirty' }
        : state;
    case 'sync':
      return state.status === 'idle'
        ? { draftValue: event.value, originalValue: event.value, status: 'idle' }
        : state;
  }
}

export function readRuntimeDataTableEditingOptions(props: Record<string, unknown>): RuntimeDataTableEditingOptions {
  const editing = isRecord(props.editing) ? props.editing : {};
  const keyField = readString(editing.keyField) ?? readString(props.keyField) ?? readString(props.primaryKeyField) ?? 'id';
  return {
    commitOnBlur: editing.commitOnBlur !== false && props.commitOnBlur !== false && props.blurCommit !== false,
    enabled: props.editable === true && editing.enabled !== false,
    keyField
  };
}

export function isRuntimeDataTableColumnEditable(
  column: RuntimeDataTableColumnDefinition,
  row: Record<string, unknown>,
  options: RuntimeDataTableEditingOptions,
  disabled: boolean,
  readOnly: boolean,
  props: Record<string, unknown>
): boolean {
  if (!options.enabled || disabled || readOnly) return false;
  if (column.readOnly === true || column.writable === false || column.editable === false || column.permissionDenied === true) return false;
  if (props.editPermissionGranted === false) return false;
  const inferredPrimaryKey = column.fieldCode === 'id' && row.id !== undefined || column.fieldCode === 'key' && row.key !== undefined;
  if (column.primaryKey === true || inferredPrimaryKey || column.fieldCode === options.keyField || column.fieldCode === String(row.primaryKeyField ?? '')) return false;
  return true;
}

export function validateRuntimeDataTableValue(value: unknown, column: RuntimeDataTableColumnDefinition): { ok: boolean; value?: unknown; error?: string } {
  const kind = runtimeDataTableValueKind(column);
  const validation = column.validation ?? {};
  const empty = value === null || value === undefined || value === '';
  if (column.required === true || validation.required === true) {
    if (empty) return { error: 'A value is required.', ok: false };
  } else if (empty) {
    return { ok: true, value: kind === 'number' || kind === 'boolean' || kind === 'json' ? null : '' };
  }

  if (kind === 'number') {
    const parsed = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(parsed)) return { error: 'Enter a valid number.', ok: false };
    const min = readFiniteNumber(validation.min ?? column.validation?.minimum);
    const max = readFiniteNumber(validation.max ?? column.validation?.maximum);
    if (min !== undefined && parsed < min) return { error: `Value must be at least ${min}.`, ok: false };
    if (max !== undefined && parsed > max) return { error: `Value must be at most ${max}.`, ok: false };
    return { ok: true, value: parsed };
  }
  if (kind === 'boolean') {
    if (typeof value === 'boolean') return { ok: true, value };
    if (value === 'true' || value === 'false') return { ok: true, value: value === 'true' };
    return { error: 'Enter a valid boolean.', ok: false };
  }
  if (kind === 'json') {
    if (typeof value !== 'string') return { ok: true, value };
    try {
      return { ok: true, value: JSON.parse(value) as unknown };
    } catch {
      return { error: 'Enter valid JSON.', ok: false };
    }
  }
  if (kind === 'date') {
    const dateValue = String(value);
    if (Number.isNaN(Date.parse(dateValue))) return { error: 'Enter a valid date.', ok: false };
    return { ok: true, value: dateValue };
  }

  const text = String(value);
  const minLength = readFiniteNumber(validation.minLength);
  const maxLength = readFiniteNumber(validation.maxLength);
  if (minLength !== undefined && text.length < minLength) return { error: `Enter at least ${minLength} characters.`, ok: false };
  if (maxLength !== undefined && text.length > maxLength) return { error: `Enter no more than ${maxLength} characters.`, ok: false };
  if (typeof validation.pattern === 'string') {
    try {
      if (!new RegExp(validation.pattern).test(text)) return { error: 'The value has an invalid format.', ok: false };
    } catch {
      return { error: 'The column validation pattern is invalid.', ok: false };
    }
  }
  return { ok: true, value: text };
}

export function runtimeDataTableValueKind(column: RuntimeDataTableColumnDefinition): 'boolean' | 'date' | 'json' | 'number' | 'text' {
  const type = String(column.dataType ?? column.editorType ?? 'text').toLowerCase().replace(/[-_\s]/g, '');
  if (type.includes('json') || type === 'object') return 'json';
  if (type.includes('bool')) return 'boolean';
  if (type.includes('date') || type.includes('time')) return 'date';
  if (type.includes('number') || type.includes('int') || type.includes('decimal') || type.includes('float') || type.includes('double') || type.includes('money')) return 'number';
  return 'text';
}

export function formatRuntimeDataTableEditorValue(value: unknown, column: RuntimeDataTableColumnDefinition): string | boolean {
  if (runtimeDataTableValueKind(column) === 'boolean') return value === true || value === 'true';
  if (value === null || value === undefined) return '';
  return runtimeDataTableValueKind(column) === 'json' && typeof value !== 'string' ? JSON.stringify(value, null, 2) : String(value);
}

export function readRuntimeDataTableConflict(error: unknown): RuntimeDataTableConflict | null {
  const candidate = findConflictCandidate(error, new Set<unknown>(), 0);
  if (!candidate) return null;
  const serverRow = isRecord(candidate.serverValues) ? candidate.serverValues : undefined;
  const localRow = isRecord(candidate.localValues) ? candidate.localValues : undefined;
  return {
    canOverwrite: candidate.canOverwrite === true || (candidate.canOverwrite === undefined && Boolean(localRow)),
    canRetry: candidate.canRetry !== false && Boolean(serverRow),
    localRow,
    localValue: candidate.localValue,
    message: readString(candidate.conflictMessage) ?? 'The value changed on the server before it was saved.',
    serverRow,
    serverValue: candidate.serverValue
  };
}

export function runtimeDataTableErrorMessage(error: unknown): string {
  return error instanceof Error && error.message.trim() ? error.message : 'The value could not be saved.';
}

function findConflictCandidate(value: unknown, visited: Set<unknown>, depth: number): Record<string, unknown> | null {
  if (!isRecord(value) || depth > 4 || visited.has(value)) return null;
  visited.add(value);
  const status = value.status ?? (isRecord(value.response) ? value.response.status : undefined);
  const nestedData = value.data;
  if (isRecord(nestedData) && (nestedData.conflict === true || isRecord(nestedData.serverValues) || isRecord(nestedData.localValues))) {
    return nestedData;
  }
  if (status === 409 || value.conflict === true) {
    const payload = value;
    return payload;
  }
  for (const nested of [value.cause, value.raw, value.response, value.data]) {
    const found = findConflictCandidate(nested, visited, depth + 1);
    if (found) return found;
  }
  return null;
}

function readFiniteNumber(value: unknown): number | undefined {
  if (value === undefined || value === null || value === '') return undefined;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : undefined;
}

function readString(value: unknown): string | undefined {
  return typeof value === 'string' && value.trim() ? value.trim() : undefined;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
