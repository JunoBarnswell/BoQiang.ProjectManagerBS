import { useState } from 'react';

import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../../shared/icons/AppIcon';

interface SensitiveFieldInputProps {
  hasExistingSecret: boolean;
  label: string;
  value: string;
  onChange: (value: string) => void;
  onClear: () => void;
}

export function SensitiveFieldInput({ hasExistingSecret, label, value, onChange, onClear }: SensitiveFieldInputProps) {
  const [editing, setEditing] = useState(false);
  const configured = hasExistingSecret && !value && !editing;

  return (
    <div className="space-y-2">
      {configured ? (
        <div className="flex items-center justify-between gap-2 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm">
          <span className="inline-flex items-center gap-2 text-slate-700">
            <AppIcon className="h-4 w-4 text-slate-500" name="key" />{translateCurrentLiteral("已配置，默认不会覆盖")}</span>
          <div className="flex items-center gap-2">
            <button className="text-xs font-medium text-primary-700 hover:text-primary-800" type="button" onClick={() => setEditing(true)}>{translateCurrentLiteral("重新填写")}</button>
            <button className="text-xs font-medium text-red-600 hover:text-red-700" type="button" onClick={onClear}>{translateCurrentLiteral("清空")}</button>
          </div>
        </div>
      ) : null}
      {!configured ? (
        <input
          className="h-9 w-full rounded border border-slate-300 px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
          placeholder={`请输入${label}`}
          type="password"
          value={value}
          onChange={(event) => onChange(event.target.value)}
        />
      ) : null}
    </div>
  );
}
