import type { ApplicationDataCenterTypeOption } from '../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';
import { WorkspacePanel } from '../workspace-shell/WorkspacePanel';

interface DataCenterTypeTreeProps {
  activeType: string;
  loading?: boolean;
  options: ApplicationDataCenterTypeOption[];
  onChange: (type: string) => void;
}

export function DataCenterTypeTree({ activeType, loading, options, onChange }: DataCenterTypeTreeProps) {
  return (
    <WorkspacePanel bodyClassName="flex-1 space-y-1 overflow-y-auto p-2" className="flex h-full min-h-0 flex-col" description="按 PRD 类型查看配置对象" title={translateCurrentLiteral("类型筛选")}>
        <button
          type="button"
          className={`flex w-full items-center gap-2 rounded-md px-2 py-2 text-left text-sm transition-colors ${
            activeType ? 'text-slate-600 hover:bg-slate-50' : 'bg-primary-50 font-medium text-primary-700'
          }`}
          onClick={() => onChange('')}
        >
          <AppIcon className="h-4 w-4" name="list" />{translateCurrentLiteral("全部类型")}</button>
        {loading ? <div className="px-2 py-3 text-xs text-slate-400">{translateCurrentLiteral("正在加载类型...")}</div> : null}
        {options.map((option) => (
          <button
            key={option.type}
            type="button"
            className={`w-full rounded-md px-2 py-2 text-left transition-colors ${
              activeType === option.type ? 'bg-primary-50 text-primary-700' : 'text-slate-600 hover:bg-slate-50'
            }`}
            onClick={() => onChange(option.type)}
          >
            <div className="flex items-center justify-between gap-2">
              <span className="truncate text-sm font-medium">{option.title}</span>
              <span className="rounded border border-slate-200 px-1.5 py-0.5 text-[11px] text-slate-500">{option.type}</span>
            </div>
            <div className="mt-1 line-clamp-2 text-xs leading-5 text-slate-500">{option.description}</div>
          </button>
        ))}
    </WorkspacePanel>
  );
}
