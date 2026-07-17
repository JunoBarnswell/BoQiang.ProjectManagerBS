import { useVirtualizer } from '@tanstack/react-virtual';
import { Copy, Download, Pin, PinOff } from 'lucide-react';
import { useCallback, useEffect, useRef, useState, type CSSProperties } from 'react';

import type { ApplicationDataCenterPreviewField } from '../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';
import { PermissionButton } from '../auth/PermissionButton';

import { TypedValueInput } from './TypedValueInput';
import { parseTypedValue } from './TypedValueParser';

interface EditingCell {
  fieldCode: string;
  rowIndex: number;
}

interface WorkbenchDataGridProps {
  editable: boolean;
  editDisabledReason?: string | null;
  fields: ApplicationDataCenterPreviewField[];
  primaryKeys: string[];
  rows: Array<Record<string, unknown>>;
  savingCell?: EditingCell | null;
  onCellCommit?: (row: Record<string, unknown>, fieldCode: string, value: unknown) => void;
  onDelete?: (row: Record<string, unknown>) => void;
  onEdit?: (row: Record<string, unknown>) => void;
  onExport?: () => void;
}

export function WorkbenchDataGrid({ editable, editDisabledReason, fields, primaryKeys, rows, savingCell, onCellCommit, onDelete, onEdit, onExport }: WorkbenchDataGridProps) {
  const [editingCell, setEditingCell] = useState<EditingCell | null>(null);
  const [draftValue, setDraftValue] = useState('');
  const [columnWidths, setColumnWidths] = useState<Record<string, number>>({});
  const [pinnedFields, setPinnedFields] = useState<Set<string>>(new Set());
  const [copyStatus, setCopyStatus] = useState('');
  const rowScrollRef = useRef<HTMLDivElement>(null);
  const virtualizer = useVirtualizer({ count: rows.length, getScrollElement: () => rowScrollRef.current, estimateSize: () => 38, overscan: 6 });
  const virtualRows = virtualizer.getVirtualItems();
  const primaryKeySet = new Set(primaryKeys.map((key) => key.toLowerCase()));

  if (rows.length === 0) {
    return <div className="rounded-md border border-dashed border-slate-200 px-4 py-8 text-center text-sm text-slate-500">{translateCurrentLiteral("暂无数据")}</div>;
  }

  return (
    <div className="overflow-hidden rounded-md border border-slate-200 bg-white">
      <div className="flex items-center justify-between border-b border-slate-200 bg-slate-50 px-2.5 py-1.5 text-xs text-slate-600">
        <span>{rows.length} 行数据 · 主键 {primaryKeys.length ? primaryKeys.join(', ') : '无'}</span>
        <div className="flex items-center gap-2">
          {copyStatus ? <span className="text-emerald-600" role="status">{copyStatus}</span> : null}
          <button className="secondary-button h-7 text-xs" type="button" onClick={() => void copyRows()}><Copy className="h-3.5 w-3.5" />{translateCurrentLiteral('复制当前页')}</button>
          <PermissionButton code="app:data-center:data-source:export" className="secondary-button h-7 text-xs" type="button" onClick={onExport ?? exportRows}><Download className="h-3.5 w-3.5" />{translateCurrentLiteral('导出')}</PermissionButton>
          {!editable && editDisabledReason ? <span className="text-xs text-amber-600">{editDisabledReason}</span> : null}
        </div>
      </div>
      <div ref={rowScrollRef} className="max-h-[520px] overflow-auto">
        <table className="min-w-full text-left text-xs">
          <thead className="bg-white text-xs text-slate-500">
            <tr>
              {fields.map((field) => {
                const pinned = pinnedFields.has(field.fieldCode);
                return <th className="relative border-b border-slate-200 px-2.5 py-1.5 font-medium" key={field.fieldCode} style={cellStyle(field.fieldCode, pinned)}>
                  <div className="flex items-center gap-1">
                    <span className="min-w-0 flex-1 truncate">{field.fieldName || field.fieldCode}</span>
                    <button className="shrink-0 text-slate-400 hover:text-primary-600" type="button" aria-label={`${translateCurrentLiteral(pinned ? '取消固定' : '固定')} ${field.fieldCode}`} onClick={() => togglePinned(field.fieldCode)}>{pinned ? <PinOff className="h-3.5 w-3.5" /> : <Pin className="h-3.5 w-3.5" />}</button>
                  </div>
                  <ColumnResizeHandle width={columnWidths[field.fieldCode] ?? 160} onChange={(width) => setColumnWidths((current) => ({ ...current, [field.fieldCode]: width }))} />
                </th>;
              })}
              <th className="border-b border-slate-200 px-2.5 py-1.5 font-medium">{translateCurrentLiteral("操作")}</th>
            </tr>
          </thead>
          <tbody>
            {virtualRows.length > 0 ? <tr aria-hidden="true"><td colSpan={fields.length + 1} style={{ height: virtualRows[0].start }} /></tr> : null}
            {virtualRows.map((virtualRow) => {
              const rowIndex = virtualRow.index;
              const row = rows[rowIndex];
              return <tr className="border-b border-slate-100 last:border-b-0" key={virtualRow.key}>
                {fields.map((field) => {
                  const primary = primaryKeySet.has(field.fieldCode.toLowerCase());
                  const editing = editingCell?.rowIndex === rowIndex && editingCell.fieldCode === field.fieldCode;
                  const saving = savingCell?.rowIndex === rowIndex && savingCell.fieldCode === field.fieldCode;
                  return (
                    <EditableCell
                      disabled={!editable || primary || saving}
                      draftValue={draftValue}
                      dataType={field.dataType}
                      editing={editing}
                      fieldCode={field.fieldCode}
                      key={field.fieldCode}
                      primary={primary}
                      saving={saving}
                      value={row[field.fieldCode]}
                      cellStyle={cellStyle(field.fieldCode, pinnedFields.has(field.fieldCode))}
                      onCancel={() => cancelEdit()}
                      onChange={setDraftValue}
                      onCommit={(value) => commitCell(row, field, value)}
                      onCopy={() => void copyValue(row[field.fieldCode])}
                      onStart={() => startEdit(rowIndex, field.fieldCode, row[field.fieldCode])}
                    />
                  );
                })}
                <td className="whitespace-nowrap px-2.5 py-1.5">
                  <button className="secondary-button h-7 text-xs" type="button" aria-label={translateCurrentLiteral('复制行')} onClick={() => void copyValue(row)}><Copy className="h-3.5 w-3.5" /></button>
                  <PermissionButton className="secondary-button h-7 text-xs" code="app:data-center:data-source:data-edit" disabled={!editable} type="button" onClick={() => onEdit?.(row)}>{translateCurrentLiteral("整行编辑")}</PermissionButton>
                  <PermissionButton className="danger-button ml-1.5 h-7 text-xs" code="app:data-center:data-source:data-edit" disabled={!editable} type="button" onClick={() => onDelete?.(row)}>{translateCurrentLiteral("删除")}</PermissionButton>
                </td>
              </tr>;
            })}
            {virtualRows.length > 0 ? <tr aria-hidden="true"><td colSpan={fields.length + 1} style={{ height: Math.max(0, virtualizer.getTotalSize() - virtualRows[virtualRows.length - 1].end) }} /></tr> : null}
          </tbody>
        </table>
      </div>
    </div>
  );

  function startEdit(rowIndex: number, fieldCode: string, value: unknown) {
    setEditingCell({ fieldCode, rowIndex });
    setDraftValue(toInputValue(value));
  }

  function cancelEdit() {
    setEditingCell(null);
    setDraftValue('');
  }

  function commitCell(row: Record<string, unknown>, field: ApplicationDataCenterPreviewField, value: string) {
    const fieldCode = field.fieldCode;
    const currentValue = toInputValue(row[fieldCode]);
    if (value === currentValue) {
      cancelEdit();
      return;
    }

    const parsed = parseTypedValue(value, field.dataType);
    if (!parsed.ok) {
      setCopyStatus(parsed.error);
      return;
    }
    onCellCommit?.(row, fieldCode, parsed.value);
    cancelEdit();
  }

  function togglePinned(fieldCode: string) {
    setPinnedFields((current) => {
      const next = new Set(current);
      if (next.has(fieldCode)) next.delete(fieldCode); else next.add(fieldCode);
      return next;
    });
  }

  function cellStyle(fieldCode: string, pinned: boolean): CSSProperties {
    const width = columnWidths[fieldCode] ?? 160;
    if (!pinned) return { width, minWidth: width, maxWidth: width };
    const left = fields.slice(0, fields.findIndex((field) => field.fieldCode === fieldCode)).filter((field) => pinnedFields.has(field.fieldCode)).reduce((total, field) => total + (columnWidths[field.fieldCode] ?? 160), 0);
    return { position: 'sticky', left, zIndex: 1, width, minWidth: width, maxWidth: width, background: 'white' };
  }

  async function copyRows() {
    await copyValue(rows.map((row) => fields.map((field) => row[field.fieldCode])));
  }

  async function copyValue(value: unknown) {
    const text = typeof value === 'string' ? value : JSON.stringify(value);
    try {
      await navigator.clipboard.writeText(text ?? '');
      setCopyStatus(translateCurrentLiteral('已复制'));
    } catch {
      setCopyStatus(translateCurrentLiteral('复制失败'));
    }
  }

  function exportRows() {
    const header = fields.map((field) => escapeCsv(field.fieldName || field.fieldCode)).join(',');
    const body = rows.map((row) => fields.map((field) => escapeCsv(row[field.fieldCode])).join(',')).join('\n');
    const blob = new Blob([`${header}\n${body}`], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'data-browser-page.csv';
    anchor.click();
    URL.revokeObjectURL(url);
  }
}

function escapeCsv(value: unknown) {
  const text = value === null || value === undefined ? '' : typeof value === 'object' ? JSON.stringify(value) : String(value);
  return /[",\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text;
}

function ColumnResizeHandle({ width, onChange }: { width: number; onChange: (width: number) => void }) {
  const start = useRef<{ x: number; width: number } | null>(null);
  return <span className="absolute right-0 top-0 h-full w-1 cursor-col-resize touch-none" onPointerDown={(event) => { start.current = { x: event.clientX, width }; event.currentTarget.setPointerCapture(event.pointerId); }} onPointerMove={(event) => { if (start.current) onChange(Math.min(520, Math.max(100, start.current.width + event.clientX - start.current.x))); }} onPointerUp={(event) => { start.current = null; event.currentTarget.releasePointerCapture(event.pointerId); }} />;
}

function formatCell(value: unknown) {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  return typeof value === 'object' ? '结构化值' : String(value);
}

function toInputValue(value: unknown) {
  if (value === null || value === undefined) {
    return '';
  }

  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}

function EditableCell({
  dataType,
  disabled,
  draftValue,
  editing,
  fieldCode,
  primary,
  saving,
  value,
  cellStyle,
  onCancel,
  onChange,
  onCommit,
  onCopy,
  onStart
}: {
  dataType: string;
  disabled: boolean;
  draftValue: string;
  editing: boolean;
  fieldCode: string;
  primary: boolean;
  saving: boolean;
  value: unknown;
  cellStyle: CSSProperties;
  onCancel: () => void;
  onChange: (value: string) => void;
  onCommit: (value: string) => void;
  onCopy: () => void;
  onStart: () => void;
}) {
  const committedRef = useRef(false);

  const commitOnce = useCallback((nextValue: string) => {
    if (committedRef.current) {
      return;
    }

    committedRef.current = true;
    onCommit(nextValue);
  }, [onCommit]);

  useEffect(() => {
    if (editing) committedRef.current = false;
  }, [editing]);

  if (editing) {
    return (
      <td className="min-w-[150px] px-1.5 py-1">
        <form
          onSubmit={(event) => {
            event.preventDefault();
            commitOnce(draftValue);
          }}
        >
          <TypedValueInput
            className="form-input h-7 text-xs"
            dataType={dataType}
            value={draftValue}
            onBlur={() => commitOnce(draftValue)}
            onChange={onChange}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                event.preventDefault();
                onCancel();
              }
            }}
          />
        </form>
      </td>
    );
  }

  return (
    <td className="max-w-[240px] px-2.5 py-1.5 text-slate-700" style={cellStyle}>
      <div className="flex items-center gap-1">
      <button
        className={[
          'group flex min-h-7 w-full items-center justify-between gap-2 rounded px-1.5 text-left transition',
          disabled ? 'cursor-default text-slate-700' : 'hover:bg-primary-50 hover:text-primary-700',
          saving ? 'bg-primary-50 text-primary-700' : ''
        ].join(' ')}
        disabled={disabled}
        title={primary ? '主键字段用于定位当前行，暂不支持直接编辑。' : `点击编辑 ${fieldCode}`}
        type="button"
        onClick={onStart}
      >
        <span className="truncate">{saving ? '保存中...' : formatCell(value)}</span>
        {!disabled ? <span className="shrink-0 text-xs text-slate-300 opacity-0 group-hover:opacity-100">{translateCurrentLiteral("编辑")}</span> : null}
      </button>
      <button className="shrink-0 text-slate-300 hover:text-primary-600" type="button" aria-label={`${translateCurrentLiteral('复制')} ${fieldCode}`} onClick={onCopy}><Copy className="h-3.5 w-3.5" /></button>
      </div>
    </td>
  );
}
