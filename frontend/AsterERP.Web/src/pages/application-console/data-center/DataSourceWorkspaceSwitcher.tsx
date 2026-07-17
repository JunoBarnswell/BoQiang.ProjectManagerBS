import { useParams } from 'react-router-dom';

import {
  listApplicationDataCenterObjects
} from '../../../api/application-data-center/applicationDataCenter.api';
import { queryKeys } from '../../../core/query/queryKeys';
import { useApiQuery } from '../../../core/query/useApiQuery';

interface DataSourceWorkspaceSwitcherProps {
  disabled?: boolean;
  value: string;
  onChange: (dataSourceId: string) => void;
}

export function DataSourceWorkspaceSwitcher({ disabled, value, onChange }: DataSourceWorkspaceSwitcherProps) {
  const { appCode, tenantId } = useParams();
  const dataSourcesQuery = useApiQuery({
    queryFn: ({ signal }) =>
      listApplicationDataCenterObjects(
        'data-sources',
        {
          pageIndex: 1,
          pageSize: 100,
          status: ''
        },
        signal
      ),
    queryKey: queryKeys.applicationDataCenter.workspaceSwitcher(tenantId, appCode)
  });

  const items = dataSourcesQuery.data?.data.items ?? [];

  return (
    <select
      className="h-9 min-w-[220px] rounded border border-slate-300 bg-white px-2 text-sm text-slate-700 outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50 disabled:text-slate-500"
      disabled={disabled || dataSourcesQuery.isFetching}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    >
      <option value="">{dataSourcesQuery.isFetching ? '正在加载数据源' : '切换数据库上下文'}</option>
      {items.map((item) => (
        <option key={item.id} value={item.id}>
          {item.objectName} / {item.objectCode}
        </option>
      ))}
    </select>
  );
}
