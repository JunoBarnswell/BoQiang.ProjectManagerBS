import type { ChangeEvent } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';

import { resolveDictOptionLabel } from './dictStore';
import { useDict } from './useDict';

interface DictCheckboxProps {
  dictType: string;
  options?: Array<{ disabled?: boolean; label: string; value: string }>;
  name: string;
  value?: string[];
  onValueChange?: (value: string[]) => void;
}

export function DictCheckbox({ dictType, name, onValueChange, options: explicitOptions, value = [] }: DictCheckboxProps) {
  const { translate } = useI18n();
  const { options: dictOptions } = useDict(dictType);
  const options = explicitOptions ?? dictOptions;

  return (
    <div className="flex flex-wrap gap-2.5">
      {options.map((option) => {
        const checked = value.includes(option.value);
        const resolvedLabel = resolveDictOptionLabel(option.label, translate);

        return (
          <label 
            key={option.value} 
            className={`flex items-center gap-2.5 cursor-pointer px-3 py-2 border rounded-md transition-all text-sm shadow-sm ${
              checked 
                ? 'border-primary-500 bg-primary-50 text-primary-700 ring-1 ring-primary-500/20' 
                : 'border-gray-200 bg-white text-gray-700 hover:border-gray-300 hover:bg-gray-50'
            }`}
          >
            <input
              checked={checked}
              name={name}
              onChange={(event: ChangeEvent<HTMLInputElement>) => {
                const nextValue = event.target.checked
                  ? [...value, option.value]
                  : value.filter((item) => item !== option.value);
                onValueChange?.(nextValue);
              }}
              type="checkbox"
              value={option.value}
              className="rounded border-gray-300 text-primary-600 focus:ring-primary-500 w-4 h-4 cursor-pointer"
            />
            <div className="flex flex-col justify-center">
              <span className="font-medium leading-none">{extractOptionTitle(resolvedLabel)}</span>
              {extractOptionMeta(resolvedLabel) ? (
                <span className={`text-[11px] mt-1 leading-none ${checked ? 'text-primary-600/80' : 'text-gray-400'}`}>
                  {extractOptionMeta(resolvedLabel)}
                </span>
              ) : null}
            </div>
          </label>
        );
      })}
    </div>
  );
}

function extractOptionTitle(label: string) {
  const match = label.match(/^(.*)\s+\(([^)]+)\)$/);
  return match ? match[1] : label;
}

function extractOptionMeta(label: string) {
  const match = label.match(/\(([^)]+)\)$/);
  return match ? match[1] : '';
}
