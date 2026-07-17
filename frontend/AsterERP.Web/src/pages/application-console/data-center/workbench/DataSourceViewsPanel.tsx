import { useState } from 'react';


import {
  createApplicationDataSourceView,
  deleteApplicationDataSourceView,
  getApplicationDataSourceColumns,
  listApplicationDataSourceViews,
  previewApplicationDataSourceSql,
  previewApplicationQueryPlan,
  updateApplicationDataSourceView
} from '../../../../api/application-data-center/applicationDataCenter.api';
import type { ApplicationDataCenterPreviewResponse, ApplicationDataSourceViewItem, ApplicationDataSourceViewUpsertRequest } from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../core/query/useApiQuery';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';

import { WorkbenchDrawerForm } from './components/WorkbenchDrawerForm';
import { WorkbenchResultViewer } from './components/WorkbenchResultViewer';
import { WorkbenchSqlEditor } from './components/WorkbenchSqlEditor';

interface DataSourceViewsPanelProps {
  dataSourceId: string;
  onRefresh: () => void;
}

export function DataSourceViewsPanel({ dataSourceId, onRefresh }: DataSourceViewsPanelProps) {
  const message = useMessage();
  const confirm = useConfirm();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<ApplicationDataSourceViewUpsertRequest | null>(null);
  const [preview, setPreview] = useState<ApplicationDataCenterPreviewResponse | null>(null);
  const viewsQuery = useApiQuery({
    enabled: Boolean(dataSourceId),
    queryFn: ({ signal }) => listApplicationDataSourceViews(dataSourceId, signal),
    queryKey: ['application-data-center', 'workbench', dataSourceId, 'views']
  });
  const saveMutation = useApiMutation({
    mutationFn: (request: ApplicationDataSourceViewUpsertRequest) =>
      editingId ? updateApplicationDataSourceView(dataSourceId, editingId, request) : createApplicationDataSourceView(dataSourceId, request),
    onError: (error) => message.error(getErrorMessage(error, '保存视图失败')),
    onSuccess: () => {
      message.success('视图已保存');
      closeDrawer();
      onRefresh();
    }
  });

  return (
    <section className="space-y-4 rounded-md border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-slate-950">{translateCurrentLiteral("视图管理")}</h2>
          <p className="text-sm text-slate-500">{translateCurrentLiteral("视图列表、别名、备注、SQL 查看/创建和预览。")}</p>
        </div>
        <PermissionButton className="primary-button" code="app:data-center:query-dataset:add" type="button" onClick={openCreate}>{translateCurrentLiteral("新建视图")}</PermissionButton>
      </div>
      <ObjectList
        emptyText="暂无视图"
        items={(viewsQuery.data?.data ?? []).map((item) => ({
          id: item.id,
          detail: item.remark ?? '',
          status: item.status,
          subtitle: item.viewName,
          title: item.alias,
          onDelete: () => deleteView(item),
          onEdit: () => openEdit(item),
          onPreview: () => void previewView(item)
        }))}
      />
      <WorkbenchResultViewer preview={preview} />
      <WorkbenchDrawerForm
        footer={<><button className="secondary-button" type="button" onClick={closeDrawer}>{translateCurrentLiteral("取消")}</button><button className="primary-button" disabled={!form || saveMutation.isPending} type="button" onClick={() => form && saveMutation.mutate(form)}>{translateCurrentLiteral("保存")}</button></>}
        open={Boolean(form)}
        title={editingId ? '编辑视图' : '新建视图'}
        width="xl"
        onClose={closeDrawer}
      >
        {form ? (
          <div className="space-y-3">
            <Field label="视图名" value={form.viewName} onChange={(value) => setForm({ ...form, viewName: value })} />
            <Field label="别名" value={form.alias} onChange={(value) => setForm({ ...form, alias: value })} />
            <Field label="Schema" value={form.schemaName ?? ''} onChange={(value) => setForm({ ...form, schemaName: value })} />
            <WorkbenchSqlEditor
              dataSourceId={dataSourceId}
              onExecute={async () => {
                const response = await previewApplicationDataSourceSql(dataSourceId, { maxRows: 20, sql: form.sql });
                setPreview(response.data);
                return response.data;
              }}
              value={form.sql}
              onChange={(value) => setForm({ ...form, sql: value })}
            />
            <textarea className="form-input min-h-20" placeholder={translateCurrentLiteral("备注")} value={form.remark ?? ''} onChange={(event) => setForm({ ...form, remark: event.target.value })} />
          </div>
        ) : null}
      </WorkbenchDrawerForm>
    </section>
  );

  function openCreate() {
    setEditingId(null);
    setForm({ alias: '', remark: '', schemaName: '', sql: '', viewName: '' });
  }

  function openEdit(item: ApplicationDataSourceViewItem) {
    setEditingId(item.id);
    setForm({ alias: item.alias, remark: item.remark ?? '', schemaName: item.schemaName ?? '', sql: item.sql, viewName: item.viewName });
  }

  function closeDrawer() {
    setEditingId(null);
    setForm(null);
  }

  async function previewView(item: ApplicationDataSourceViewItem) {
    try {
      const columnsResponse = await getApplicationDataSourceColumns(dataSourceId, item.viewName);
      const columns = columnsResponse.data.map((column) => ({ fieldResourceId: column.resourceId }));
      if (columns.length === 0) throw new Error('view has no QueryPlan columns');
      const tableResourceId = columnsResponse.data[0].resourceId.split(':column:')[0];
      const response = await previewApplicationQueryPlan({ accessMode: 'readOnly', auditId: null, columns, dataSourceId, filters: [], groupBy: [], having: [], joins: [], nodes: [{ alias: '', id: 'root', kind: 'view', resourceId: tableResourceId }], page: { index: 1, size: 20 }, parameters: [], riskConfirmed: false, rowLimit: 20, sorts: [], timeoutSeconds: 30 });
      setPreview(response.data.data);
    } catch (error) {
      message.error(getErrorMessage(error, '预览 SQL 失败'));
    }
  }

  function deleteView(item: ApplicationDataSourceViewItem) {
    confirm({
      content: `确认删除视图 ${item.alias}？`,
      confirmText: '删除',
      title: '删除视图',
      onConfirm: async () => {
        await deleteApplicationDataSourceView(dataSourceId, item.id);
        message.success('视图已删除');
        onRefresh();
      }
    });
  }
}

