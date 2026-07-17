import { Edit3, ExternalLink, GitBranch, Plus, RefreshCcw, Route, Search, Trash2 } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';


import {
  createApplicationDataCenterObject,
  deleteApplicationDataCenterObject,
  getApplicationDataCenterObject,
  listApplicationDataCenterObjects,
  updateApplicationDataCenterObject
} from '../../../../api/application-data-center/applicationDataCenter.api';
import type { ApplicationDataCenterObjectListItem, ApplicationDataCenterObjectUpsertRequest } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../core/query/useApiQuery';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';
import { createDefaultMicroflowDefinition, normalizeMicroflowCode } from '../microflows/microflowDefaults';

import { WorkbenchDrawerForm } from './components/WorkbenchDrawerForm';

interface DataSourceMicroflowsPanelProps {
  dataSourceId: string;
  onRefresh: () => void;
}

const resourcePath = 'microflows' as const;

export function DataSourceMicroflowsPanel({ dataSourceId, onRefresh }: DataSourceMicroflowsPanelProps) {
  const navigate = useNavigate();
  const { appCode, tenantId } = useParams();
  const message = useMessage();
  const confirm = useConfirm();
  const [keyword, setKeyword] = useState('');
  const [selectedId, setSelectedId] = useState('');
  const [form, setForm] = useState<MicroflowFormState | null>(null);
  const microflowsQuery = useApiQuery({
    enabled: Boolean(dataSourceId),
    keepPreviousData: true,
    queryFn: ({ signal }) => listApplicationDataCenterObjects(resourcePath, { keyword, pageIndex: 1, pageSize: 100 }, signal),
    queryKey: ['application-data-center', 'workbench', dataSourceId, 'microflows', keyword]
  });
  const microflows = useMemo(
    () => microflowsQuery.data?.data.items ?? [],
    [microflowsQuery.data?.data.items]
  );
  const selectedMicroflow = microflows.find((item) => item.id === selectedId) ?? microflows[0] ?? null;
  const saveMutation = useApiMutation({
    mutationFn: () => {
      if (!form) {
        throw new Error('表单为空');
      }

      const request = buildRequest(form, dataSourceId);
      return form.id
        ? updateApplicationDataCenterObject(resourcePath, form.id, request)
        : createApplicationDataCenterObject(resourcePath, request);
    },
    onError: (error) => message.error(getErrorMessage(error, '保存微流失败')),
    onSuccess: (response) => {
      message.success('微流已保存');
      setSelectedId(response.data.object.id);
      setForm(null);
      onRefresh();
    }
  });

  return (
    <section className="grid min-h-[560px] gap-3 xl:grid-cols-[360px_1fr]">
      <aside className="rounded-md border border-slate-200 bg-white">
        <div className="border-b border-slate-100 p-3">
          <div className="mb-3 flex items-center justify-between gap-2">
            <div className="min-w-0">
              <div className="flex items-center gap-2 text-sm font-semibold text-slate-950">
                <GitBranch className="h-4 w-4 text-primary-600" />{translateCurrentLiteral("微流管理")}</div>
              <div className="mt-0.5 text-xs text-slate-500">{microflows.length} 条微流</div>
            </div>
            <PermissionButton className="primary-button h-8 text-xs" code="app:data-center:microflow:add" type="button" onClick={openCreate}>
              <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新建")}</PermissionButton>
          </div>
          <label className="relative block">
            <Search className="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-slate-400" />
            <input className="form-input h-8 pl-8 text-xs" placeholder={translateCurrentLiteral("搜索微流")} value={keyword} onChange={(event) => setKeyword(event.target.value)} />
          </label>
        </div>
        <div className="data-source-microflows-panel__list overflow-y-auto p-2">
          {microflows.map((item) => (
            <button
              className={[
                'mb-2 w-full rounded-md border p-3 text-left transition',
                selectedMicroflow?.id === item.id ? 'border-primary-300 bg-primary-50' : 'border-slate-200 bg-white hover:border-primary-200'
              ].join(' ')}
              key={item.id}
              type="button"
              onClick={() => setSelectedId(item.id)}
            >
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="truncate text-sm font-semibold text-slate-950">{item.objectName}</div>
                  <div className="mt-1 truncate text-xs text-slate-500">{item.objectCode}</div>
                </div>
                <span className="rounded-full border border-slate-200 bg-white px-2 py-0.5 text-[11px] text-slate-600">{item.status}</span>
              </div>
              {item.endpoint ? <div className="mt-2 truncate text-xs text-primary-600">{item.endpoint}</div> : null}
            </button>
          ))}
          {!microflows.length ? <div className="rounded border border-dashed border-slate-200 px-3 py-10 text-center text-sm text-slate-500">{translateCurrentLiteral("暂无微流")}</div> : null}
        </div>
      </aside>
      <main className="min-w-0 rounded-md border border-slate-200 bg-white p-3">
        {selectedMicroflow ? renderDetail(selectedMicroflow) : <EmptyDetail onCreate={openCreate} />}
      </main>
      <WorkbenchDrawerForm
        footer={(
          <>
            <button className="secondary-button" type="button" onClick={() => setForm(null)}>{translateCurrentLiteral("取消")}</button>
            <button className="primary-button" disabled={!form || saveMutation.isPending} type="button" onClick={() => saveMutation.mutate()}>{translateCurrentLiteral("保存")}</button>
          </>
        )}
        open={Boolean(form)}
        title={form?.id ? '编辑微流' : '新建微流'}
        width="md"
        onClose={() => setForm(null)}
      >
        {form ? <MicroflowForm form={form} onChange={setForm} /> : null}
      </WorkbenchDrawerForm>
    </section>
  );

  function renderDetail(item: ApplicationDataCenterObjectListItem) {
    return (
      <div className="flex h-full min-h-[520px] flex-col">
        <div className="flex flex-col gap-3 border-b border-slate-100 pb-3 lg:flex-row lg:items-start lg:justify-between">
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <span className="flex h-9 w-9 items-center justify-center rounded-md bg-primary-50 text-primary-600">
                <GitBranch className="h-4 w-4" />
              </span>
              <div className="min-w-0">
                <div className="truncate text-base font-semibold text-slate-950">{item.objectName}</div>
                <div className="mt-0.5 truncate text-xs text-slate-500">{item.objectCode} / v{item.versionNo} / {item.status}</div>
              </div>
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap gap-2">
            <button className="secondary-button h-8 text-xs" type="button" onClick={onRefresh}>
              <RefreshCcw className="h-3.5 w-3.5" />{translateCurrentLiteral("刷新")}</button>
            <PermissionButton className="secondary-button h-8 text-xs" code="app:data-center:microflow:edit" type="button" onClick={() => void openEdit(item)}>
              <Edit3 className="h-3.5 w-3.5" />{translateCurrentLiteral("编辑")}</PermissionButton>
            <PermissionButton className="secondary-button h-8 text-xs" code="app:data-center:microflow:view" type="button" onClick={() => openMicroflowConsole(item.id)}>
              <ExternalLink className="h-3.5 w-3.5" />{translateCurrentLiteral("设计器")}</PermissionButton>
            <PermissionButton className="danger-button h-8 text-xs" code="app:data-center:microflow:delete" type="button" onClick={() => deleteMicroflow(item)}>
              <Trash2 className="h-3.5 w-3.5" />{translateCurrentLiteral("删除")}</PermissionButton>
          </div>
        </div>
        <div className="mt-3 grid gap-3 md:grid-cols-4">
          <Metric label="端点" value={item.endpoint ? 1 : 0} />
          <Metric label="引用" value={item.referenceCount} />
          <Metric label="版本" value={item.versionNo} />
          <Metric label="校验" value={item.lastValidationStatus || '未校验'} />
        </div>
        {item.endpoint ? (
          <div className="mt-3 flex items-center gap-1.5 rounded border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700">
            <Route className="h-4 w-4 text-primary-500" />
            <span className="font-medium">{translateCurrentLiteral("发布端点")}</span>
            <span className="min-w-0 truncate">{item.endpoint}</span>
          </div>
        ) : null}
        <div className="mt-3 rounded-md border border-slate-200 p-3">
          <div className="mb-2 text-sm font-semibold text-slate-950">{translateCurrentLiteral("前端绑定")}</div>
          <div className="grid gap-2 text-xs text-slate-600 md:grid-cols-2">
            <ReadonlyField label="微流ID" value={item.id} />
            <ReadonlyField label="微流编码" value={item.objectCode} />
            <ReadonlyField label="选择值" value={selectedMicroflow?.id === item.id ? '当前已选中' : '未选中'} />
            <ReadonlyField label="更新时间" value={item.updatedTime || item.createdTime} />
          </div>
        </div>
      </div>
    );
  }

  function openCreate() {
    const code = normalizeMicroflowCode(`microflow_${Date.now().toString(36)}`);
    setForm({ id: null, objectCode: code, objectName: '新建微流', remark: '' });
  }

  async function openEdit(item: ApplicationDataCenterObjectListItem) {
    try {
      const response = await getApplicationDataCenterObject(resourcePath, item.id);
      setForm({
        configJson: response.data.configJson,
        endpoint: response.data.endpoint ?? '',
        id: item.id,
        objectCode: response.data.objectCode,
        objectName: response.data.objectName,
        remark: response.data.remark ?? ''
      });
    } catch (error) {
      message.error(getErrorMessage(error, '加载微流详情失败'));
    }
  }

  function deleteMicroflow(item: ApplicationDataCenterObjectListItem) {
    confirm({
      content: `确认删除微流 ${item.objectName}？`,
      confirmText: '删除',
      title: '删除微流',
      onConfirm: async () => {
        await deleteApplicationDataCenterObject(resourcePath, item.id);
        message.success('微流已删除');
        if (selectedId === item.id) {
          setSelectedId('');
        }
        onRefresh();
      }
    });
  }

  function openMicroflowConsole(microflowId?: string) {
    if (!tenantId || !appCode) {
      return;
    }

    const params = new URLSearchParams();
    params.set('dataSourceId', dataSourceId);
    if (microflowId) {
      params.set('microflowId', microflowId);
    }

    navigate(
      `/tenants/${encodeURIComponent(tenantId)}/apps/${encodeURIComponent(appCode)}/admin/data-center/microflows?${params.toString()}`
    );
  }
}

