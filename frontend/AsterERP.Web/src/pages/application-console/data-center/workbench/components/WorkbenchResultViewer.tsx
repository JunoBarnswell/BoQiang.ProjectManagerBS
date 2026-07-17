import { ChevronDown, ChevronRight, Clock, Database, Hash, XCircle } from 'lucide-react';
import { Fragment, useState } from 'react';

import type {
  ApplicationDataCenterPreviewField,
  ApplicationDataCenterPreviewPage,
  ApplicationDataCenterPreviewResponse
} from '../../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../../core/i18n/I18nProvider';


interface WorkbenchResultViewerProps {
  onPageChange?: (pageIndex: number) => void;
  preview?: ApplicationDataCenterPreviewResponse | null;
}

type ComplexKind = 'object' | 'array' | 'arrayObject';

const maxNestedDepth = 3;

export function WorkbenchResultViewer({ onPageChange, preview }: WorkbenchResultViewerProps) {
  const [expandedCells, setExpandedCells] = useState<Set<string>>(() => new Set());

  if (!preview) {
    return <div className="rounded-md border border-dashed border-slate-200 px-4 py-8 text-center text-sm text-slate-500">{translateCurrentLiteral("暂无操作结果")}</div>;
  }

  const toggleCell = (key: string) => {
    setExpandedCells((current) => {
      const next = new Set(current);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      return next;
    });
  };

  const tableFields = preview.fields;

  return (
    <div className="workbench-result-viewer rounded-md border border-slate-200 bg-white shadow-sm">
      <ResultSummary preview={preview} />
      {preview.fields.length === 0 ? (
        <div className="px-4 py-6 text-sm text-slate-500">{translateCurrentLiteral("执行成功，未返回字段。")}</div>
      ) : (
        <div className="workbench-result-viewer__scroll overflow-auto">
          <table className="workbench-result-viewer__table w-full text-left text-sm" style={{ minWidth: Math.max(720, tableFields.length * 156) }}>
            <thead className="bg-white text-xs text-slate-500">
              <tr>
                {tableFields.map((field) => (
                  <th className="border-b border-slate-200 px-3 py-2 font-medium whitespace-nowrap" key={field.fieldCode}>
                    {field.fieldName || field.fieldCode}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {preview.rows.length === 0 ? (
                <tr>
                  <td className="px-3 py-6 text-center text-sm text-slate-500" colSpan={Math.max(1, tableFields.length)}>{translateCurrentLiteral("无返回数据")}</td>
                </tr>
              ) : preview.rows.map((row, rowIndex) => {
                const rowKey = `${preview.page?.pageIndex ?? 1}:${rowIndex}`;
                const expandedValues = preview.fields
                  .map((field) => ({ field, key: `${rowKey}:${field.fieldCode}`, value: normalizeJsonLikeValue(row[field.fieldCode]) }))
                  .filter((item) => expandedCells.has(item.key) && resolveComplexKind(item.field, item.value));

                return (
                  <Fragment key={rowKey}>
                    <tr className="border-b border-slate-100 last:border-b-0 hover:bg-slate-50" key={rowKey}>
                      {tableFields.map((field) => {
                        const value = normalizeJsonLikeValue(row[field.fieldCode]);
                        const cellKey = `${rowKey}:${field.fieldCode}`;
                        const complexKind = resolveComplexKind(field, value);
                        return (
                          <td className="max-w-[280px] px-3 py-2 align-middle text-slate-700" key={field.fieldCode}>
                            {complexKind ? (
                              <ComplexCellButton
                                expanded={expandedCells.has(cellKey)}
                                kind={complexKind}
                                value={value}
                                onClick={() => toggleCell(cellKey)}
                              />
                            ) : (
                              <ScalarCell value={value} />
                            )}
                          </td>
                        );
                      })}
                    </tr>
                    {expandedValues.length > 0 ? (
                      <tr className="bg-slate-50" key={`${rowKey}:expanded`}>
                        <td className="border-b border-slate-100 px-3 py-3" colSpan={Math.max(1, tableFields.length)}>
                          <div className="grid gap-3">
                            {expandedValues.map(({ field, value }) => (
                              <NestedValuePanel field={field} key={field.fieldCode} value={value} depth={1} />
                            ))}
                          </div>
                        </td>
                      </tr>
                    ) : null}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <ResultFooter page={preview.page ?? null} onPageChange={onPageChange} />
    </div>
  );
}

function ResultSummary({ preview }: { preview: ApplicationDataCenterPreviewResponse }) {
  const rowCount = preview.page?.totalRows ?? preview.rows.length;
  return (
    <div className="workbench-result-viewer__summary border-b border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-medium">{preview.message || '查询结果'}</span>
        <span className="rounded-full bg-white px-2 py-0.5 text-xs text-slate-500">{rowCount} 行</span>
        {preview.audit ? (
          <>
            <span className="inline-flex items-center gap-1 rounded-full bg-white px-2 py-0.5 text-xs text-slate-500">
              <Hash className="h-3 w-3" />
              {preview.audit.traceId}
            </span>
            <span className="inline-flex items-center gap-1 rounded-full bg-white px-2 py-0.5 text-xs text-slate-500">
              <Clock className="h-3 w-3" />
              {preview.audit.durationMs}ms
            </span>
            <span className="inline-flex items-center gap-1 rounded-full bg-white px-2 py-0.5 text-xs text-slate-500">
              <Database className="h-3 w-3" />
              返回 {preview.audit.returnedRows} / 影响 {preview.audit.affectedRows ?? 0}
            </span>
          </>
        ) : null}
      </div>
      {preview.audit?.errorMessage ? (
        <div className="mt-2 inline-flex items-center gap-1 text-xs text-red-600">
          <XCircle className="h-3.5 w-3.5" />
          {preview.audit.errorMessage}
        </div>
      ) : null}
    </div>
  );
}

function ResultFooter({ onPageChange, page }: { onPageChange?: (pageIndex: number) => void; page: ApplicationDataCenterPreviewPage | null }) {
  if (!page || !onPageChange) {
    return null;
  }

  return (
    <div className="flex items-center justify-between border-t border-slate-200 bg-white px-3 py-2 text-xs text-slate-500">
      <span>第 {page.pageIndex} 页 / 每页 {page.pageSize} 行 / 共 {page.totalRows} 行</span>
      <div className="flex items-center gap-2">
        <button
          className="secondary-button h-7 text-xs"
          disabled={!page.hasPrevious}
          type="button"
          onClick={() => onPageChange(page.pageIndex - 1)}
        >{translateCurrentLiteral("上一页")}</button>
        <button
          className="secondary-button h-7 text-xs"
          disabled={!page.hasNext}
          type="button"
          onClick={() => onPageChange(page.pageIndex + 1)}
        >{translateCurrentLiteral("下一页")}</button>
      </div>
    </div>
  );
}

function ComplexCellButton({
  expanded,
  kind,
  value,
  onClick
}: {
  expanded: boolean;
  kind: ComplexKind;
  value: unknown;
  onClick: () => void;
}) {
  return (
    <button
      className="inline-flex max-w-full items-center gap-1 rounded-md border border-slate-200 bg-white px-2 py-1 text-xs font-medium text-slate-700 hover:border-blue-300 hover:text-blue-700"
      type="button"
      onClick={onClick}
    >
      {expanded ? <ChevronDown className="h-3.5 w-3.5" /> : <ChevronRight className="h-3.5 w-3.5" />}
      <span>{formatComplexLabel(kind, value)}</span>
    </button>
  );
}

function ScalarCell({ value }: { value: unknown }) {
  const cellText = formatScalar(value);
  return <span className="block overflow-hidden text-ellipsis whitespace-nowrap" title={cellText}>{cellText}</span>;
}

function NestedValuePanel({
  depth,
  field,
  value
}: {
  depth: number;
  field: ApplicationDataCenterPreviewField;
  value: unknown;
}) {
  const normalizedValue = normalizeJsonLikeValue(value);
  const kind = resolveComplexKind(field, normalizedValue);
  if (!kind) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-3">
        <div className="mb-2 text-xs font-semibold text-slate-600">{field.fieldName || field.fieldCode}</div>
        <ScalarCell value={normalizedValue} />
      </div>
    );
  }

  if (depth > maxNestedDepth) {
    return (
      <details className="rounded-md border border-slate-200 bg-white p-3">
        <summary className="cursor-pointer text-xs font-semibold text-slate-600">{field.fieldName || field.fieldCode} JSON 详情</summary>
        <pre className="mt-2 max-h-72 overflow-auto rounded bg-slate-950 p-3 text-xs text-slate-100">{safeStringify(normalizedValue)}</pre>
      </details>
    );
  }

  if (Array.isArray(normalizedValue)) {
    return <ArrayValuePanel depth={depth} field={field} values={normalizedValue} />;
  }

  return <ObjectValuePanel depth={depth} field={field} value={normalizedValue as Record<string, unknown>} />;
}

function ObjectValuePanel({
  depth,
  field,
  value
}: {
  depth: number;
  field: ApplicationDataCenterPreviewField;
  value: Record<string, unknown>;
}) {
  const entries = Object.entries(value);
  return (
    <div className="rounded-md border border-slate-200 bg-white">
      <div className="border-b border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600">
        {field.fieldName || field.fieldCode} · 对象
      </div>
      <div className="grid gap-2 p-3">
        {entries.length === 0 ? <div className="text-xs text-slate-500">{translateCurrentLiteral("空对象")}</div> : entries.map(([key, childValue]) => {
          const normalizedChild = normalizeJsonLikeValue(childValue);
          const childField = createChildField(key, normalizedChild, field);
          const childKind = resolveComplexKind(childField, normalizedChild);
          return (
            <div className="grid gap-1 text-xs" key={key}>
              <div className="font-medium text-slate-500">{key}</div>
              {childKind ? (
                <NestedValuePanel depth={depth + 1} field={childField} value={normalizedChild} />
              ) : (
                <div className="rounded border border-slate-100 bg-slate-50 px-2 py-1 text-slate-700">{formatScalar(normalizedChild)}</div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}

function ArrayValuePanel({
  depth,
  field,
  values
}: {
  depth: number;
  field: ApplicationDataCenterPreviewField;
  values: unknown[];
}) {
  const rowValues = values.map((item) => normalizeJsonLikeValue(item));
  const tableFields = inferFieldsFromArray(field, rowValues);

  return (
    <div className="rounded-md border border-slate-200 bg-white">
      <div className="border-b border-slate-200 px-3 py-2 text-xs font-semibold text-slate-600">
        {field.fieldName || field.fieldCode} · 数组({values.length})
      </div>
      {rowValues.length === 0 ? (
        <div className="p-3 text-xs text-slate-500">{translateCurrentLiteral("空数组")}</div>
      ) : tableFields.length === 0 ? (
        <div className="grid gap-1 p-3">
          {rowValues.map((item, index) => <ScalarCell key={index} value={item} />)}
        </div>
      ) : (
        <div className="overflow-auto">
          <table className="w-full min-w-[520px] text-left text-xs">
            <thead className="bg-slate-50 text-slate-500">
              <tr>
                {tableFields.map((column) => <th className="border-b border-slate-200 px-2 py-1.5 font-medium" key={column.fieldCode}>{column.fieldName || column.fieldCode}</th>)}
              </tr>
            </thead>
            <tbody>
              {rowValues.map((row, rowIndex) => {
                if (!isPlainObject(row)) {
                  return (
                    <tr className="border-b border-slate-100 last:border-b-0" key={rowIndex}>
                      <td className="px-2 py-1.5 text-slate-700" colSpan={tableFields.length}>{formatScalar(row)}</td>
                    </tr>
                  );
                }

                return (
                  <tr className="border-b border-slate-100 last:border-b-0" key={rowIndex}>
                    {tableFields.map((column) => {
                      const cellValue = normalizeJsonLikeValue((row as Record<string, unknown>)[column.fieldCode]);
                      const childField = createChildField(column.fieldCode, cellValue, field);
                      const childKind = resolveComplexKind(childField, cellValue);
                      return (
                        <td className="max-w-[220px] px-2 py-1.5 align-top text-slate-700" key={column.fieldCode}>
                          {childKind ? (
                            <NestedValuePanel depth={depth + 1} field={childField} value={cellValue} />
                          ) : (
                            <ScalarCell value={cellValue} />
                          )}
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function resolveComplexKind(field: ApplicationDataCenterPreviewField, value: unknown): ComplexKind | null {
  const kind = field.valueKind;
  if (kind === 'object' || kind === 'array' || kind === 'arrayObject') {
    return kind;
  }

  if (Array.isArray(value)) {
    return value.some((item) => isPlainObject(normalizeJsonLikeValue(item))) ? 'arrayObject' : 'array';
  }

  return isPlainObject(value) ? 'object' : null;
}

function formatComplexLabel(kind: ComplexKind, value: unknown) {
  if (kind === 'object' && isPlainObject(value)) {
    return `对象(${Object.keys(value).length})`;
  }

  if (Array.isArray(value)) {
    return kind === 'arrayObject' ? `数组对象(${value.length})` : `数组(${value.length})`;
  }

  return kind === 'object' ? '对象' : '数组';
}

function formatScalar(value: unknown) {
  if (value === null || value === undefined || value === '') {
    return '-';
  }

  if (typeof value === 'boolean') {
    return value ? 'true' : 'false';
  }

  if (typeof value === 'object') {
    return safeStringify(value);
  }

  return String(value);
}

function inferFieldsFromArray(parent: ApplicationDataCenterPreviewField, values: unknown[]): ApplicationDataCenterPreviewField[] {
  if (parent.children && parent.children.length > 0) {
    return parent.children;
  }

  const keys: string[] = [];
  values.forEach((item) => {
    if (!isPlainObject(item)) {
      return;
    }

    Object.keys(item).forEach((key) => {
      if (!keys.includes(key)) {
        keys.push(key);
      }
    });
  });

  return keys.map((key, index) => createPreviewField(key, index));
}

function createChildField(fieldCode: string, value: unknown, parent: ApplicationDataCenterPreviewField): ApplicationDataCenterPreviewField {
  const child = parent.children?.find((item) => item.fieldCode === fieldCode);
  return child ?? createPreviewField(fieldCode, 0, inferValueKind(value));
}

function createPreviewField(fieldCode: string, order: number, valueKind: ApplicationDataCenterPreviewField['valueKind'] = 'scalar'): ApplicationDataCenterPreviewField {
  return {
    dataType: valueKind === 'scalar' ? 'string' : String(valueKind),
    fieldCode,
    fieldName: fieldCode,
    nullable: true,
    order,
    primaryKey: false,
    valueKind
  };
}

function inferValueKind(value: unknown): ApplicationDataCenterPreviewField['valueKind'] {
  if (Array.isArray(value)) {
    return value.some((item) => isPlainObject(normalizeJsonLikeValue(item))) ? 'arrayObject' : 'array';
  }

  return isPlainObject(value) ? 'object' : 'scalar';
}

function normalizeJsonLikeValue(value: unknown): unknown {
  if (typeof value !== 'string') {
    return value;
  }

  const text = value.trim();
  if (!((text.startsWith('{') && text.endsWith('}')) || (text.startsWith('[') && text.endsWith(']')))) {
    return value;
  }

  try {
    return JSON.parse(text);
  } catch {
    return value;
  }
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

function safeStringify(value: unknown) {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}
