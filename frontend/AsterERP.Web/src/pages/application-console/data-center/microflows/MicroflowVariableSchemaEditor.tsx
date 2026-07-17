import { Copy, Plus, Trash2 } from 'lucide-react';

import type {
  MicroflowDomainField,
  MicroflowDomainObject,
  MicroflowVariable
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';


import {
  copyFieldsFromDomainObject,
  createMicroflowVariableField,
  findDomainObjectByCode,
  microflowFieldDataTypeOptions,
  microflowVariableValueTypeOptions,
  normalizeVariableFields,
  normalizeVariableValueType
} from './microflowVariableSchema';

interface MicroflowVariableSchemaEditorProps {
  domainObjects: MicroflowDomainObject[];
  onChange: (variable: MicroflowVariable) => void;
  onDelete: () => void;
  variable: MicroflowVariable;
}

export function MicroflowVariableSchemaEditor({
  domainObjects,
  onChange,
  onDelete,
  variable
}: MicroflowVariableSchemaEditorProps) {
  const valueType = normalizeVariableValueType(variable.valueType);
  const structured = valueType === 'array' || valueType === 'object';
  const fields = normalizeVariableFields(variable);
  const selectedDomainObject = findDomainObjectByCode(domainObjects, variable.schemaObjectCode);

  return (
    <div className="mb-2 grid gap-2 rounded border border-slate-200 p-2">
      <DenseInput label="编码" value={variable.variableCode} onChange={(variableCode) => patch({ variableCode })} />
      <DenseInput label="名称" value={variable.variableName} onChange={(variableName) => patch({ variableName })} />
      <label className="microflow-dense-input">
        <span className="text-right text-sky-700">{translateCurrentLiteral("类型")}</span>
        <select className="form-input h-8 text-xs" value={variable.valueType || 'string'} onChange={(event) => changeValueType(event.target.value)}>
          {microflowVariableValueTypeOptions.map((option) => (
            <option key={option.value} value={option.value}>{option.label}</option>
          ))}
        </select>
      </label>

      {structured ? (
        <div className="grid gap-2 rounded border border-slate-100 bg-slate-50 p-2">
          <label className="microflow-dense-input">
            <span className="text-right text-sky-700">{translateCurrentLiteral("对象")}</span>
            <select className="form-input h-8 text-xs" value={variable.schemaObjectCode ?? ''} onChange={(event) => patch({ schemaObjectCode: event.target.value || null })}>
              <option value="">{translateCurrentLiteral("未绑定")}</option>
              {domainObjects.map((object) => (
                <option key={object.objectCode} value={object.objectCode}>
                  {object.objectName || object.objectCode}
                </option>
              ))}
            </select>
          </label>
          <button
            className="secondary-button h-7 w-full text-xs"
            disabled={!selectedDomainObject}
            type="button"
            onClick={copySelectedDomainFields}
          >
            <Copy className="h-3.5 w-3.5" />{translateCurrentLiteral("带入领域字段")}</button>
          <div className="grid gap-1">
            {fields.map((field, index) => renderField(field, index))}
            <button className="secondary-button h-7 w-full text-xs" type="button" onClick={addField}>
              <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新增字段")}</button>
          </div>
          {renderDefaultValueEditor()}
        </div>
      ) : (
        renderScalarDefaultEditor()
      )}

      <button className="danger-button h-7 w-full text-xs" type="button" onClick={onDelete}>
        <Trash2 className="h-3.5 w-3.5" />{translateCurrentLiteral("删除变量")}</button>
    </div>
  );

  function patch(patchValue: Partial<MicroflowVariable>) {
    onChange({ ...variable, ...patchValue });
  }

  function changeValueType(nextValueType: string) {
    const nextType = normalizeVariableValueType(nextValueType);
    const nextDefaultValue = nextType === 'array'
      ? Array.isArray(variable.defaultValue) ? variable.defaultValue : []
      : nextType === 'object'
        ? isPlainRecord(variable.defaultValue) ? variable.defaultValue : {}
        : nextType === 'boolean'
          ? parseBoolean(variable.defaultValue)
          : variable.defaultValue ?? '';
    patch({ defaultValue: nextDefaultValue, valueType: nextValueType });
  }

  function copySelectedDomainFields() {
    if (!selectedDomainObject) {
      return;
    }

    patch({
      fields: copyFieldsFromDomainObject(selectedDomainObject),
      schemaObjectCode: selectedDomainObject.objectCode
    });
  }

  function addField() {
    patch({ fields: [...fields, createMicroflowVariableField(fields.length)] });
  }

  function patchField(index: number, patchValue: Partial<MicroflowDomainField>) {
    patch({
      fields: fields.map((field, current) => current === index ? { ...field, ...patchValue } : field)
    });
  }

  function deleteField(index: number) {
    patch({ fields: fields.filter((_, current) => current !== index) });
  }

  function renderField(field: MicroflowDomainField, index: number) {
    return (
      <div className="grid gap-1 rounded border border-slate-200 bg-white p-2" key={`${field.fieldCode}-${index}`}>
        <DenseInput label="字段" value={field.fieldCode} onChange={(fieldCode) => patchField(index, { fieldCode })} />
        <DenseInput label="显示名" value={field.fieldName} onChange={(fieldName) => patchField(index, { fieldName })} />
        <label className="microflow-dense-input">
          <span className="text-right text-sky-700">{translateCurrentLiteral("类型")}</span>
          <select className="form-input h-8 text-xs" value={field.dataType || 'string'} onChange={(event) => patchField(index, { dataType: event.target.value })}>
            {microflowFieldDataTypeOptions.map((option) => (
              <option key={option.value} value={option.value}>{option.label}</option>
            ))}
          </select>
        </label>
        <div className="grid grid-cols-2 gap-1 text-xs">
          <Check label="隐藏" checked={!field.visible} onChange={(checked) => patchField(index, { visible: !checked })} />
          <Check label="可写" checked={field.writable} onChange={(checked) => patchField(index, { writable: checked })} />
          <Check label="必填" checked={Boolean(field.required)} onChange={(checked) => patchField(index, { required: checked })} />
          <Check label="只读" checked={Boolean(field.readOnly)} onChange={(checked) => patchField(index, { readOnly: checked })} />
        </div>
        <button className="danger-button h-7 w-full text-xs" type="button" onClick={() => deleteField(index)}>
          <Trash2 className="h-3.5 w-3.5" />{translateCurrentLiteral("删除字段")}</button>
      </div>
    );
  }

  function renderScalarDefaultEditor() {
    if (valueType === 'boolean') {
      return (
        <label className="microflow-dense-input">
          <span className="text-right text-sky-700">{translateCurrentLiteral("默认")}</span>
          <select className="form-input h-8 text-xs" value={String(parseBoolean(variable.defaultValue))} onChange={(event) => patch({ defaultValue: event.target.value === 'true' })}>
            <option value="false">{translateCurrentLiteral("否")}</option>
            <option value="true">{translateCurrentLiteral("是")}</option>
          </select>
        </label>
      );
    }

    return (
      <label className="microflow-dense-input">
        <span className="text-right text-sky-700">{translateCurrentLiteral("默认")}</span>
        <input
          className="form-input h-8 text-xs"
          inputMode={valueType === 'number' ? 'decimal' : undefined}
          type={valueType === 'date' ? 'date' : valueType === 'datetime' ? 'datetime-local' : 'text'}
          value={String(variable.defaultValue ?? '')}
          onChange={(event) => patch({ defaultValue: event.target.value })}
        />
      </label>
    );
  }

  function renderDefaultValueEditor() {
    if (fields.length === 0) {
      return <div className="rounded border border-dashed border-slate-200 px-2 py-4 text-center text-xs text-slate-500">{translateCurrentLiteral("未配置字段")}</div>;
    }

    return valueType === 'array' ? renderArrayDefaultEditor() : renderObjectDefaultEditor();
  }

  function renderObjectDefaultEditor() {
    const value = isPlainRecord(variable.defaultValue) ? variable.defaultValue : {};
    return (
      <div className="grid gap-1 rounded border border-slate-200 bg-white p-2">
        <div className="text-xs font-semibold text-slate-600">{translateCurrentLiteral("对象默认值")}</div>
        {fields.map((field) => (
          <DefaultFieldInput
            field={field}
            key={field.fieldCode}
            value={value[field.fieldCode]}
            onChange={(nextValue) => patch({ defaultValue: { ...value, [field.fieldCode]: nextValue } })}
          />
        ))}
      </div>
    );
  }

  function renderArrayDefaultEditor() {
    const rows = Array.isArray(variable.defaultValue)
      ? variable.defaultValue.filter(isPlainRecord)
      : [];
    return (
      <div className="grid gap-2 rounded border border-slate-200 bg-white p-2">
        <div className="flex items-center justify-between gap-2">
          <div className="text-xs font-semibold text-slate-600">{translateCurrentLiteral("数组默认行")}</div>
          <button className="secondary-button h-7 text-xs" type="button" onClick={() => patch({ defaultValue: [...rows, {}] })}>
            <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("添加行")}</button>
        </div>
        {rows.map((row, rowIndex) => (
          <div className="grid gap-1 rounded border border-slate-100 bg-slate-50 p-2" key={rowIndex}>
            {fields.map((field) => (
              <DefaultFieldInput
                field={field}
                key={field.fieldCode}
                value={row[field.fieldCode]}
                onChange={(nextValue) => {
                  const nextRows = rows.map((item, current) => current === rowIndex ? { ...item, [field.fieldCode]: nextValue } : item);
                  patch({ defaultValue: nextRows });
                }}
              />
            ))}
            <button className="danger-button h-7 w-full text-xs" type="button" onClick={() => patch({ defaultValue: rows.filter((_, current) => current !== rowIndex) })}>
              <Trash2 className="h-3.5 w-3.5" />{translateCurrentLiteral("删除默认行")}</button>
          </div>
        ))}
      </div>
    );
  }
}

