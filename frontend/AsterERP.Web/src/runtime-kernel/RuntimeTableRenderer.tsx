import type { ReactNode } from 'react';

import type { RuntimeComponentRenderContext } from './RuntimeComponentTypes';
import { applyRuntimeNodePresentation } from './RuntimeNodePresentation';

interface TableColumn {
  key: string;
  title: string;
}

export function hasTableRuntimeRenderer(type: string): boolean {
  return type === 'table.semantic';
}

export function renderTableRuntime(context: RuntimeComponentRenderContext): ReactNode {
  const columns = normalizeColumns(context.props.columns);
  const rows = normalizeRows(context.bindings?.data ?? context.props.rows);
  const error = context.props.error ?? context.bindings?.error;
  return applyRuntimeNodePresentation(context, <div className="overflow-hidden rounded border border-slate-200 bg-white">
    {context.loading ? <div className="px-3 py-2 text-xs text-slate-500" role="status">{String(context.props.loadingState ?? 'Loading')}</div> : null}
    {error ? <div className="px-3 py-2 text-xs text-red-600" role="alert">{String(context.props.errorState ?? error)}</div> : null}
    <table aria-label={String(context.props.caption ?? context.title)} className="min-w-full border-collapse text-sm">
      <caption className="caption-top px-3 py-2 text-left text-xs font-semibold text-slate-600">{String(context.props.caption ?? context.title)}</caption>
      <thead className="bg-slate-50 text-xs font-semibold text-slate-600">
        <tr>{columns.map((column) => <th className="border-b border-slate-200 px-3 py-2 text-left" key={column.key} scope="col">{column.title}</th>)}</tr>
      </thead>
      <tbody>
        {rows.length > 0
          ? rows.map((row, rowIndex) => <tr className="odd:bg-white even:bg-slate-50/60" key={rowIndex}>{columns.map((column) => <td className="border-b border-slate-100 px-3 py-2 text-slate-700" key={column.key}>{String(row[column.key] ?? '')}</td>)}</tr>)
          : <tr><td className="px-3 py-6 text-center text-xs text-slate-400" colSpan={Math.max(columns.length, 1)}>{String(context.props.emptyState ?? 'No data')}</td></tr>}
      </tbody>
    </table>
  </div>);
}

function normalizeColumns(value: unknown): TableColumn[] {
  const parsed = parseMaybeJson(value);
  if (!Array.isArray(parsed)) return [{ key: 'name', title: 'Name' }, { key: 'value', title: 'Value' }];
  return parsed.map((item, index) => {
    const record = item && typeof item === 'object' && !Array.isArray(item) ? item as Record<string, unknown> : {};
    const key = String(record.key ?? record.field ?? record.fieldCode ?? `column${index + 1}`).trim();
    return { key, title: String(record.title ?? record.label ?? record.fieldName ?? key) };
  }).filter((column) => column.key);
}

function normalizeRows(value: unknown): Array<Record<string, unknown>> {
  const parsed = parseMaybeJson(value);
  return Array.isArray(parsed) ? parsed.map((item) => item && typeof item === 'object' && !Array.isArray(item) ? item as Record<string, unknown> : { value: item }) : [];
}

function parseMaybeJson(value: unknown): unknown {
  if (typeof value !== 'string') return value;
  try { return JSON.parse(value); } catch { return value; }
}
