import type { ReactNode } from 'react';

import type { ApplicationDataCenterObjectUpsertRequest } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import type { DataSourceWorkspaceContext } from '../dataSourceWorkspaceTypes';
import { WorkbenchSqlEditor } from '../workbench/components/WorkbenchSqlEditor';

import { CodeRuleSegmentEditor } from './CodeRuleSegmentEditor';
import type { ConfigCondition, ConfigFieldSchema, ConfigFormSchema, ConfigFormMode } from './configFormTypes';
import { hasExistingSecret, parseJsonObject, resolveConfigValue, updateConfigField } from './configJsonCodec';
import { ConfigObjectSelect } from './ConfigObjectSelect';
import { KeyValueEditor } from './KeyValueEditor';
import type { KeyValueRow } from './KeyValueEditor';
import { MappingEditor } from './MappingEditor';
import { SensitiveFieldInput } from './SensitiveFieldInput';

interface ConfigFormRendererProps {
  dataSourceContext?: DataSourceWorkspaceContext | null;
  form: ApplicationDataCenterObjectUpsertRequest;
  mode: ConfigFormMode;
  publicConfigJson?: string | null;
  schema: ConfigFormSchema | null;
  onChange: (form: ApplicationDataCenterObjectUpsertRequest) => void;
}

export function ConfigFormRenderer({ dataSourceContext, form, mode, publicConfigJson, schema, onChange }: ConfigFormRendererProps) {
  if (!schema) {
    return (
      <section className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm leading-6 text-amber-800">
        当前类型还没有可视化配置 schema，请使用高级 JSON 模式维护，并确保保存前 JSON 合法。
      </section>
    );
  }

  const configValues = parseJsonObject(form.configJson).value;

  return (
    <div className="space-y-4">
      <div className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2">
        <div className="text-sm font-semibold text-slate-900">{schema.title}</div>
        <div className="mt-1 text-xs leading-5 text-slate-500">{schema.description}</div>
      </div>
      {schema.sections.map((section) => {
        const visibleFields = section.fields.filter((field) => isVisible(field.visibleWhen, configValues));
        if (visibleFields.length === 0) {
          return null;
        }

        return (
          <section key={section.key} className="rounded-md border border-slate-200 bg-white p-3">
            <div className="mb-3">
              <div className="text-sm font-semibold text-slate-900">{section.title}</div>
              {section.description ? <div className="mt-1 text-xs leading-5 text-slate-500">{section.description}</div> : null}
            </div>
            <div className="grid grid-cols-2 gap-3">
              {visibleFields.map((field) => (
                <FieldShell key={field.name} field={field}>
                  {renderField({
                    disabled: mode === 'view',
                    dataSourceContext,
                    field,
                    form,
                    configValues,
                    hasSecret: hasExistingSecret(publicConfigJson),
                    onChange
                  })}
                </FieldShell>
              ))}
            </div>
          </section>
        );
      })}
    </div>
  );
}

function FieldShell({ children, field }: { children: ReactNode; field: ConfigFieldSchema }) {
  return (
    <label className={`${field.span === 2 ? 'col-span-2' : ''} block text-sm`}>
      <span className="mb-1 block font-medium text-slate-700">
        {field.label}
        {field.required ? <span className="text-red-500"> *</span> : null}
      </span>
      {children}
      {field.helpText ? <span className="mt-1 block text-xs leading-5 text-slate-500">{field.helpText}</span> : null}
    </label>
  );
}

