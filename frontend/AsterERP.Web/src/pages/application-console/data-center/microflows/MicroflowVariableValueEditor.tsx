import { Plus, Trash2 } from 'lucide-react';
import type { ReactNode } from 'react';

import type {
  MicroflowDomainField,
  MicroflowVariable
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';


import {
  createEmptyRow,
  createErrorKey,
  type MicroflowObjectInputValue,
  type MicroflowVariableInputValue,
  normalizeVariableFields,
  normalizeVariableValueType
} from './microflowVariableSchema';

interface MicroflowVariableValueEditorProps {
  errors: Record<string, string>;
  onChange: (values: Record<string, MicroflowVariableInputValue>) => void;
  values: Record<string, MicroflowVariableInputValue>;
  variables: MicroflowVariable[];
}

export function MicroflowVariableValueEditor({
  errors,
  onChange,
  values,
  variables
}: MicroflowVariableValueEditorProps) {
  if (variables.length === 0) {
    return <div className="microflow-preview-muted">{translateCurrentLiteral("无输入变量")}</div>;
  }

  return (
    <div className="microflow-preview-variable-form-list">
      {variables.map((variable) => {
        const variableCode = variable.variableCode.trim();
        return variableCode ? renderVariable(variable, variableCode) : null;
      })}
    </div>
  );

  function renderVariable(variable: MicroflowVariable, variableCode: string) {
    const valueType = normalizeVariableValueType(variable.valueType);
    const fields = normalizeVariableFields(variable);
    return (
      <section className="microflow-preview-variable-card" key={variableCode}>
        <header>
          <div>
            <strong>{variable.variableName || variableCode}</strong>
            <span>{variableCode} / {valueType}</span>
          </div>
        </header>
        {valueType === 'array'
          ? renderArrayVariable(variableCode, fields)
          : valueType === 'object'
            ? renderObjectVariable(variableCode, fields)
            : renderScalarVariable(variableCode, valueType)}
      </section>
    );
  }

  function renderScalarVariable(variableCode: string, valueType: string) {
    const value = values[variableCode] ?? '';
    const error = errors[createErrorKey(variableCode)];
    if (valueType === 'boolean') {
      return (
        <FieldShell error={error}>
          <select className="form-input h-8 text-xs" value={String(parseBoolean(value))} onChange={(event) => setVariableValue(variableCode, event.target.value === 'true')}>
            <option value="false">{translateCurrentLiteral("否")}</option>
            <option value="true">{translateCurrentLiteral("是")}</option>
          </select>
        </FieldShell>
      );
    }

    return (
      <FieldShell error={error}>
        <input
          className="form-input h-8 text-xs"
          inputMode={valueType === 'number' ? 'decimal' : undefined}
          type={valueType === 'date' ? 'date' : valueType === 'datetime' ? 'datetime-local' : 'text'}
          value={String(value ?? '')}
          onChange={(event) => setVariableValue(variableCode, event.target.value)}
        />
      </FieldShell>
    );
  }

  function renderObjectVariable(variableCode: string, fields: MicroflowDomainField[]) {
    if (fields.length === 0) {
      return <div className="microflow-preview-muted">{translateCurrentLiteral("未配置字段，将提交空对象")}</div>;
    }

    const value = asObjectValue(values[variableCode], fields);
    return (
      <div className="microflow-preview-object-grid">
        {fields.map((field) => renderObjectField(variableCode, field, value, (nextValue) => {
          setVariableValue(variableCode, { ...value, [field.fieldCode]: nextValue });
        }))}
      </div>
    );
  }

  function renderArrayVariable(variableCode: string, fields: MicroflowDomainField[]) {
    if (fields.length === 0) {
      return <div className="microflow-preview-muted">{translateCurrentLiteral("未配置字段，将提交空数组")}</div>;
    }

    const rows = Array.isArray(values[variableCode]) ? values[variableCode] as MicroflowObjectInputValue[] : [];
    return (
      <div className="microflow-preview-array-editor">
        <div className="microflow-preview-array-toolbar">
          <span>{rows.length} 行</span>
          <button className="secondary-button h-8 text-xs" type="button" onClick={() => setVariableValue(variableCode, [...rows, createEmptyRow(fields)])}>
            <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("添加行")}</button>
        </div>
        {rows.length === 0 ? <div className="microflow-preview-muted">{translateCurrentLiteral("暂无明细行")}</div> : null}
        {rows.map((row, rowIndex) => (
          <div className="microflow-preview-array-row" key={rowIndex}>
            <div className="microflow-preview-array-row__fields">
              {fields.map((field) => renderArrayField(variableCode, field, row, rowIndex, (nextValue) => {
                const nextRows = rows.map((item, current) => current === rowIndex ? { ...item, [field.fieldCode]: nextValue } : item);
                setVariableValue(variableCode, nextRows);
              }))}
            </div>
            <button className="icon-button h-8 w-8" title={translateCurrentLiteral("删除行")} type="button" onClick={() => setVariableValue(variableCode, rows.filter((_, current) => current !== rowIndex))}>
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
        ))}
      </div>
    );
  }

  function renderObjectField(
    variableCode: string,
    field: MicroflowDomainField,
    value: MicroflowObjectInputValue,
    onFieldChange: (value: boolean | string) => void
  ) {
    return (
      <FieldInput
        error={errors[createErrorKey(variableCode, field.fieldCode)]}
        field={field}
        key={field.fieldCode}
        value={value[field.fieldCode]}
        onChange={onFieldChange}
      />
    );
  }

  function renderArrayField(
    variableCode: string,
    field: MicroflowDomainField,
    row: MicroflowObjectInputValue,
    rowIndex: number,
    onFieldChange: (value: boolean | string) => void
  ) {
    return (
      <FieldInput
        compact
        error={errors[createErrorKey(variableCode, field.fieldCode, rowIndex)]}
        field={field}
        key={field.fieldCode}
        value={row[field.fieldCode]}
        onChange={onFieldChange}
      />
    );
  }

  function setVariableValue(variableCode: string, value: MicroflowVariableInputValue) {
    onChange({ ...values, [variableCode]: value });
  }
}

