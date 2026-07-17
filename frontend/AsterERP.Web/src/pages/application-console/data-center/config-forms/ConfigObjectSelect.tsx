import { useMemo, useState } from 'react';


import {
  getApplicationDataCenterObject,
  getApplicationDataSourceTables,
  listApplicationDataCenterObjects,
  type ApplicationDataCenterResourcePath
} from '../../../../api/application-data-center/applicationDataCenter.api';
import type { ApplicationDataCenterObjectListItem } from '../../../../api/application-data-center/applicationDataCenter.types';
import { getUser, getUsers } from '../../../../api/system/system-management.api';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useApiQuery } from '../../../../core/query/useApiQuery';
import type { DataSourceWorkspaceContext } from '../dataSourceWorkspaceTypes';

import type { ConfigFieldSchema } from './configFormTypes';

interface ConfigObjectSelectProps {
  configValues: Record<string, unknown>;
  dataSourceContext?: DataSourceWorkspaceContext | null;
  disabled?: boolean;
  field: ConfigFieldSchema;
  value: string;
  onChange: (value: string) => void;
}

const objectResourceLabels: Partial<Record<ApplicationDataCenterResourcePath, string>> = {
  'connection-tests': '检测任务',
  'data-sources': '数据源',
  'dictionaries-codes': '字典编码',
  'entities-fields': '实体字段',
  'integration-tasks': '同步任务',
  models: '数据模型',
  'api-services': 'API 服务',
  microflows: '微流',
  'query-datasets': '查询数据集'
};

const riskFields = [
  { label: translateCurrentLiteral("对象编码"), value: 'objectCode' },
  { label: translateCurrentLiteral("对象名称"), value: 'objectName' },
  { label: translateCurrentLiteral("对象类型"), value: 'objectType' },
  { label: translateCurrentLiteral("端点/路径"), value: 'endpoint' },
  { label: translateCurrentLiteral("公开配置"), value: 'configJson' },
  { label: translateCurrentLiteral("发布状态"), value: 'status' },
  { label: translateCurrentLiteral("来源对象"), value: 'sourceObjectId' },
  { label: translateCurrentLiteral("目标对象"), value: 'targetObjectId' }
];

export function ConfigObjectSelect({ configValues, dataSourceContext, disabled, field, value, onChange }: ConfigObjectSelectProps) {
  const [keyword, setKeyword] = useState('');
  const resourcePaths = resolveResourcePaths(field);
  const [resourcePath, setResourcePath] = useState<ApplicationDataCenterResourcePath>(resourcePaths[0] ?? 'microflows');

  if (field.component === 'userSelect') {
    return <UserSelect disabled={disabled} keyword={keyword} setKeyword={setKeyword} value={value} onChange={onChange} />;
  }

  if (field.component === 'riskFieldSelect') {
    return <RiskFieldSelect disabled={disabled} value={value} onChange={onChange} />;
  }

  if (field.component === 'tableSelect') {
    return (
      <TableSelect
        configValues={configValues}
        dataSourceContext={dataSourceContext}
        disabled={disabled}
        field={field}
        value={value}
        onChange={onChange}
      />
    );
  }

  return (
    <ObjectSelect
      disabled={disabled}
      field={field}
      keyword={keyword}
      resourcePath={resourcePath}
      resourcePaths={resourcePaths}
      setKeyword={setKeyword}
      setResourcePath={setResourcePath}
      value={value}
      onChange={onChange}
    />
  );
}