function renderField({
  disabled,
  dataSourceContext,
  field,
  form,
  configValues,
  hasSecret,
  onChange
}: {
  disabled: boolean;
  dataSourceContext?: DataSourceWorkspaceContext | null;
  field: ConfigFieldSchema;
  form: ApplicationDataCenterObjectUpsertRequest;
  configValues: Record<string, unknown>;
  hasSecret: boolean;
  onChange: (form: ApplicationDataCenterObjectUpsertRequest) => void;
}) {
  const value = resolveConfigValue(form, field);
  const commonClass = 'h-9 w-full rounded border border-slate-300 px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50 disabled:text-slate-500';

  if (field.name.toLowerCase() === 'sql') {
    return (
      <WorkbenchSqlEditor
        dataSourceId={dataSourceContext?.dataSourceId}
        label={field.label}
        readOnly={disabled}
        value={asString(value)}
        onChange={(next) => onChange(updateConfigField(form, field, next))}
      />
    );
  }

  if (field.component === 'textarea') {
    return (
      <textarea
        className={`${commonClass} min-h-[96px] py-2`}
        disabled={disabled}
        placeholder={field.placeholder}
        rows={field.rows ?? 4}
        value={asString(value)}
        onChange={(event) => onChange(updateConfigField(form, field, event.target.value))}
      />
    );
  }

  if (field.component === 'number') {
    return (
      <input
        className={commonClass}
        disabled={disabled}
        placeholder={field.placeholder}
        type="number"
        value={asNumberInput(value)}
        onChange={(event) => onChange(updateConfigField(form, field, event.target.value === '' ? '' : Number(event.target.value)))}
      />
    );
  }

  if (field.component === 'select') {
    return (
      <select className={commonClass} disabled={disabled} value={asString(value)} onChange={(event) => onChange(updateConfigField(form, field, event.target.value))}>
        <option value="">{translateCurrentLiteral("请选择")}</option>
        {field.options?.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  if (field.component === 'switch') {
    return (
      <button
        className={`inline-flex h-9 items-center gap-2 rounded border px-3 text-sm ${value ? 'border-primary-300 bg-primary-50 text-primary-700' : 'border-slate-300 bg-white text-slate-600'}`}
        disabled={disabled}
        type="button"
        onClick={() => onChange(updateConfigField(form, field, !value))}
      >
        <span className={`h-4 w-8 rounded-full ${value ? 'bg-primary-500' : 'bg-slate-300'}`}>
          <span className={`block h-4 w-4 rounded-full bg-white transition-transform ${value ? 'translate-x-4' : ''}`} />
        </span>
        {value ? '已启用' : '未启用'}
      </button>
    );
  }

  if (field.component === 'password') {
    return (
      <SensitiveFieldInput
        hasExistingSecret={hasSecret}
        label={field.label}
        value={asString(value)}
        onChange={(next) => onChange(updateConfigField(form, { ...field, target: 'secret' }, next))}
        onClear={() => onChange({ ...form, secretConfigJson: '' })}
      />
    );
  }

  if (
    field.component === 'objectSelect' ||
    field.component === 'dataSourceSelect' ||
    field.component === 'modelSelect' ||
    field.component === 'userSelect' ||
    field.component === 'riskFieldSelect' ||
    field.component === 'tableSelect'
  ) {
    return (
      <ConfigObjectSelect
        configValues={configValues}
        dataSourceContext={dataSourceContext}
        disabled={disabled}
        field={field}
        value={asString(value)}
        onChange={(next) => onChange(updateConfigField(form, field, next))}
      />
    );
  }

  if (field.component === 'keyValueList') {
    return (
      <KeyValueEditor
        value={toKeyValueRows(value, field.name)}
        onChange={(next) => onChange(updateConfigField(form, field, fromKeyValueRows(compactRows(next), field.name)))}
      />
    );
  }

  if (field.component === 'mappingList') {
    return <MappingEditor value={asArray(value)} onChange={(next) => onChange(updateConfigField(form, field, compactRows(next)))} />;
  }

  if (field.component === 'fieldList') {
    return <MappingEditor variant="fields" value={asArray(value)} onChange={(next) => onChange(updateConfigField(form, field, compactRows(next)))} />;
  }

  if (field.component === 'codeRuleSegments') {
    return <CodeRuleSegmentEditor value={asArray(value)} onChange={(next) => onChange(updateConfigField(form, field, next))} />;
  }

  return (
    <input
      className={commonClass}
      disabled={disabled}
      placeholder={field.placeholder}
      type="text"
      value={asString(value)}
      onChange={(event) => onChange(updateConfigField(form, field, event.target.value))}
    />
  );
}

function isVisible(condition: ConfigCondition | undefined, values: Record<string, unknown>): boolean {
  if (!condition) {
    return true;
  }

  const value = values[condition.field];
  if (condition.equals !== undefined) {
    return value === condition.equals;
  }

  if (condition.notEquals !== undefined) {
    return value !== condition.notEquals;
  }

  if (condition.includes) {
    return condition.includes.includes(value);
  }

  return true;
}

function asString(value: unknown): string {
  return typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean' ? String(value) : '';
}

function asNumberInput(value: unknown): string | number {
  return typeof value === 'number' ? value : typeof value === 'string' ? value : '';
}

function asArray<T>(value: unknown): T[] {
  return Array.isArray(value) ? (value as T[]) : [];
}

function compactRows<T extends object>(rows: T[]): T[] {
  return rows.filter((row) =>
    Object.values(row as Record<string, unknown>).some((value) => value !== null && value !== undefined && String(value).trim() !== '')
  );
}

function toKeyValueRows(value: unknown, fieldName: string): KeyValueRow[] {
  const rows = asArray<Record<string, unknown>>(value);
  if (fieldName === 'items') {
    return rows.map((row) => ({ key: asString(row.value ?? row.itemValue), value: asString(row.label ?? row.itemLabel) }));
  }

  return rows.map((row) => ({ key: asString(row.key), value: asString(row.value), description: asString(row.description) }));
}

function fromKeyValueRows(rows: KeyValueRow[], fieldName: string): KeyValueRow[] | Array<{ label: string; value: string }> {
  if (fieldName === 'items') {
    return rows.map((row) => ({ label: row.value, value: row.key }));
  }

  return rows;
}
