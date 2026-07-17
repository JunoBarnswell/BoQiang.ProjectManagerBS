import { useEffect, useState } from 'react';

import type { ApplicationMappingCacheColumn, ApplicationMappingCacheParameter } from '../../../../api/application-data-center/applicationDataCenter.types';

import { WorkbenchDrawerForm } from './components/WorkbenchDrawerForm';
import { formatMappingCacheParameterValue, getMappingCacheParameterTypes, parseMappingCacheParameterValue, type MappingCacheParameterType, validateMappingCacheParameterDraft } from './mappingCacheParameterTypes';

const EMPTY_PARAMETERS: ApplicationMappingCacheParameter[] = [];
const EMPTY_VALUES: Record<string, unknown> = {};

interface MappingCacheParameterDialogProps {
  open: boolean;
  mode: 'configure' | 'execute';
  columns: ApplicationMappingCacheColumn[];
  parameters?: ApplicationMappingCacheParameter[];
  parameter?: ApplicationMappingCacheParameter | null;
  existingParameters?: ApplicationMappingCacheParameter[];
  initialValues?: Record<string, unknown>;
  onClose: () => void;
  onSave?: (parameter: ApplicationMappingCacheParameter) => void;
  onRun?: (values: Record<string, unknown>) => void;
}

export function MappingCacheParameterDialog({ open, mode, columns, parameters = EMPTY_PARAMETERS, parameter, existingParameters = EMPTY_PARAMETERS, initialValues = EMPTY_VALUES, onClose, onSave, onRun }: MappingCacheParameterDialogProps) {
  const [draft, setDraft] = useState<ApplicationMappingCacheParameter | null>(null);
  const [defaultInput, setDefaultInput] = useState('');
  const [executionInputs, setExecutionInputs] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    if (mode === 'configure') {
      const boundColumn = parameter ? columns.find((column) => column.sourceResourceId === parameter.columnResourceId) : columns[0];
      const next = parameter ?? { resourceId: '', name: boundColumn?.targetName ?? '', columnResourceId: boundColumn?.sourceResourceId ?? '', dataType: boundColumn ? getMappingCacheParameterTypes(boundColumn)[0] : 'string', required: true };
      setDraft(next);
      setDefaultInput(formatMappingCacheParameterValue(next.dataType as MappingCacheParameterType, next.defaultValue));
    } else {
      setExecutionInputs(Object.fromEntries(parameters.map((item) => [item.resourceId, formatMappingCacheParameterValue(item.dataType as MappingCacheParameterType, initialValues[item.resourceId] ?? item.defaultValue)])));
    }
    setError(null);
  }, [columns, existingParameters, initialValues, mode, open, parameter, parameters]);

  if (!open) return null;
  const title = mode === 'configure' ? (parameter ? 'Edit parameter' : 'Add parameter') : 'Enter mapping cache parameters';
  const save = () => {
    if (!draft || !onSave) return;
    const errors = validateMappingCacheParameterDraft(draft, existingParameters, parameter?.resourceId);
    const parsedDefault = defaultInput.trim() === '' ? { value: undefined } : parseMappingCacheParameterValue(draft.dataType as MappingCacheParameterType, defaultInput);
    if (parsedDefault.error) errors.defaultValue = parsedDefault.error;
    const firstError = errors.name ?? errors.columnResourceId ?? errors.dataType ?? errors.defaultValue;
    if (firstError) { setError(firstError); return; }
    onSave({ ...draft, name: draft.name.trim(), defaultValue: parsedDefault.value });
  };
  const run = () => {
    if (!onRun) return;
    const values: Record<string, unknown> = {};
    for (const item of parameters) {
      const raw = executionInputs[item.resourceId] ?? '';
      if (raw.trim() === '') {
        if (item.required && item.defaultValue === undefined) { setError(`Required parameter is missing: ${item.name}.`); return; }
        if (item.defaultValue !== undefined) values[item.resourceId] = item.defaultValue;
        continue;
      }
      const parsed = parseMappingCacheParameterValue(item.dataType as MappingCacheParameterType, raw);
      if (parsed.error) { setError(`${item.name}: ${parsed.error}`); return; }
      values[item.resourceId] = parsed.value;
    }
    onRun(values);
  };

  return <WorkbenchDrawerForm open={open} title={title} width="lg" onClose={onClose} footer={<><button className="secondary-button" type="button" onClick={onClose}>Cancel</button><button className="primary-button" type="button" onClick={mode === 'configure' ? save : run}>{mode === 'configure' ? 'Save parameter' : 'Run test'}</button></>}>
    <div className="space-y-4">
      {error ? <p className="rounded border border-red-200 bg-red-50 p-2 text-sm text-red-700" role="alert">{error}</p> : null}
      {mode === 'configure' && draft ? <>
        <label className="block text-sm font-medium text-slate-700">Parameter name<input className="form-input mt-1 h-9" value={draft.name} onChange={(event) => setDraft({ ...draft, name: event.target.value, resourceId: '' })} /></label>
        <label className="block text-sm font-medium text-slate-700">Bound column<select className="form-input mt-1 h-9" value={draft.columnResourceId} onChange={(event) => { const column = columns.find((item) => item.sourceResourceId === event.target.value); setDraft({ ...draft, columnResourceId: event.target.value, dataType: column ? getMappingCacheParameterTypes(column)[0] : draft.dataType, resourceId: '' }); }}><option value="">Select a column</option>{columns.map((column) => <option key={column.sourceResourceId} value={column.sourceResourceId}>{column.targetName}</option>)}</select></label>
        <label className="block text-sm font-medium text-slate-700">Value type<select className="form-input mt-1 h-9" value={draft.dataType} onChange={(event) => setDraft({ ...draft, dataType: event.target.value })}>{(draft.columnResourceId ? getMappingCacheParameterTypes(columns.find((column) => column.sourceResourceId === draft.columnResourceId) ?? { dataType: 'string' } as ApplicationMappingCacheColumn) : ['string']).map((type) => <option key={type} value={type}>{type}</option>)}</select></label>
        <label className="flex items-center gap-2 text-sm font-medium text-slate-700"><input type="checkbox" checked={draft.required} onChange={(event) => setDraft({ ...draft, required: event.target.checked })} />Required</label>
        <label className="block text-sm font-medium text-slate-700">Default value<input className="form-input mt-1 h-9" value={defaultInput} onChange={(event) => setDefaultInput(event.target.value)} placeholder={draft.dataType === 'json' ? '{"key":"value"}' : 'Optional'} /></label>
      </> : parameters.map((item) => <ParameterValueInput key={item.resourceId} parameter={item} value={executionInputs[item.resourceId] ?? ''} onChange={(value) => setExecutionInputs((current) => ({ ...current, [item.resourceId]: value }))} />)}
    </div>
  </WorkbenchDrawerForm>;
}

function ParameterValueInput({ parameter, value, onChange }: { parameter: ApplicationMappingCacheParameter; value: string; onChange: (value: string) => void }) {
  const type = parameter.dataType as MappingCacheParameterType;
  if (type === 'boolean') return <label className="flex items-center gap-2 text-sm font-medium text-slate-700"><input type="checkbox" checked={value === 'true'} onChange={(event) => onChange(event.target.checked ? 'true' : 'false')} />{parameter.name}{parameter.required ? ' *' : ''}</label>;
  return <label className="block text-sm font-medium text-slate-700">{parameter.name}{parameter.required ? ' *' : ''}{type === 'json' ? <textarea className="form-input mt-1 min-h-24" value={value} onChange={(event) => onChange(event.target.value)} /> : <input className="form-input mt-1 h-9" type={type === 'number' ? 'number' : type === 'date' ? 'date' : 'text'} value={value} onChange={(event) => onChange(event.target.value)} />}</label>;
}
