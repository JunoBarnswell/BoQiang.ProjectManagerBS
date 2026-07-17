import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

import { RuntimeDataTableCellEditor } from './data-table/RuntimeDataTableCellEditor';
import {
  isRuntimeDataTableColumnEditable,
  readRuntimeDataTableEditingOptions,
  type RuntimeDataTableColumnDefinition,
  type RuntimeDataTableCommit,
  type RuntimeDataTableCommitRequest
} from './data-table/runtimeDataTableEditing';
import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';
import type { RuntimeActionFlow } from './RuntimeTypes';

export function hasDataTableRuntimeRenderer(type: string): boolean {
  return type === 'report.dataTable';
}

export function renderDataTableRuntime(context: RuntimeComponentRenderContext): ReactNode {
  return <RuntimeDataTable context={context} />;
}

function RuntimeDataTable({ context }: { context: RuntimeComponentRenderContext }) {
  const sourceRows = useMemo(() => readRows(context), [context]);
  const columns = useMemo(() => readColumns(context.props.columns), [context.props.columns]);
  const editingOptions = useMemo(() => readRuntimeDataTableEditingOptions(context.props), [context.props]);
  const [rows, setRows] = useState(sourceRows);
  const [page, setPage] = useState(0);
  const [pendingAction, setPendingAction] = useState<{ item: RuntimeDataTableAction; row: Record<string, unknown> } | null>(null);
  const rowsRef = useRef(rows);
  const rowsFingerprintRef = useRef(serializeRows(sourceRows));

  useEffect(() => {
    const fingerprint = serializeRows(sourceRows);
    if (fingerprint !== rowsFingerprintRef.current) {
      rowsFingerprintRef.current = fingerprint;
      rowsRef.current = sourceRows;
      setRows(sourceRows);
    }
  }, [sourceRows]);

  useEffect(() => {
    if (!pendingAction) return;
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setPendingAction(null);
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [pendingAction]);

  const pageSize = normalizePageSize(context.props.pageSize);
  const pageCount = Math.max(1, Math.ceil(rows.length / pageSize));
  const currentPage = Math.min(page, pageCount - 1);
  const visibleRows = rows.slice(currentPage * pageSize, (currentPage + 1) * pageSize);
  const actions = readActions(context.props.rowActions);
  const executeRowAction = async (item: RuntimeDataTableAction, row: Record<string, unknown>): Promise<void> => {
    if (item.requiresConfirmation) {
      setPendingAction({ item, row });
      return;
    }

    await context.executeAction(item.action, { ...context.runtime, variables: { ...context.runtime.variables, currentRow: row } });
  };
  const confirmPendingAction = async (): Promise<void> => {
    if (!pendingAction) return;
    const action = pendingAction.item.action;
    const row = pendingAction.row;
    setPendingAction(null);
    await context.executeAction(action, { ...context.runtime, variables: { ...context.runtime.variables, currentRow: row } });
  };

  const commitCell: RuntimeDataTableCommit = async (request: RuntimeDataTableCommitRequest) => {
    if (context.changeAction) {
      await executeTableChangeAction(context, request);
    }
    const nextRows = rowsRef.current.map((row, index) => index === request.rowIndex ? request.nextRow : row);
    rowsFingerprintRef.current = serializeRows(nextRows);
    rowsRef.current = nextRows;
    setRows(nextRows);
    context.onChange(nextRows);
    return { row: request.nextRow, value: request.value };
  };

  return applyRuntimeNodePresentation(context, <div className="overflow-hidden rounded border border-slate-200 bg-white">
    {context.loading ? <div className="px-3 py-2 text-xs text-slate-500" role="status">{String(context.props.loadingState ?? 'Loading…')}</div> : null}
    {context.props.title ? <div className="border-b border-slate-200 px-3 py-2 text-sm font-semibold text-slate-700">{String(context.props.title)}</div> : null}
    <div className="runtime-data-table-scroll overflow-x-auto"><table className="runtime-data-table min-w-full border-collapse text-sm"><thead className="bg-slate-50 text-xs font-semibold text-slate-600"><tr>{columns.map((column) => <th className="border-b border-slate-200 px-3 py-2 text-left" key={column.fieldCode}>{column.fieldName}</th>)}{actions.length > 0 ? <th className="border-b border-slate-200 px-3 py-2 text-left">操作</th> : null}</tr></thead><tbody>{visibleRows.length === 0 ? <tr><td className="px-3 py-6 text-center text-xs text-slate-400" colSpan={Math.max(1, columns.length + (actions.length > 0 ? 1 : 0))}>{String(context.props.emptyState ?? '暂无数据')}</td></tr> : visibleRows.map((row, rowIndex) => {
      const absoluteIndex = currentPage * pageSize + rowIndex;
      const key = rowKey(row, absoluteIndex);
      return <tr className="odd:bg-white even:bg-slate-50/60" key={key}>{columns.map((column) => <td className="border-b border-slate-100 px-3 py-2 text-slate-700" key={column.fieldCode}>{isRuntimeDataTableColumnEditable(column, row, editingOptions, context.disabled, context.readOnly, context.props)
        ? <RuntimeDataTableCellEditor ariaLabel={`${column.fieldName} row ${absoluteIndex + 1}`} column={column} commitOnBlur={editingOptions.commitOnBlur} disabled={context.disabled || context.readOnly} onCommit={commitCell} row={row} rowIndex={absoluteIndex} rowKey={key} value={row[column.fieldCode]} />
        : formatValue(row[column.fieldCode])}</td>)}{actions.length > 0 ? <td className="border-b border-slate-100 px-3 py-2"><div className="flex flex-wrap gap-1">{actions.map((item) => <button className="secondary-button h-7 px-2 text-xs" disabled={context.disabled || context.readOnly} key={item.action.id} type="button" onClick={() => void executeRowAction(item, row)}>{item.label}</button>)}</div></td> : null}</tr>;
    })}</tbody></table></div>
    {pageCount > 1 ? <div className="flex items-center justify-between border-t border-slate-200 px-3 py-2 text-xs text-slate-500"><span>第 {currentPage + 1} / {pageCount} 页</span><div className="flex gap-1"><button className="secondary-button h-7 px-2 text-xs" disabled={currentPage === 0 || context.disabled} type="button" onClick={() => setPage((value) => Math.max(0, value - 1))}>上一页</button><button className="secondary-button h-7 px-2 text-xs" disabled={currentPage === pageCount - 1 || context.disabled} type="button" onClick={() => setPage((value) => Math.min(pageCount - 1, value + 1))}>下一页</button></div></div> : null}
     {pendingAction ? <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/40 p-4 backdrop-blur-[2px]" data-runtime-confirmation-modal="true"><div aria-describedby="runtime-confirmation-description" aria-labelledby="runtime-confirmation-title" aria-modal="true" className="w-full max-w-md rounded-xl border border-slate-200 bg-white shadow-2xl" role="dialog"><div className="border-b border-slate-200 px-5 py-4"><p className="text-xs font-medium uppercase tracking-wide text-slate-500">{pendingAction.item.label === '删除' ? '确认删除' : '确认操作'}</p><h2 className="mt-1 text-base font-semibold text-slate-900" id="runtime-confirmation-title">{pendingAction.item.label}</h2></div><div className="px-5 py-5 text-sm leading-6 text-slate-600" id="runtime-confirmation-description">{pendingAction.item.confirmationMessage}</div><div className="flex justify-end gap-2 border-t border-slate-200 bg-slate-50 px-5 py-3"><button className="secondary-button h-8 px-4 text-xs" type="button" onClick={() => setPendingAction(null)}>取消</button><button className="primary-button h-8 px-4 text-xs" type="button" onClick={() => void confirmPendingAction()}>确认</button></div></div></div> : null}
   </div>);
}

interface RuntimeDataTableAction { action: RuntimeActionFlow; confirmationMessage: string; label: string; requiresConfirmation: boolean; }

async function executeTableChangeAction(context: RuntimeComponentRenderContext, request: RuntimeDataTableCommitRequest): Promise<void> {
  if (!context.changeAction) return;
  await context.executeAction(context.changeAction, {
    ...context.runtime,
    variables: {
      ...context.runtime.variables,
      conflictResolution: request.conflictResolution ?? null,
      currentRow: request.nextRow,
      fieldCode: request.fieldCode,
      localRow: request.localRow,
      originalRow: request.originalRow,
      originalValue: request.originalValue,
      row: request.nextRow,
      value: request.value
    }
  });
}

function readRows(context: RuntimeComponentRenderContext): Array<Record<string, unknown>> {
  return readRowsFromValue(context.value ?? context.bindings?.data ?? context.props.rows);
}

function readRowsFromValue(value: unknown): Array<Record<string, unknown>> {
  if (typeof value === 'string') {
    try {
      return readRowsFromValue(JSON.parse(value));
    } catch {
      return [];
    }
  }
  return Array.isArray(value)
    ? value.map((item) => item && typeof item === 'object' && !Array.isArray(item) ? item as Record<string, unknown> : { value: item })
    : [];
}

function readColumns(value: unknown): RuntimeDataTableColumnDefinition[] {
  const parsed = typeof value === 'string' ? parseJson(value) : value;
  return Array.isArray(parsed) ? parsed.map((item, index) => {
    const record = item && typeof item === 'object' && !Array.isArray(item) ? item as Record<string, unknown> : {};
    const validation = record.validation && typeof record.validation === 'object' && !Array.isArray(record.validation) ? record.validation as Record<string, unknown> : undefined;
    const fieldCode = String(record.fieldCode ?? record.key ?? record.field ?? `column${index + 1}`);
    return {
      dataType: typeof record.dataType === 'string' ? record.dataType : undefined,
      editable: record.editable !== false,
      editorType: typeof record.editorType === 'string' ? record.editorType : undefined,
      fieldCode,
      fieldName: String(record.fieldName ?? record.title ?? fieldCode),
      permissionDenied: record.permissionDenied === true,
      primaryKey: record.primaryKey === true,
      readOnly: record.readOnly === true,
      required: record.required === true,
      validation,
      writable: record.writable !== false
    };
  }) : [];
}

function readActions(value: unknown): RuntimeDataTableAction[] {
  const parsed = typeof value === 'string' ? parseJson(value) : value;
  return Array.isArray(parsed) ? parsed.map((item) => {
    const record = item && typeof item === 'object' && !Array.isArray(item) ? item as Record<string, unknown> : {};
    const action = record.action && typeof record.action === 'object' ? record.action as RuntimeActionFlow : null;
    return action && typeof action.id === 'string' && Array.isArray(action.steps) ? {
      action,
      confirmationMessage: String(record.confirmationMessage ?? '确定执行此操作吗？'),
      label: String(record.label ?? action.name ?? action.id),
      requiresConfirmation: record.requiresConfirmation === true
    } : null;
  }).filter((item): item is RuntimeDataTableAction => item !== null) : [];
}

function normalizePageSize(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0 ? Math.min(200, Math.floor(value)) : 20;
}

function rowKey(row: Record<string, unknown>, index: number): string {
  return String(row.id ?? row.key ?? row.code ?? index);
}

function formatValue(value: unknown): string {
  return value === null || value === undefined ? '' : typeof value === 'object' ? JSON.stringify(value) : String(value);
}

function parseJson(value: string): unknown {
  try {
    return JSON.parse(value);
  } catch {
    return [];
  }
}

function serializeRows(rows: Array<Record<string, unknown>>): string {
  try {
    return JSON.stringify(rows);
  } catch {
    return String(rows.length);
  }
}