interface MicroflowFormState {
  configJson?: string;
  endpoint?: string;
  id: string | null;
  objectCode: string;
  objectName: string;
  remark: string;
}

function buildRequest(form: MicroflowFormState, dataSourceId: string): ApplicationDataCenterObjectUpsertRequest {
  const objectCode = normalizeMicroflowCode(form.objectCode || form.objectName || 'microflow');
  const definition = createDefaultMicroflowDefinition(objectCode);
  const configJson = form.configJson ?? JSON.stringify({
    ...definition,
    dataMappings: [{ expression: { dataType: 'string', kind: 'literal', value: dataSourceId }, mappingCode: 'contextDataSource', target: 'dataSourceId' }]
  });

  return {
    configJson,
    endpoint: form.endpoint ?? definition.apiEndpoints[0]?.routePath ?? '',
    environment: 'default',
    objectCode,
    objectName: form.objectName.trim() || '未命名微流',
    objectType: 'Microflow',
    remark: form.remark,
    secretConfigJson: null
  };
}

function MicroflowForm({ form, onChange }: { form: MicroflowFormState; onChange: (form: MicroflowFormState) => void }) {
  return (
    <div className="space-y-3">
      <Field label="微流编码" value={form.objectCode} onChange={(value) => onChange({ ...form, objectCode: normalizeMicroflowCode(value) })} />
      <Field label="微流名称" value={form.objectName} onChange={(value) => onChange({ ...form, objectName: value })} />
      <label className="block text-sm font-medium text-slate-700">{translateCurrentLiteral("备注")}<textarea className="form-input mt-1 min-h-24" value={form.remark} onChange={(event) => onChange({ ...form, remark: event.target.value })} />
      </label>
    </div>
  );
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return (
    <label className="block text-sm font-medium text-slate-700">
      {label}
      <input className="form-input mt-1 h-9" value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function Metric({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded border border-slate-200 bg-slate-50 px-3 py-2">
      <div className="text-xs text-slate-400">{label}</div>
      <div className="mt-1 truncate text-sm font-semibold text-slate-900">{value}</div>
    </div>
  );
}

function ReadonlyField({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded border border-slate-100 bg-slate-50 px-2 py-1">
      <div className="text-[11px] text-slate-400">{label}</div>
      <div className="mt-0.5 truncate font-medium text-slate-800">{value || '-'}</div>
    </div>
  );
}

function EmptyDetail({ onCreate }: { onCreate: () => void }) {
  return (
    <div className="flex min-h-[520px] flex-col items-center justify-center rounded-md border border-dashed border-slate-200 text-center">
      <GitBranch className="mb-3 h-8 w-8 text-slate-300" />
      <div className="text-sm font-medium text-slate-700">{translateCurrentLiteral("还没有微流")}</div>
      <button className="primary-button mt-3 h-8 text-xs" type="button" onClick={onCreate}>
        <Plus className="h-3.5 w-3.5" />{translateCurrentLiteral("新建微流")}</button>
    </div>
  );
}