function ObjectSelect({
  disabled,
  field,
  keyword,
  resourcePath,
  resourcePaths,
  setKeyword,
  setResourcePath,
  value,
  onChange
}: {
  disabled?: boolean;
  field: ConfigFieldSchema;
  keyword: string;
  resourcePath: ApplicationDataCenterResourcePath;
  resourcePaths: ApplicationDataCenterResourcePath[];
  setKeyword: (value: string) => void;
  setResourcePath: (value: ApplicationDataCenterResourcePath) => void;
  value: string;
  onChange: (value: string) => void;
}) {
  const objectQuery = useApiQuery({
    queryFn: ({ signal }) =>
      listApplicationDataCenterObjects(
        resourcePath,
        {
          keyword,
          objectType: field.objectTypes?.length === 1 ? field.objectTypes[0] : undefined,
          pageIndex: 1,
          pageSize: 50
        },
        signal
      ),
    queryKey: ['application-data-center', 'object-select', resourcePath, keyword, field.objectTypes]
  });

  const selectedQuery = useApiQuery({
    enabled: Boolean(value),
    queryFn: ({ signal }) => getApplicationDataCenterObject(resourcePath, value, signal),
    queryKey: ['application-data-center', 'object-select-detail', resourcePath, value]
  });

  const rows = objectQuery.data?.data.items ?? [];
  const selected = selectedQuery.data?.data;

  return (
    <div className="space-y-2">
      <div className="flex gap-2">
        {resourcePaths.length > 1 ? (
          <select
            className="h-9 w-[120px] rounded border border-slate-300 bg-white px-2 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100"
            disabled={disabled}
            value={resourcePath}
            onChange={(event) => setResourcePath(event.target.value as ApplicationDataCenterResourcePath)}
          >
            {resourcePaths.map((path) => (
              <option key={path} value={path}>
                {objectResourceLabels[path] ?? path}
              </option>
            ))}
          </select>
        ) : null}
        <input
          className="h-9 min-w-0 flex-1 rounded border border-slate-300 px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50 disabled:text-slate-500"
          disabled={disabled}
          placeholder={translateCurrentLiteral("搜索名称、编码或端点")}
          value={keyword}
          onChange={(event) => setKeyword(event.target.value)}
        />
      </div>
      <select
        className="h-9 w-full rounded border border-slate-300 bg-white px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50 disabled:text-slate-500"
        disabled={disabled || objectQuery.isFetching}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">{objectQuery.isFetching ? '正在加载对象' : '请选择对象'}</option>
        {selected && !rows.some((item) => item.id === selected.id) ? (
          <option value={selected.id}>{formatObjectOption(selected)}</option>
        ) : null}
        {rows.map((item) => (
          <option key={item.id} value={item.id}>
            {formatObjectOption(item)}
          </option>
        ))}
      </select>
      {objectQuery.isError ? <div className="text-xs text-red-600">{translateCurrentLiteral("候选对象加载失败，请检查权限或稍后重试。")}</div> : null}
      {!objectQuery.isFetching && rows.length === 0 ? <div className="text-xs text-slate-500">{translateCurrentLiteral("暂无可选对象。")}</div> : null}
    </div>
  );
}

function UserSelect({
  disabled,
  keyword,
  setKeyword,
  value,
  onChange
}: {
  disabled?: boolean;
  keyword: string;
  setKeyword: (value: string) => void;
  value: string;
  onChange: (value: string) => void;
}) {
  const usersQuery = useApiQuery({
    queryFn: ({ signal }) => getUsers({ keyword, pageIndex: 1, pageSize: 50 }, signal),
    queryKey: ['application-data-center', 'user-select', keyword]
  });

  const selectedQuery = useApiQuery({
    enabled: Boolean(value),
    queryFn: () => getUser(value),
    queryKey: ['application-data-center', 'user-select-detail', value]
  });

  const rows = usersQuery.data?.data.items ?? [];
  const selected = selectedQuery.data?.data;

  return (
    <div className="space-y-2">
      <input
        className="h-9 w-full rounded border border-slate-300 px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50 disabled:text-slate-500"
        disabled={disabled}
        placeholder={translateCurrentLiteral("搜索用户姓名或账号")}
        value={keyword}
        onChange={(event) => setKeyword(event.target.value)}
      />
      <select
        className="h-9 w-full rounded border border-slate-300 bg-white px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50 disabled:text-slate-500"
        disabled={disabled || usersQuery.isFetching}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">{usersQuery.isFetching ? '正在加载用户' : '不指定负责人'}</option>
        {selected && !rows.some((item) => item.id === selected.id) ? (
          <option value={selected.id}>{selected.displayName} / {selected.userName}</option>
        ) : null}
        {rows.map((item) => (
          <option key={item.id} value={item.id}>
            {item.displayName} / {item.userName}
          </option>
        ))}
      </select>
      {usersQuery.isError ? <div className="text-xs text-red-600">{translateCurrentLiteral("用户加载失败，请检查权限或稍后重试。")}</div> : null}
    </div>
  );
}

