import { useEffect, useReducer, useRef, type ChangeEvent, type KeyboardEvent } from 'react';

import {
  createRuntimeDataTableEditState,
  formatRuntimeDataTableEditorValue,
  readRuntimeDataTableConflict,
  runtimeDataTableErrorMessage,
  runtimeDataTableValueKind,
  transitionRuntimeDataTableEditState,
  validateRuntimeDataTableValue,
  type RuntimeDataTableColumnDefinition,
  type RuntimeDataTableCommit,
  type RuntimeDataTableCommitResult,
  type RuntimeDataTableConflict,
  type RuntimeDataTableEditState
} from './runtimeDataTableEditing';

export interface RuntimeDataTableCellEditorProps {
  ariaLabel: string;
  column: RuntimeDataTableColumnDefinition;
  commitOnBlur: boolean;
  disabled: boolean;
  onCommit: RuntimeDataTableCommit;
  row: Record<string, unknown>;
  rowIndex: number;
  rowKey: string;
  value: unknown;
}

export function RuntimeDataTableCellEditor({ ariaLabel, column, commitOnBlur, disabled, onCommit, row, rowIndex, rowKey, value }: RuntimeDataTableCellEditorProps) {
  const [state, dispatch] = useReducer(
    transitionRuntimeDataTableEditState,
    value,
    createRuntimeDataTableEditState
  );
  const stateRef = useRef<RuntimeDataTableEditState>(state);
  const baseRowRef = useRef(row);
  const skipNextBlurRef = useRef(false);
  const activeCommitRef = useRef(false);

  useEffect(() => {
    stateRef.current = state;
  }, [state]);
  useEffect(() => {
    if (state.status === 'idle') {
      baseRowRef.current = row;
      dispatch({ type: 'sync', value });
    }
  }, [row, state.status, value]);

  const kind = runtimeDataTableValueKind(column);
  const disabledForState = disabled || state.status === 'saving' || state.status === 'validating';
  const inputValue = formatRuntimeDataTableEditorValue(state.draftValue, column);
  const inputTextValue = typeof inputValue === 'boolean' ? '' : inputValue;

  function changeDraft(nextValue: unknown): void {
    dispatch({ draftValue: nextValue, type: 'change' });
  }

  function beginEdit(): void {
    dispatch({ type: 'begin' });
  }

  function cancelEdit(): void {
    if (stateRef.current.status === 'saving' || stateRef.current.status === 'validating') return;
    dispatch({ type: 'cancel' });
  }

  async function submit(source: 'blur' | 'enter' | 'retry' | 'overwrite' = 'enter', conflictResolution?: 'overwrite' | 'retry'): Promise<void> {
    const current = stateRef.current;
    if (activeCommitRef.current || current.status === 'saving' || current.status === 'validating') return;
    if (current.status === 'conflict' && !conflictResolution) return;
    if (current.status === 'idle' || current.status === 'editing' && Object.is(current.draftValue, current.originalValue)) {
      dispatch({ type: 'cancel' });
      return;
    }

    const validation = validateRuntimeDataTableValue(current.draftValue, column);
    dispatch({ type: 'validate' });
    if (!validation.ok) {
      dispatch({ error: validation.error ?? 'The value is invalid.', type: 'validationFailed' });
      return;
    }

    const baseRow = conflictResolution === 'retry' && current.conflict?.serverRow
      ? current.conflict.serverRow
      : conflictResolution === 'overwrite' && current.conflict?.localRow
        ? current.conflict.localRow
        : baseRowRef.current;
    const originalValue = conflictResolution === 'retry' && current.conflict?.serverValue !== undefined
      ? current.conflict.serverValue
      : current.originalValue;
    const nextRow = { ...baseRow, [column.fieldCode]: validation.value };
    const request = {
      conflictResolution,
      fieldCode: column.fieldCode,
      localRow: { ...baseRowRef.current, [column.fieldCode]: validation.value },
      nextRow,
      originalRow: baseRow,
      originalValue,
      rowIndex,
      rowKey,
      value: validation.value
    };

    activeCommitRef.current = true;
    if (current.status === 'conflict') {
      dispatch({ originalValue, type: 'rebase' });
    }
    dispatch({ type: 'validate' });
    dispatch({ type: 'validationPassed' });
    dispatch({ type: 'saveStarted' });
    try {
      const result = await onCommit(request);
      const resolved = result as RuntimeDataTableCommitResult | void;
      const savedValue = resolved?.value ?? validation.value;
      baseRowRef.current = resolved?.row ?? nextRow;
      dispatch({ type: 'saveSucceeded', value: savedValue });
    } catch (error) {
      const conflict = readRuntimeDataTableConflict(error);
      if (conflict) {
        const normalizedConflict = withFallbackConflict(conflict, request.nextRow, request.localRow, column.fieldCode);
        dispatch({ conflict: normalizedConflict, type: 'conflict' });
      } else {
        dispatch({ error: runtimeDataTableErrorMessage(error), type: 'saveFailed' });
      }
    } finally {
      activeCommitRef.current = false;
    }

    if (source === 'enter') skipNextBlurRef.current = true;
  }

  function handleBlur(): void {
    if (skipNextBlurRef.current) {
      skipNextBlurRef.current = false;
      return;
    }
    if (commitOnBlur) void submit('blur');
  }

  function handleKeyDown(event: KeyboardEvent<HTMLInputElement | HTMLTextAreaElement>): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      cancelEdit();
      return;
    }
    if (event.key === 'Enter') {
      event.preventDefault();
      skipNextBlurRef.current = true;
      void submit('enter');
    }
  }

  function retryAfterConflict(resolution: 'retry' | 'overwrite'): void {
    if (!stateRef.current.conflict) return;
    if (resolution === 'retry') {
      dispatch({ originalValue: stateRef.current.conflict.serverValue, type: 'rebase' });
    }
    void submit(resolution, resolution);
  }

  const inputProps = {
    'aria-label': ariaLabel,
    className: 'form-input h-8 min-w-24 text-xs',
    disabled: disabledForState,
    onBlur: handleBlur,
    onChange: (event: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => changeDraft(kind === 'boolean' && 'checked' in event.currentTarget ? event.currentTarget.checked : event.currentTarget.value),
    onFocus: beginEdit,
    onKeyDown: handleKeyDown,
    value: inputTextValue
  };

  return <div className="grid gap-1" data-edit-state={state.status} data-field-code={column.fieldCode}>
    {kind === 'json'
      ? <textarea {...inputProps} className="form-input min-h-20 min-w-40 text-xs" />
      : <input {...inputProps} checked={kind === 'boolean' ? Boolean(inputValue) : undefined} type={kind === 'boolean' ? 'checkbox' : kind === 'number' ? 'number' : kind === 'date' ? dateInputType(column) : 'text'} />}
    {state.status !== 'idle' ? <span aria-live="polite" className="text-[11px] text-slate-500">{editStatusLabel(state.status)}</span> : null}
    {state.error ? <div className="text-[11px] text-red-600" role="alert">{state.error}</div> : null}
    {state.status === 'failed' ? <button className="secondary-button h-6 w-fit px-2 text-[11px]" type="button" onMouseDown={(event) => event.preventDefault()} onClick={() => void submit('retry')}>Retry</button> : null}
    {state.status === 'conflict' && state.conflict ? <ConflictPanel conflict={state.conflict} onOverwrite={() => retryAfterConflict('overwrite')} onRetry={() => retryAfterConflict('retry')} /> : null}
  </div>;
}

