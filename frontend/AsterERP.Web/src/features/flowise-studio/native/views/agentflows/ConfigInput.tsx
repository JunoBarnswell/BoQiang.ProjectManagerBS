import {
  Button,
  Checkbox,
  FormControl,
  FormControlLabel,
  FormGroup,
  InputAdornment,
  InputLabel,
  MenuItem,
  Select,
  Switch,
  TextField
} from '@mui/material';
import { useMemo, useState } from 'react';

import { formatMessage } from '../../../../../core/i18n/formatMessage';
import { useI18n } from '../../../../../core/i18n/I18nProvider';
import { flowiseI18nKeys } from '../../../i18n/flowiseI18nKeys';
import type { FlowiseNodeInputParam } from '../../../types/node.types';

export interface FlowiseVariableOption {
  label: string;
  value: string;
}

interface ConfigInputProps {
  param: FlowiseNodeInputParam;
  variableOptions?: FlowiseVariableOption[];
  value: unknown;
  onChange: (name: string, value: unknown) => void;
}

export function ConfigInput({ param, variableOptions = [], value, onChange }: ConfigInputProps) {
  const { translate } = useI18n();
  const configInputKeys = flowiseI18nKeys.native.agentflows.configInput;
  const [jsonError, setJsonError] = useState(false);
  const [passwordVisible, setPasswordVisible] = useState(false);
  const selectedValues = useMemo(() => {
    if (Array.isArray(value)) {
      return value.map(String);
    }

    if (typeof value === 'string' && value) {
      return value.split(',').map((item) => item.trim()).filter(Boolean);
    }

    return [];
  }, [value]);

  if (param.type === 'boolean') {
    return (
      <FormControlLabel
        className="flowise-config-row flowise-config-row--inline"
        control={<Switch checked={Boolean(value)} size="small" onChange={(event) => onChange(param.name, event.target.checked)} />}
        label={param.label}
      />
    );
  }

  if (param.type === 'options' || param.type === 'credential') {
    return (
      <div className="flowise-config-row">
        <span>{param.label}</span>
        <FormControl fullWidth size="small">
          <InputLabel>{param.placeholder ?? param.label}</InputLabel>
          <Select
            label={param.placeholder ?? param.label}
            value={String(value ?? param.default ?? '')}
            onChange={(event) => onChange(param.name, event.target.value)}
          >
            <MenuItem value="">{param.placeholder ?? ''}</MenuItem>
            {(param.options ?? []).map((option) => {
              const optionValue = String(option.value ?? option.name ?? '');
              return (
              <MenuItem key={optionValue} value={optionValue}>
                {option.label}
              </MenuItem>
              );
            })}
          </Select>
        </FormControl>
      </div>
    );
  }

  if (param.type === 'multiOptions') {
    return (
      <FormControl className="flowise-config-row flowise-config-row--options" component="fieldset">
        <span>{param.label}</span>
        <FormGroup>
          {(param.options ?? []).map((option) => {
            const optionValue = String(option.value ?? option.name ?? '');
            const checked = selectedValues.includes(optionValue);
            return (
              <FormControlLabel
                key={optionValue}
                control={
                  <Checkbox
                    checked={checked}
                    size="small"
                    onChange={(event) => {
                      const nextValues = event.target.checked
                        ? [...selectedValues, optionValue]
                        : selectedValues.filter((item) => item !== optionValue);
                      onChange(param.name, nextValues);
                    }}
                  />
                }
                label={option.label}
              />
            );
          })}
        </FormGroup>
      </FormControl>
    );
  }

  if (param.type === 'json' || param.type === 'code' || param.type === 'array' || param.type === 'grid') {
    const textValue = typeof value === 'string' ? value : JSON.stringify(value ?? param.default ?? {}, null, 2);
    return (
      <div className="flowise-config-row">
        <span>{param.label}</span>
        <TextField
          error={jsonError}
          fullWidth
          multiline
          minRows={param.rows ?? 5}
          placeholder={param.placeholder ?? undefined}
          size="small"
          value={textValue}
          onBlur={(event) => {
            if (param.type === 'code') {
              return;
            }

            try {
              const parsed = event.target.value.trim() ? JSON.parse(event.target.value) : param.type === 'array' ? [] : {};
              setJsonError(false);
              onChange(param.name, parsed);
            } catch {
              setJsonError(true);
            }
          }}
          onChange={(event) => {
            setJsonError(false);
            onChange(param.name, event.target.value);
          }}
        />
        <VariableInsertControl
          disabled={!param.acceptVariable}
          options={variableOptions}
          variableInsertLabel={translate(configInputKeys.insertVariable)}
          variableInsertMenuLabel={translate(configInputKeys.variableInsertMenuLabel)}
          variableInsertTip={formatMessage(translate(configInputKeys.variableInsertTip), { example: '{{$node.output.content}}' })}
          variableSuggestionsEmpty={translate(configInputKeys.noUpstreamVariables)}
          onInsert={(template) => onChange(param.name, appendVariableTemplate(textValue, template, param.type))}
        />
        {jsonError ? <small>{translate(flowiseI18nKeys.messages.invalidJson)}</small> : null}
      </div>
    );
  }

  if (param.type === 'file') {
    return (
      <div className="flowise-config-row">
        <span>{param.label}</span>
        <Button size="small" variant="outlined" onClick={() => void pickFilesForFlowData((payload) => onChange(param.name, payload))}>
          {param.placeholder ?? translate(flowiseI18nKeys.actions.upload)}
        </Button>
        {Array.isArray(value) && value.length > 0 ? <small>{value.map((item) => fileNameOf(item)).join(', ')}</small> : null}
      </div>
    );
  }

  const textInputValue = String(value ?? param.default ?? '');
  const showVariableSuggestions = Boolean(param.acceptVariable && textInputValue.endsWith('{{'));

  return (
    <div className="flowise-config-row">
      <span>{param.label}</span>
      <TextField
        fullWidth
        placeholder={param.placeholder ?? undefined}
        size="small"
        type={param.type === 'number' ? 'number' : param.type === 'password' && !passwordVisible ? 'password' : param.type === 'date' ? 'date' : param.type === 'time' ? 'time' : 'text'}
        value={textInputValue}
        slotProps={{
          input: {
            endAdornment: param.type === 'password' ? (
              <InputAdornment position="end">
                <Button size="small" onClick={() => setPasswordVisible((current) => !current)}>
                  {translate(flowiseI18nKeys.actions.reveal)}
                </Button>
              </InputAdornment>
            ) : null
          }
        }}
        onChange={(event) => onChange(param.name, param.type === 'number' ? Number(event.target.value) : event.target.value)}
      />
      {showVariableSuggestions ? (
        <VariableSuggestionList
          options={variableOptions}
          onInsert={(template) => onChange(param.name, replaceVariableTrigger(textInputValue, template))}
        />
      ) : null}
      <VariableInsertControl
        disabled={!param.acceptVariable}
        options={variableOptions}
        variableInsertLabel={translate(configInputKeys.insertVariable)}
        variableInsertMenuLabel={translate(configInputKeys.variableInsertMenuLabel)}
        variableInsertTip={formatMessage(translate(configInputKeys.variableInsertTip), { example: '{{$node.output.content}}' })}
        variableSuggestionsEmpty={translate(configInputKeys.noUpstreamVariables)}
        onInsert={(template) => onChange(param.name, appendVariableTemplate(textInputValue, template, param.type))}
      />
    </div>
  );
}

