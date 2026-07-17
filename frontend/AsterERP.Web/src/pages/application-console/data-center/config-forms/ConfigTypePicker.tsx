import type { ApplicationDataCenterTypeOption } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';

interface ConfigTypePickerProps {
  activeType: string;
  options: ApplicationDataCenterTypeOption[];
  onChange: (type: string) => void;
}

export function ConfigTypePicker({ activeType, options, onChange }: ConfigTypePickerProps) {
  return (
    <section>
      <div className="mb-3 text-sm font-semibold text-slate-900">{translateCurrentLiteral("1. 类型")}</div>
      <div className="grid grid-cols-2 gap-2">
        {options.map((option) => (
          <button
            key={option.type}
            type="button"
            className={`rounded-md border p-3 text-left transition-colors ${
              activeType === option.type
                ? 'border-primary-300 bg-primary-50 text-primary-800'
                : 'border-slate-200 bg-white text-slate-700 hover:border-primary-200 hover:bg-primary-50/50'
            }`}
            onClick={() => onChange(option.type)}
          >
            <div className="text-sm font-medium">{option.title}</div>
            <div className="mt-1 line-clamp-2 text-xs leading-5 text-slate-500">{option.description}</div>
            {option.requiredFields.length > 0 ? (
              <div className="mt-2 truncate text-[11px] text-slate-400">必填：{option.requiredFields.join('、')}</div>
            ) : null}
          </button>
        ))}
      </div>
    </section>
  );
}