function ConflictPanel({ conflict, onOverwrite, onRetry }: { conflict: RuntimeDataTableConflict; onOverwrite: () => void; onRetry: () => void }) {
  return <div className="grid gap-1 rounded border border-amber-300 bg-amber-50 p-2 text-[11px] text-amber-950" role="alert">
    <div>{conflict.message}</div>
    <div className="grid gap-1 sm:grid-cols-2">
      <div><div className="font-semibold">Server value</div><pre className="max-h-24 overflow-auto rounded bg-white p-1">{formatConflictValue(conflict.serverValue ?? conflict.serverRow)}</pre></div>
      <div><div className="font-semibold">Local value</div><pre className="max-h-24 overflow-auto rounded bg-white p-1">{formatConflictValue(conflict.localValue ?? conflict.localRow)}</pre></div>
    </div>
    <div className="flex gap-1">
      {conflict.canRetry ? <button className="secondary-button h-6 px-2 text-[11px]" type="button" onMouseDown={(event) => event.preventDefault()} onClick={onRetry}>Retry with server base</button> : null}
      {conflict.canOverwrite ? <button className="danger-button h-6 px-2 text-[11px]" type="button" onMouseDown={(event) => event.preventDefault()} onClick={onOverwrite}>Overwrite server</button> : null}
    </div>
  </div>;
}

function withFallbackConflict(conflict: RuntimeDataTableConflict, nextRow: Record<string, unknown>, localRow: Record<string, unknown>, fieldCode: string): RuntimeDataTableConflict {
  return {
    ...conflict,
    canOverwrite: conflict.canOverwrite || Boolean(conflict.localRow),
    canRetry: conflict.canRetry || Boolean(conflict.serverRow),
    localRow: conflict.localRow ?? localRow,
    localValue: conflict.localValue ?? nextRow[fieldCode],
    serverValue: conflict.serverValue ?? conflict.serverRow?.[fieldCode]
  };
}

function formatConflictValue(value: unknown): string {
  if (value === undefined) return '—';
  return typeof value === 'string' ? value : JSON.stringify(value, null, 2);
}

function editStatusLabel(status: RuntimeDataTableEditState['status']): string {
  return status === 'editing' ? 'Editing' : status === 'dirty' ? 'Unsaved changes' : status === 'validating' ? 'Validating…' : status === 'saving' ? 'Saving…' : status === 'conflict' ? 'Conflict' : 'Save failed';
}

function dateInputType(column: RuntimeDataTableColumnDefinition): 'date' | 'datetime-local' {
  return String(column.dataType ?? column.editorType ?? '').toLowerCase().includes('time') ? 'datetime-local' : 'date';
}