function VariableInsertControl({
  disabled,
  variableInsertLabel,
  variableInsertMenuLabel,
  variableInsertTip,
  variableSuggestionsEmpty,
  options,
  onInsert
}: {
  disabled: boolean;
  variableInsertLabel: string;
  variableInsertMenuLabel: string;
  variableInsertTip: string;
  variableSuggestionsEmpty: string;
  options: FlowiseVariableOption[];
  onInsert: (template: string) => void;
}) {
  const [open, setOpen] = useState(false);

  if (disabled) {
    return null;
  }

  return (
    <div className="flowise-variable-insert">
      <Button
        aria-expanded={open}
        aria-haspopup="listbox"
        disabled={options.length === 0}
        size="small"
        type="button"
        variant="outlined"
        onClick={() => setOpen((current) => !current)}
      >
        {variableInsertLabel}
      </Button>
      {options.length === 0 ? <small>{variableSuggestionsEmpty}</small> : null}
      {open ? (
        <div className="flowise-variable-insert__menu" role="listbox" aria-label={variableInsertMenuLabel}>
          {options.map((option) => (
            <button
              key={`${option.value}-${option.label}`}
              className="flowise-variable-insert__option"
              role="option"
              type="button"
              onClick={() => {
                onInsert(option.value);
                setOpen(false);
              }}
            >
              <span>{option.label}</span>
              <code>{option.value}</code>
            </button>
          ))}
        </div>
      ) : null}
      <small>{variableInsertTip}</small>
    </div>
  );
}

