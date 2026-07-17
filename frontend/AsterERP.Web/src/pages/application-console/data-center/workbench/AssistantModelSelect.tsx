import { ChevronDown } from 'lucide-react';

import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import type { AiModelConfigDto } from '../../../../features/ai-center/api/aiCenter.api';

interface AssistantModelSelectProps {
  loading?: boolean;
  models: AiModelConfigDto[];
  value: string;
  onChange: (value: string) => void;
}

export function AssistantModelSelect({ loading, models, value, onChange }: AssistantModelSelectProps) {
  const selected = models.find((model) => model.id === value);
  const enabledModels = models.filter((model) => model.isEnabled);
  const groups = groupModels(enabledModels);

  return (
    <label className="relative flex min-w-0 items-center gap-2 rounded-lg border border-slate-200 bg-white px-2.5 py-1.5 text-xs text-slate-600 shadow-sm">
      <span className="shrink-0 text-slate-400">{translateCurrentLiteral("模型")}</span>
      <select
        className="min-w-0 max-w-[190px] appearance-none bg-transparent pr-5 text-xs font-medium text-slate-800 outline-none disabled:cursor-not-allowed disabled:text-slate-400"
        disabled={loading || enabledModels.length === 0}
        title={selected ? `${selected.displayName} (${selected.modelCode})` : '请选择模型'}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">{loading ? '加载中' : '请选择模型'}</option>
        {groups.map((group) => (
          <optgroup key={group.providerName} label={group.providerName}>
            {group.models.map((model) => (
              <option key={model.id} value={model.id}>
                {model.displayName} · {model.modelCode} · {model.maxContextTokens}
              </option>
            ))}
          </optgroup>
        ))}
      </select>
      <ChevronDown className="pointer-events-none absolute right-2 h-3.5 w-3.5 text-slate-400" />
    </label>
  );
}

function groupModels(models: AiModelConfigDto[]) {
  const map = new Map<string, AiModelConfigDto[]>();
  for (const model of models) {
    const key = model.providerName || '默认供应商';
    map.set(key, [...(map.get(key) ?? []), model]);
  }

  return Array.from(map.entries()).map(([providerName, items]) => ({
    models: items.sort((left, right) => left.sortOrder - right.sortOrder),
    providerName
  }));
}
