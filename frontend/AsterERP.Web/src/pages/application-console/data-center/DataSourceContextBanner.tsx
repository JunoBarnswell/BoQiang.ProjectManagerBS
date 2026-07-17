import type { ApplicationDataCenterObjectDetail } from '../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../core/i18n/I18nProvider';
import { AppIcon } from '../../../shared/icons/AppIcon';

import { DataCenterStatusBadge } from './DataCenterStatusBadge';
import { DataSourceWorkspaceSwitcher } from './DataSourceWorkspaceSwitcher';

interface DataSourceContextBannerProps {
  dataSource: ApplicationDataCenterObjectDetail | null;
  dataSourceId: string;
  loading?: boolean;
  onChange: (dataSourceId: string) => void;
  onClear: () => void;
}

export function DataSourceContextBanner({ dataSource, dataSourceId, loading, onChange, onClear }: DataSourceContextBannerProps) {
  if (!dataSourceId) {
    return (
      <section className="rounded-md border border-slate-200 bg-white px-4 py-3 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="min-w-0">
            <div className="flex items-center gap-2 text-sm font-semibold text-slate-900">
              <AppIcon className="h-4 w-4 text-slate-500" name="database" />{translateCurrentLiteral("未选择数据库上下文")}</div>
            <div className="mt-1 text-xs leading-5 text-slate-500">{translateCurrentLiteral("可以从数据源管理进入指定数据库，或在这里切换上下文后再维护模型、实体、接口、查询和同步任务。")}</div>
          </div>
          <DataSourceWorkspaceSwitcher value="" onChange={onChange} />
        </div>
      </section>
    );
  }

  return (
    <section className="rounded-md border border-primary-100 bg-primary-50 px-4 py-3 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <AppIcon className="h-4 w-4 text-primary-600" name="database" />
            <span className="text-sm font-semibold text-primary-950">
              {loading ? '正在加载数据库上下文' : dataSource?.objectName ?? '数据库上下文不可用'}
            </span>
            {dataSource ? <DataCenterStatusBadge status={dataSource.status} /> : null}
          </div>
          <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-xs leading-5 text-primary-800">
            <span>编码：{dataSource?.objectCode ?? dataSourceId}</span>
            <span>类型：{dataSource?.objectType ?? '-'}</span>
            <span>端点：{dataSource?.endpoint || '-'}</span>
            <span>最近检测：{dataSource?.lastValidationStatus || '-'}</span>
          </div>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <DataSourceWorkspaceSwitcher disabled={loading} value={dataSourceId} onChange={onChange} />
          <button className="ghost-button" type="button" onClick={onClear}>{translateCurrentLiteral("退出上下文")}</button>
        </div>
      </div>
    </section>
  );
}