function RiskFieldSelect({ disabled, value, onChange }: { disabled?: boolean; value: string; onChange: (value: string) => void }) {
  const selected = new Set(splitCsv(value));

  return (
    <div className="grid grid-cols-2 gap-2 rounded border border-slate-200 bg-slate-50 p-2">
      {riskFields.map((item) => (
        <label key={item.value} className="inline-flex items-center gap-2 text-xs text-slate-700">
          <input
            checked={selected.has(item.value)}
            disabled={disabled}
            type="checkbox"
            onChange={(event) => {
              const next = new Set(selected);
              if (event.target.checked) {
                next.add(item.value);
              } else {
                next.delete(item.value);
              }
              onChange(Array.from(next).join(','));
            }}
          />
          {item.label}
        </label>
      ))}
    </div>
  );
}

function TableSelect({
  configValues,
  dataSourceContext,
  disabled,
  field,
  value,
  onChange
}: ConfigObjectSelectProps) {
  const dataSourceId = resolveTableDataSourceId(field, configValues, dataSourceContext);
  const tablesQuery = useApiQuery({
    enabled: Boolean(dataSourceId),
    queryFn: ({ signal }) => getApplicationDataSourceTables(dataSourceId, signal),
    queryKey: ['application-data-center', 'table-select', dataSourceId]
  });

  const options = useMemo(
    () => tablesQuery.data?.data.map((item) => ({
      label: item.schemaName ? `${item.schemaName}.${item.tableName}` : item.tableName,
      type: item.tableType
    })) ?? [],
    [tablesQuery.data?.data]
  );

  return (
    <div className="space-y-2">
      <select
        className="h-9 w-full rounded border border-slate-300 bg-white px-3 text-sm outline-none focus:border-primary-400 focus:ring-2 focus:ring-primary-100 disabled:bg-slate-50 disabled:text-slate-500"
        disabled={disabled || !dataSourceId || tablesQuery.isFetching}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">{!dataSourceId ? '请先选择数据源' : tablesQuery.isFetching ? '正在加载表清单' : '请选择来源表'}</option>
        {value && !options.some((item) => item.label === value) ? <option value={value}>{value}</option> : null}
        {options.map((item) => (
          <option key={item.label} value={item.label}>
            {item.label} / {item.type}
          </option>
        ))}
      </select>
      {tablesQuery.isError ? <div className="text-xs text-red-600">{translateCurrentLiteral("表清单加载失败，请先确认数据源测试通过且账号有读取元数据权限。")}</div> : null}
      {!tablesQuery.isFetching && dataSourceId && options.length === 0 ? <div className="text-xs text-slate-500">{translateCurrentLiteral("当前数据源暂无可选表。")}</div> : null}
    </div>
  );
}

function resolveResourcePaths(field: ConfigFieldSchema): ApplicationDataCenterResourcePath[] {
  if (field.component === 'dataSourceSelect') {
    return ['data-sources'];
  }

  if (field.component === 'modelSelect') {
    return ['microflows'];
  }

  if (field.objectResourcePaths?.length) {
    return normalizeResourcePaths(field.objectResourcePaths);
  }

  return normalizeResourcePaths([field.objectResourcePath ?? 'microflows']);
}

function normalizeResourcePaths(paths: string[]): ApplicationDataCenterResourcePath[] {
  return paths
    .filter((path, index, values): path is ApplicationDataCenterResourcePath =>
      isApplicationDataCenterResourcePath(path) && values.indexOf(path) === index
    );
}

function isApplicationDataCenterResourcePath(path: string): path is ApplicationDataCenterResourcePath {
  return [
    'data-sources',
    'connection-tests',
    'microflows',
    'entities-fields',
    'dictionaries-codes',
    'query-datasets',
    'integration-tasks'
  ].includes(path);
}

function resolveTableDataSourceId(
  field: ConfigFieldSchema,
  configValues: Record<string, unknown>,
  dataSourceContext?: DataSourceWorkspaceContext | null
): string {
  const candidateKeys = [
    field.tableDataSourceField,
    'sourceDataSourceId',
    'dataSourceId',
    'sourceObjectId'
  ].filter(Boolean) as string[];

  for (const key of candidateKeys) {
    const value = configValues[key];
    if (typeof value === 'string' && value.trim()) {
      return value.trim();
    }
  }

  return dataSourceContext?.dataSourceId ?? '';
}

function formatObjectOption(item: Pick<ApplicationDataCenterObjectListItem, 'objectCode' | 'objectName' | 'objectType' | 'status'>) {
  return `${item.objectName} / ${item.objectCode} / ${item.objectType} / ${item.status}`;
}

function splitCsv(value: string): string[] {
  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}
