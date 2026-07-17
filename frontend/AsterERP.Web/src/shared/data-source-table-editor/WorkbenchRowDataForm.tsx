import { useState } from 'react';

import type { ApplicationDataSourceColumn } from '../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../core/i18n/I18nProvider';

import { TypedValueInput } from './TypedValueInput';
import { parseTypedValue } from './TypedValueParser';

interface WorkbenchRowDataFormProps {
  columns: ApplicationDataSourceColumn[];
  mode: 'create' | 'edit';
  primaryKeys: string[];
  values: Record<string, unknown>;
  onChange: (values: Record<string, unknown>) => void;
}
export function WorkbenchRowDataForm({ columns, mode, primaryKeys, values, onChange }: WorkbenchRowDataFormProps) {
  const keySet = new Set(primaryKeys.map((key) => key.toLowerCase()));
  const [errors, setErrors] = useState<Record<string, string>>({});

  return (
    <div className="space-y-3">
      <div className="rounded-md border border-primary-100 bg-primary-50/70 px-3 py-2 text-xs text-primary-900">
        <div className="font-semibold">{mode === 'edit' ? '编辑行数据' : '新增行数据'}</div>
        <div className="mt-0.5 text-primary-700">
          {mode === 'edit' ? '按主键定位当前记录，主键字段在编辑时锁定，避免保存时定位漂移。' : '按当前表结构录入字段值，保存后会写入当前数据库。'}
        </div>
      </div>
      <div className="grid gap-2">
        {columns.map((column) => {
          const isPrimary = keySet.has(column.columnName.toLowerCase());
          const locked = mode === 'edit' && isPrimary;
          const value = toInputValue(values[column.columnName]);
          return (
            <label className="rounded-md border border-slate-200 bg-white p-3 shadow-sm transition focus-within:border-primary-300 focus-within:ring-2 focus-within:ring-primary-100" key={column.columnName}>
              <span className="flex items-start justify-between gap-3">
                <span className="min-w-0">
                  <span className="block text-xs font-semibold text-slate-900">{column.columnName}</span>
                  <span className="mt-1 flex flex-wrap items-center gap-1.5 text-[11px] text-slate-500">
                    <span>{column.dataType}</span>
                    <span>{column.nullable ? '可为空' : '必填'}</span>
                    {isPrimary ? <span className="rounded bg-primary-50 px-2 py-0.5 font-medium text-primary-700">{translateCurrentLiteral("主键")}</span> : null}
                  </span>
                </span>
              </span>
              <TypedValueInput
                ariaLabel={column.columnName}
                className="form-input mt-2 h-8 text-xs"
                dataType={column.dataType}
                disabled={locked}
                value={value}
                onChange={(nextValue) => {
                  const parsed = parseTypedValue(nextValue, column.dataType);
                  if (parsed.ok) {
                    setErrors((current) => { const next = { ...current }; delete next[column.columnName]; return next; });
                    onChange({ ...values, [column.columnName]: parsed.value });
                  } else {
                    setErrors((current) => ({ ...current, [column.columnName]: parsed.error }));
                  }
                }}
                onValidationError={(message) => setErrors((current) => ({ ...current, [column.columnName]: message }))}
              />
              {errors[column.columnName] ? <span className="mt-1 block text-xs text-red-600" role="alert">{errors[column.columnName]}</span> : null}
              {locked ? <span className="mt-2 block text-xs text-slate-400">{translateCurrentLiteral("主键用于定位当前行，编辑时不可修改。")}</span> : null}
            </label>
          );
        })}
      </div>
    </div>
  );
}

function toInputValue(value: unknown): string {
  if (value === null || value === undefined) return '';
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}