function FieldInput({
  compact = false,
  error,
  field,
  onChange,
  value
}: {
  compact?: boolean;
  error?: string;
  field: MicroflowDomainField;
  onChange: (value: boolean | string) => void;
  value: boolean | string | undefined;
}) {
  const valueType = normalizeVariableValueType(field.dataType);
  const label = field.fieldName || field.fieldCode;
  if (valueType === 'boolean') {
    return (
      <FieldShell compact={compact} error={error} label={label} required={field.required}>
        <select className="form-input h-8 text-xs" value={String(parseBoolean(value))} onChange={(event) => onChange(event.target.value === 'true')}>
          <option value="false">{translateCurrentLiteral("否")}</option>
          <option value="true">{translateCurrentLiteral("是")}</option>
        </select>
      </FieldShell>
    );
  }

  return (
    <FieldShell compact={compact} error={error} label={label} required={field.required}>
      <input
        className="form-input h-8 text-xs"
        inputMode={valueType === 'number' ? 'decimal' : undefined}
        type={valueType === 'date' ? 'date' : valueType === 'datetime' ? 'datetime-local' : 'text'}
        value={String(value ?? '')}
        onChange={(event) => onChange(event.target.value)}
      />
    </FieldShell>
  );
}

function FieldShell({
  children,
  compact = false,
  error,
  label,
  required = false
}: {
  children: ReactNode;
  compact?: boolean;
  error?: string;
  label?: string;
  required?: boolean;
}) {
  const className = [
    compact ? 'microflow-preview-field microflow-preview-field--compact' : 'microflow-preview-field',
    error ? 'microflow-preview-field--invalid' : ''
  ].filter(Boolean).join(' ');

  return (
    <label className={className}>
      {label ? <span>{label}{required ? <em>*</em> : null}</span> : null}
      {children}
      {error ? <small>{error}</small> : null}
    </label>
  );
}

function asObjectValue(value: MicroflowVariableInputValue | undefined, fields: MicroflowDomainField[]): MicroflowObjectInputValue {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
    ? value
    : createEmptyRow(fields);
}

function parseBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  const normalized = String(value ?? '').trim().toLowerCase();
  return normalized === 'true' || normalized === '1' || normalized === 'yes' || normalized === 'on';
}