function DenseInput({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="microflow-dense-input">
      <span className="text-right text-sky-700">{label}</span>
      <input className="form-input h-8 text-xs" value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function DefaultFieldInput({
  field,
  onChange,
  value
}: {
  field: MicroflowDomainField;
  onChange: (value: boolean | string) => void;
  value: unknown;
}) {
  const valueType = normalizeVariableValueType(field.dataType);
  if (valueType === 'boolean') {
    return (
      <label className="microflow-dense-input">
        <span className="text-right text-sky-700">{field.fieldName || field.fieldCode}</span>
        <select className="form-input h-8 text-xs" value={String(parseBoolean(value))} onChange={(event) => onChange(event.target.value === 'true')}>
          <option value="false">{translateCurrentLiteral("否")}</option>
          <option value="true">{translateCurrentLiteral("是")}</option>
        </select>
      </label>
    );
  }

  return (
    <label className="microflow-dense-input">
      <span className="text-right text-sky-700">{field.fieldName || field.fieldCode}</span>
      <input
        className="form-input h-8 text-xs"
        inputMode={valueType === 'number' ? 'decimal' : undefined}
        type={valueType === 'date' ? 'date' : valueType === 'datetime' ? 'datetime-local' : 'text'}
        value={String(value ?? '')}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function Check({ checked, label, onChange }: { checked: boolean; label: string; onChange: (checked: boolean) => void }) {
  return <label className="flex items-center gap-1"><input checked={checked} type="checkbox" onChange={(event) => onChange(event.target.checked)} /> {label}</label>;
}

function isPlainRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function parseBoolean(value: unknown): boolean {
  if (typeof value === 'boolean') {
    return value;
  }

  const normalized = String(value ?? '').trim().toLowerCase();
  return normalized === 'true' || normalized === '1' || normalized === 'yes' || normalized === 'on';
}