function Field({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <label className="block text-sm font-medium text-slate-700">{label}<input className="form-input mt-1 h-9" value={value} onChange={(event) => onChange(event.target.value)} /></label>;
}

function ObjectList({ emptyText, items }: { emptyText: string; items: Array<{ detail?: string; id: string; status: string; subtitle: string; title: string; onDelete: () => void; onEdit: () => void; onPreview: () => void }> }) {
  if (items.length === 0) return <div className="rounded-md border border-dashed border-slate-200 px-4 py-10 text-center text-sm text-slate-500">{emptyText}</div>;
  return <div className="space-y-2">{items.map((item) => <div className="flex flex-col gap-3 rounded-md border border-slate-200 p-3 lg:flex-row lg:items-center lg:justify-between" key={item.id}><div className="min-w-0"><div className="truncate font-medium text-slate-950">{item.title}</div><div className="mt-1 truncate text-sm text-slate-500">{item.subtitle} · {item.status}</div>{item.detail ? <div className="mt-1 truncate text-xs text-slate-400">{item.detail}</div> : null}</div><div className="flex shrink-0 flex-wrap gap-2"><button className="secondary-button h-8" type="button" onClick={item.onPreview}>{translateCurrentLiteral("预览")}</button><button className="secondary-button h-8" type="button" onClick={item.onEdit}>{translateCurrentLiteral("编辑")}</button><button className="danger-button h-8" type="button" onClick={item.onDelete}>{translateCurrentLiteral("删除")}</button></div></div>)}</div>;
}