function VariableSuggestionList({ options, onInsert }: { options: FlowiseVariableOption[]; onInsert: (template: string) => void }) {
  const { translate } = useI18n();
  const configInputKeys = flowiseI18nKeys.native.agentflows.configInput;
  if (options.length === 0) {
    return <small className="flowise-variable-trigger-empty">{translate(configInputKeys.variableSuggestionsEmpty)}</small>;
  }

  return (
    <div className="flowise-variable-trigger" role="listbox" aria-label={translate(configInputKeys.variableSuggestionsLabel)}>
      {options.map((option) => (
        <button
          key={`${option.value}-${option.label}`}
          className="flowise-variable-trigger__option"
          role="option"
          type="button"
          onClick={() => onInsert(option.value)}
        >
          <span>{option.label}</span>
          <code>{option.value}</code>
        </button>
      ))}
    </div>
  );
}

function appendVariableTemplate(currentValue: string, template: string, paramType?: string): string {
  if (paramType === 'array' || paramType === 'grid') {
    return appendVariableToJsonArray(currentValue, template);
  }

  if (paramType === 'json') {
    return appendVariableToJsonValue(currentValue, template);
  }

  if (!currentValue) {
    return template;
  }

  const separator = /\s$/.test(currentValue) ? '' : ' ';
  return `${currentValue}${separator}${template}`;
}

function replaceVariableTrigger(currentValue: string, template: string): string {
  return currentValue.endsWith('{{') ? `${currentValue.slice(0, -2)}${template}` : appendVariableTemplate(currentValue, template);
}

function appendVariableToJsonArray(currentValue: string, template: string): string {
  const parsed = parseJsonValue(currentValue);
  if (Array.isArray(parsed)) {
    return JSON.stringify([...parsed, template], null, 2);
  }

  return JSON.stringify([template], null, 2);
}

function appendVariableToJsonValue(currentValue: string, template: string): string {
  const parsed = parseJsonValue(currentValue);
  if (Array.isArray(parsed)) {
    return JSON.stringify([...parsed, template], null, 2);
  }

  if (parsed && typeof parsed === 'object') {
    return JSON.stringify({ ...parsed, value: template }, null, 2);
  }

  return JSON.stringify(template);
}

function parseJsonValue(value: string): unknown {
  try {
    return value.trim() ? JSON.parse(value) : undefined;
  } catch {
    return undefined;
  }
}

function pickFilesForFlowData(onPicked: (payload: Record<string, unknown>[]) => void): void {
  const input = document.createElement('input');
  input.type = 'file';
  input.multiple = true;
  input.addEventListener('change', () => {
    void (async () => {
      const files = Array.from(input.files ?? []);
      const payload = await Promise.all(files.map(readFileForFlowData));
      onPicked(payload);
    })();
  });
  input.click();
}

function readFileForFlowData(file: File): Promise<Record<string, unknown>> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.addEventListener('load', () => {
      resolve({
        data: reader.result,
        lastModified: file.lastModified,
        name: file.name,
        size: file.size,
        type: file.type
      });
    });
    reader.addEventListener('error', () => reject(reader.error));
    reader.readAsDataURL(file);
  });
}

function fileNameOf(value: unknown): string {
  if (value && typeof value === 'object' && 'name' in value) {
    return String((value as { name?: unknown }).name ?? '');
  }

  return String(value);
}
