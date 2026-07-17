import type { SelectHTMLAttributes } from 'react';

import { useI18n } from '../../core/i18n/I18nProvider';

import { useDict } from './useDict';

interface DictSelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'onChange'> {
  dictType: string;
  includeEmpty?: boolean;
  onValueChange?: (value: string) => void;
}

export function DictSelect({ dictType, includeEmpty = true, onValueChange, ...selectProps }: DictSelectProps) {
  const { translate } = useI18n();
  const { options } = useDict(dictType);

  return (
    <select {...selectProps} onChange={(event) => onValueChange?.(event.target.value)}>
      {includeEmpty ? <option value="">{translate('common.select')}</option> : null}
      {options.map((option) => (
        <option key={option.value} disabled={option.disabled} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  );
}
