import { useQueries } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';

import {
  createApplicationDataCenterObject,
  diagnoseApplicationQueryPlan,
  getApplicationDataCenterObject,
  getApplicationDataCenterWorkspace,
  getApplicationDataSourceColumns,
  getApplicationDataSourceTables,
  listApplicationDataCenterObjects,
  previewApplicationQueryPlan,
  updateApplicationDataCenterObject
} from '../../../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataCenterObjectUpsertRequest,
  ApplicationDataSourceTable,
  ApplicationQueryPlanResponse
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { useI18n } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../core/query/useApiQuery';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';

import { QueryFieldSelector } from './QueryFieldSelector';
import {
  buildQueryPlanRequest,
  createInitialQueryModel,
  createNodeId,
  createSelections,
  getSupportedQueryModelJoinTypes,
  normalizeQueryModel,
  selectNextJoinTarget,
  validateQueryModel
} from './queryModelModel';
import type {
  QueryModelConfig,
  QueryModelFieldReference,
  QueryModelJoin,
  QueryModelNode,
  QueryModelOrder,
  QueryModelPredicate
} from './queryModelTypes';

interface SelectOption {
  label: string;
  nodeId: string;
  fieldResourceId: string;
  value: string;
}

export function QueryModelDesigner() {
  const [params, setParams] = useSearchParams();
  const { translate: t } = useI18n();
  const message = useMessage();
  const objectId = params.get('objectId');
  const [name, setName] = useState('New query model');
  const [code, setCode] = useState('query_model');
  const [model, setModel] = useState<QueryModelConfig>(() => createInitialQueryModel(params.get('dataSourceId') ?? ''));
  const [diagnostics, setDiagnostics] = useState<ReturnType<typeof validateQueryModel>>([]);
  const [preview, setPreview] = useState<ApplicationQueryPlanResponse | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [selectedJoinId, setSelectedJoinId] = useState<string | null>(null);
  const workspace = useApiQuery({
    queryFn: ({ signal }) => getApplicationDataCenterWorkspace({ moduleKey: 'data-source' }, signal),
    queryKey: ['query-model', 'workspace']
  });
  const datasets = useApiQuery({
    queryFn: ({ signal }) => listApplicationDataCenterObjects('query-datasets', { pageIndex: 1, pageSize: 100 }, signal),
    queryKey: ['query-model', 'datasets']
  });
  const detail = useApiQuery({
    enabled: Boolean(objectId),
    queryFn: ({ signal }) => getApplicationDataCenterObject('query-datasets', objectId ?? '', signal),
    queryKey: ['query-model', 'detail', objectId]
  });
  const tables = useApiQuery({
    enabled: Boolean(model.dataSourceId),
    queryFn: ({ signal }) => getApplicationDataSourceTables(model.dataSourceId, signal),
    queryKey: ['query-model', 'tables', model.dataSourceId]
  });
  const columnQueries = useQueries({
    queries: model.nodes.map((node) => ({
      enabled: Boolean(model.dataSourceId && node.name),
      queryFn: ({ signal }: { signal: AbortSignal }) => getApplicationDataSourceColumns(model.dataSourceId, node.name, signal),
      queryKey: ['query-model', 'columns', model.dataSourceId, node.resourceId]
    }))
  });

  useEffect(() => {
    if (!detail.data?.data || !objectId) return;
    const config = parseJson(detail.data.data.configJson);
    setName(detail.data.data.objectName);
    setCode(detail.data.data.objectCode);
    setModel(normalizeQueryModel(isRecord(config) && isRecord(config.model) ? config.model : config, params.get('dataSourceId') ?? ''));
  }, [detail.data, objectId, params]);

  useEffect(() => {
    setModel((current) => {
      let changed = false;
      const nodes = current.nodes.map((node, index) => {
        const items = columnQueries[index]?.data?.data;
        if (!items || items.length === 0 || node.columns.length > 0) return node;
        changed = true;
        return { ...node, columns: items };
      });
      if (!changed) return current;
      const firstNode = nodes[0];
      return { ...current, nodes, selections: current.selections.length === 0 && firstNode ? createSelections(firstNode) : current.selections };
    });
  }, [columnQueries]);

  const queryFields = useMemo(() => model.nodes.flatMap((node) => node.columns.map((column) => ({ label: `${node.alias}.${column.columnName}`, fieldResourceId: column.resourceId, nodeId: node.id }))), [model.nodes]);
  const fields = useMemo<SelectOption[]>(() => queryFields.map((field) => ({ ...field, value: encodeFieldReference(field.nodeId, field.fieldResourceId) })), [queryFields]);
  const sourceTables = tables.data?.data ?? [];
  const selectedDataSource = workspace.data?.data.dataSources.find((item) => item.id === model.dataSourceId);
  const supportedJoinTypes = getSupportedQueryModelJoinTypes(selectedDataSource?.objectType);
  const saveMutation = useApiMutation({
    mutationFn: (request: ApplicationDataCenterObjectUpsertRequest) =>
      objectId ? updateApplicationDataCenterObject('query-datasets', objectId, request) : createApplicationDataCenterObject('query-datasets', request),
    onSuccess: (response) => {
      setParams({ objectId: response.data.object.id });
      message.success(t('applicationConsole.dataCenter.mappingCache.saved'));
    },
    onError: (error) => message.error(getErrorMessage(error, t('applicationConsole.dataCenter.mappingCache.saveFailed')))
  });
  const diagnoseMutation = useApiMutation({
    mutationFn: diagnoseApplicationQueryPlan,
    onSuccess: (response) => setDiagnostics((current) => [
      ...current,
      ...response.data.errors.map((item) => ({ level: 'error' as const, message: item })),
      ...response.data.warnings.map((item) => ({ level: 'warning' as const, message: item }))
    ]),
    onError: (error) => message.error(getErrorMessage(error, t('applicationConsole.dataCenter.wizard.diagnostic.failed')))
  });
  const previewMutation = useApiMutation({
    mutationFn: previewApplicationQueryPlan,
    onSuccess: (response) => setPreview(response.data),
    onError: (error) => message.error(getErrorMessage(error, t('applicationConsole.dataCenter.queryModel.noPreview')))
  });

  const update = (patch: Partial<QueryModelConfig>) => setModel((current) => ({ ...current, ...patch }));
  const runDiagnosis = () => {
    const local = validateQueryModel(model, selectedDataSource?.objectType);
    setDiagnostics(local);
    if (!local.some((item) => item.level === 'error')) void diagnoseMutation.mutateAsync(buildQueryPlanRequest(model));
  };
  const previewPlan = () => {
    const local = validateQueryModel(model, selectedDataSource?.objectType);
    setDiagnostics(local);
    if (!local.some((item) => item.level === 'error')) void previewMutation.mutateAsync(buildQueryPlanRequest(model));
  };
  const save = () => {
    const local = validateQueryModel(model, selectedDataSource?.objectType);
    setDiagnostics(local);
    if (local.some((item) => item.level === 'error')) {
      message.error(t('applicationConsole.dataCenter.queryModel.runDiagnosis'));
      return;
    }
    const request: ApplicationDataCenterObjectUpsertRequest = {
      configJson: JSON.stringify({ model, queryPlan: buildQueryPlanRequest(model) }),
      objectCode: code,
      objectName: name,
      objectType: 'QueryView',
      endpoint: '',
      environment: 'default',
      ownerUserId: '',
      remark: '',
      confirmedRiskFields: [],
      secretConfigJson: null
    };
    void saveMutation.mutateAsync(request);
  };
  const selectTable = (table: ApplicationDataSourceTable) => {
    const node: QueryModelNode = {
      id: createNodeId(model.dataSourceId, table.resourceId),
      alias: table.tableName.slice(0, 1).toLowerCase(),
      columns: [],
      kind: table.tableType.toLowerCase().includes('view') ? 'view' : 'table',
      name: table.tableName,
      resourceId: table.resourceId,
      x: 80,
      y: 80
    };
    update({ nodes: [node], selections: [], joins: [], where: [], having: [], groupBy: [], orderBy: [] });
    setSelectedNodeId(node.id);
    setSelectedJoinId(null);
  };
  const addJoin = () => {
    const target = selectNextJoinTarget(sourceTables, model.nodes);
    if (!target || !model.nodes[0]) return;
    const resourceInstance = model.nodes.filter((node) => node.resourceId === target.resourceId).length + 1;
    const node: QueryModelNode = {
      id: createNodeId(model.dataSourceId, target.resourceId, resourceInstance),
      alias: createNodeAlias(target.tableName, model.nodes),
      columns: [],
      kind: target.tableType.toLowerCase().includes('view') ? 'view' : 'table',
      name: target.tableName,
      resourceId: target.resourceId,
      x: 360,
      y: 80
    };
    const join: QueryModelJoin = {
      id: `join:${model.joins.length + 1}`,
      type: 'inner',
      leftNodeId: model.nodes[0].id,
      leftFieldResourceId: '',
      rightNodeId: node.id,
      rightFieldResourceId: ''
    };
    update({ nodes: [...model.nodes, node], joins: [...model.joins, join] });
    setSelectedJoinId(join.id);
  };

  return (
    <div className="flex min-h-[calc(100vh-8rem)] flex-col gap-3 p-4">
      <header className="flex flex-wrap items-center justify-between gap-3 rounded border bg-white p-4">
        <div><h1 className="text-lg font-semibold">{t('applicationConsole.dataCenter.queryModel.title')}</h1><p className="text-xs text-slate-500">{t('applicationConsole.dataCenter.queryModel.description')}</p></div>
        <div className="flex gap-2"><button className="secondary-button" type="button" onClick={runDiagnosis}>{t('applicationConsole.dataCenter.queryModel.diagnose')}</button><button className="secondary-button" type="button" onClick={previewPlan}>{t('applicationConsole.dataCenter.queryModel.preview')}</button><button className="primary-button" type="button" onClick={save}>{t('applicationConsole.dataCenter.queryModel.save')}</button></div>
      </header>
      <div className="grid flex-1 gap-3 xl:grid-cols-[220px_minmax(0,1fr)_320px]">
        <aside className="rounded border bg-white p-3" aria-label={t('applicationConsole.dataCenter.queryModel.models')}><h2 className="mb-2 text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.models')}</h2>{(datasets.data?.data.items ?? []).map((item) => <button className="block w-full rounded px-2 py-2 text-left text-xs hover:bg-slate-50" key={item.id} type="button" aria-label={item.objectName} onClick={() => setParams({ objectId: item.id })}>{item.objectName}</button>)}<button className="secondary-button mt-3 w-full" type="button" onClick={() => { setName('New query model'); setCode('query_model'); setModel(createInitialQueryModel(params.get('dataSourceId') ?? '')); setParams({ dataSourceId: params.get('dataSourceId') ?? '' }); }}>{t('applicationConsole.dataCenter.queryModel.new')}</button></aside>
        <main className="space-y-3 rounded border bg-slate-50 p-3">
          <section className="grid gap-2 rounded border bg-white p-3 md:grid-cols-2"><LabeledInput id="query-model-name" label={t('applicationConsole.dataCenter.queryModel.name')} value={name} onChange={setName} /><LabeledInput id="query-model-code" label={t('applicationConsole.dataCenter.queryModel.code')} value={code} onChange={setCode} /><LabeledSelect id="query-model-source" label={t('applicationConsole.dataCenter.queryModel.dataSource')} value={model.dataSourceId} onChange={(value) => update({ dataSourceId: value, nodes: [], selections: [], joins: [], where: [], having: [], groupBy: [], orderBy: [] })}><option value="">{t('applicationConsole.dataCenter.queryModel.select')}</option>{(workspace.data?.data.dataSources ?? []).map((item) => <option key={item.id} value={item.id}>{item.objectName}</option>)}</LabeledSelect><LabeledSelect id="query-model-table" label={t('applicationConsole.dataCenter.queryModel.table')} value={model.nodes[0]?.resourceId ?? ''} onChange={(value) => { const table = sourceTables.find((item) => item.resourceId === value); if (table) selectTable(table); }}><option value="">{t('applicationConsole.dataCenter.queryModel.select')}</option>{sourceTables.map((item) => <option key={item.resourceId} value={item.resourceId}>{item.tableName} ({item.tableType})</option>)}</LabeledSelect></section>
          <section className="rounded border bg-white p-3"><div className="flex items-center justify-between"><h2 className="text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.nodesAndJoins')}</h2><button className="secondary-button h-8 text-xs" disabled={supportedJoinTypes.length === 0} type="button" onClick={addJoin}>{t('applicationConsole.dataCenter.queryModel.addJoin')}</button></div><div className="my-3 flex flex-wrap gap-2" role="listbox" aria-label={t('applicationConsole.dataCenter.queryModel.nodesAndJoins')}>{model.nodes.map((node) => <div className="rounded border border-blue-200 bg-blue-50 p-2 text-xs" key={node.id} role="option" tabIndex={0} aria-selected={selectedNodeId === node.id} aria-label={t('applicationConsole.dataCenter.queryModel.node').replace('{name}', node.name)} onClick={() => setSelectedNodeId(node.id)} onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') setSelectedNodeId(node.id); }}>{node.alias} / {node.name}<div className="text-[10px]">{node.resourceId} · ({node.x},{node.y})</div></div>)}</div>{model.joins.map((join) => <JoinRow key={join.id} join={join} nodes={model.nodes} selected={selectedJoinId === join.id} supportedJoinTypes={supportedJoinTypes} usedJoinTargetIds={model.joins.filter((item) => item.id !== join.id).map((item) => item.rightNodeId)} onSelect={() => setSelectedJoinId(join.id)} onChange={(next) => update({ joins: model.joins.map((item) => item.id === join.id ? next : item) })} onRemove={() => update({ joins: model.joins.filter((item) => item.id !== join.id) })} t={t} />)}</section>
          <QueryFieldSelector selections={model.selections} fields={queryFields} onChange={(selections) => update({ selections })} t={t} />
          <PredicateSection title="WHERE" rows={model.where} fields={fields} parameters={model.parameters} onChange={(where) => update({ where })} t={t} />
          <PredicateSection title="HAVING" rows={model.having} fields={fields} parameters={model.parameters} onChange={(having) => update({ having })} t={t} />
          <GroupBySection values={model.groupBy} fields={fields} onChange={(groupBy) => update({ groupBy })} t={t} />
          <OrderSection values={model.orderBy} fields={fields} onChange={(orderBy) => update({ orderBy })} t={t} />
        </main>
        <aside className="space-y-3"><DiagnosticPanel items={diagnostics} t={t} /><ParameterSection model={model} onChange={update} t={t} /><PreviewPanel result={preview} t={t} /></aside>
      </div>
    </div>
  );
}

function LabeledInput({ id, label, value, onChange, placeholder }: { id: string; label: string; value: string; onChange: (value: string) => void; placeholder?: string }) { return <div><label className="text-xs" htmlFor={id}>{label}</label><input id={id} className="form-input mt-1 w-full" placeholder={placeholder} value={value} onChange={(event) => onChange(event.target.value)} /></div>; }
function LabeledSelect({ id, label, value, onChange, children }: { id: string; label: string; value: string; onChange: (value: string) => void; children: ReactNode }) { return <div><label className="text-xs" htmlFor={id}>{label}</label><select id={id} className="form-input mt-1 w-full" value={value} onChange={(event) => onChange(event.target.value)}>{children}</select></div>; }
function fieldOptions(fields: SelectOption[]) { return <><option value="">Select</option>{fields.map((field) => <option key={field.value} value={field.value}>{field.label}</option>)}</>; }
function parameterOptions(parameters: QueryModelConfig['parameters']) { return <><option value="">Select parameter</option>{parameters.map((parameter) => <option key={parameter.resourceId} value={parameter.resourceId}>{parameter.name}</option>)}</>; }
function PredicateSection({ title, rows, fields, parameters, onChange, t }: { title: string; rows: QueryModelPredicate[]; fields: SelectOption[]; parameters: QueryModelConfig['parameters']; onChange: (items: QueryModelPredicate[]) => void; t: (key: string) => string }) { return <section className="rounded border bg-white p-3"><div className="flex justify-between"><h2 className="text-sm font-semibold">{title}</h2><button className="secondary-button h-8 text-xs" type="button" onClick={() => onChange([...rows, { id: `${title}:${Date.now()}`, fieldResourceId: '', nodeId: '', operator: 'eq', parameterResourceId: '' }])}>{t('applicationConsole.dataCenter.queryModel.addCondition')}</button></div>{rows.map((item, index) => <div className="mt-2 grid gap-2 md:grid-cols-3" key={item.id}><LabeledSelect id={`${title}-field-${index}`} label={t('applicationConsole.dataCenter.queryModel.field')} value={item.nodeId && item.fieldResourceId ? encodeFieldReference(item.nodeId, item.fieldResourceId) : ''} onChange={(value) => onChange(rows.map((row, rowIndex) => rowIndex === index ? { ...row, ...(decodeFieldReference(value) ?? { nodeId: '', fieldResourceId: '' }) } : row))}>{fieldOptions(fields)}</LabeledSelect><LabeledSelect id={`${title}-operator-${index}`} label={t('applicationConsole.dataCenter.queryModel.operator')} value={item.operator} onChange={(value) => onChange(rows.map((row, rowIndex) => rowIndex === index ? { ...row, operator: value } : row))}><option value="eq">equals</option><option value="contains">contains</option><option value="gt">greater than</option><option value="gte">greater or equal</option><option value="lt">less than</option><option value="lte">less than</option><option value="isNull">is null</option><option value="isNotNull">is not null</option></LabeledSelect><LabeledSelect id={`${title}-parameter-${index}`} label={t('applicationConsole.dataCenter.queryModel.parameterName')} value={item.parameterResourceId} onChange={(value) => onChange(rows.map((row, rowIndex) => rowIndex === index ? { ...row, parameterResourceId: value } : row))}>{parameterOptions(parameters)}</LabeledSelect></div>)}</section>; }
function GroupBySection({ values, fields, onChange, t }: { values: QueryModelFieldReference[]; fields: SelectOption[]; onChange: (values: QueryModelFieldReference[]) => void; t: (key: string) => string }) { return <section className="rounded border bg-white p-3"><h2 className="text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.groupBy')}</h2>{values.map((field, index) => <LabeledSelect id={`group-by-${index}`} label={`${t('applicationConsole.dataCenter.queryModel.field')} ${index + 1}`} key={`${field.nodeId}:${field.fieldResourceId}-${index}`} value={field.nodeId && field.fieldResourceId ? encodeFieldReference(field.nodeId, field.fieldResourceId) : ''} onChange={(value) => onChange(values.map((item, itemIndex) => itemIndex === index ? (decodeFieldReference(value) ?? { nodeId: '', fieldResourceId: '' }) : item))}>{fieldOptions(fields)}</LabeledSelect>)}<button className="secondary-button mt-2 h-8 text-xs" type="button" onClick={() => onChange([...values, { nodeId: '', fieldResourceId: '' }])}>{t('applicationConsole.dataCenter.queryModel.addGroupField')}</button></section>; }
function OrderSection({ values, fields, onChange, t }: { values: QueryModelOrder[]; fields: SelectOption[]; onChange: (values: QueryModelOrder[]) => void; t: (key: string) => string }) { return <section className="rounded border bg-white p-3"><h2 className="text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.orderBy')}</h2>{values.map((item, index) => <div className="mt-2 grid gap-2 md:grid-cols-2" key={item.id}><LabeledSelect id={`order-field-${index}`} label={t('applicationConsole.dataCenter.queryModel.field')} value={item.nodeId && item.fieldResourceId ? encodeFieldReference(item.nodeId, item.fieldResourceId) : ''} onChange={(value) => onChange(values.map((row, rowIndex) => rowIndex === index ? { ...row, ...(decodeFieldReference(value) ?? { nodeId: '', fieldResourceId: '' }) } : row))}>{fieldOptions(fields)}</LabeledSelect><LabeledSelect id={`order-direction-${index}`} label="Direction" value={item.direction} onChange={(value) => onChange(values.map((row, rowIndex) => rowIndex === index ? { ...row, direction: value === 'desc' ? 'desc' : 'asc' } : row))}><option value="asc">ASC</option><option value="desc">DESC</option></LabeledSelect></div>)}<button className="secondary-button mt-2 h-8 text-xs" type="button" onClick={() => onChange([...values, { id: `order:${Date.now()}`, fieldResourceId: '', nodeId: '', direction: 'asc' }])}>Add order</button></section>; }
function JoinRow({ join, nodes, selected, supportedJoinTypes, usedJoinTargetIds, onSelect, onChange, onRemove, t }: { join: QueryModelJoin; nodes: QueryModelNode[]; selected: boolean; supportedJoinTypes: QueryModelJoin['type'][]; usedJoinTargetIds: string[]; onSelect: () => void; onChange: (join: QueryModelJoin) => void; onRemove: () => void; t: (key: string) => string }) { const leftFields = nodes.find((node) => node.id === join.leftNodeId)?.columns ?? []; const rightFields = nodes.find((node) => node.id === join.rightNodeId)?.columns ?? []; const rightNodeOptions = nodes.filter((node) => node.id === join.rightNodeId || (node.id !== join.leftNodeId && !usedJoinTargetIds.includes(node.id))); return <div className="mt-2 grid gap-2 rounded border border-amber-200 bg-amber-50 p-2 md:grid-cols-5" role="group" tabIndex={0} aria-selected={selected} aria-label={t('applicationConsole.dataCenter.queryModel.connection').replace('{id}', join.id)} onClick={onSelect} onKeyDown={(event) => { if (event.key === 'Enter' || event.key === ' ') onSelect(); }}><LabeledSelect id={`join-type-${join.id}`} label={t('applicationConsole.dataCenter.queryModel.joinType')} value={join.type} onChange={(value) => onChange({ ...join, type: value as QueryModelJoin['type'] })}>{supportedJoinTypes.map((type) => <option key={type} value={type}>{type.toUpperCase()}</option>)}</LabeledSelect><LabeledSelect id={`join-left-${join.id}`} label={t('applicationConsole.dataCenter.queryModel.leftNode')} value={join.leftNodeId} onChange={(value) => onChange({ ...join, leftNodeId: value, leftFieldResourceId: '' })}>{nodes.map((node) => <option key={node.id} value={node.id}>{node.alias}</option>)}</LabeledSelect><LabeledSelect id={`join-left-field-${join.id}`} label={t('applicationConsole.dataCenter.queryModel.leftField')} value={join.leftFieldResourceId} onChange={(value) => onChange({ ...join, leftFieldResourceId: value })}>{leftFields.map((field) => <option key={field.resourceId} value={field.resourceId}>{field.columnName}</option>)}</LabeledSelect><LabeledSelect id={`join-right-${join.id}`} label={t('applicationConsole.dataCenter.queryModel.rightNode')} value={join.rightNodeId} onChange={(value) => onChange({ ...join, rightNodeId: value, rightFieldResourceId: '' })}>{rightNodeOptions.map((node) => <option key={node.id} value={node.id}>{node.alias}</option>)}</LabeledSelect><div className="flex items-end gap-2"><LabeledSelect id={`join-right-field-${join.id}`} label={t('applicationConsole.dataCenter.queryModel.rightField')} value={join.rightFieldResourceId} onChange={(value) => onChange({ ...join, rightFieldResourceId: value })}>{rightFields.map((field) => <option key={field.resourceId} value={field.resourceId}>{field.columnName}</option>)}</LabeledSelect><button className="text-xs text-red-600" type="button" aria-label={t('applicationConsole.dataCenter.queryModel.remove')} onClick={onRemove}>{t('applicationConsole.dataCenter.queryModel.remove')}</button></div></div>; }
function ParameterSection({ model, onChange, t }: { model: QueryModelConfig; onChange: (patch: Partial<QueryModelConfig>) => void; t: (key: string) => string }) { return <section className="rounded border bg-white p-3"><h2 className="text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.parametersPaging')}</h2><div className="mt-2 grid grid-cols-2 gap-2"><LabeledInput id="page-index" label={t('applicationConsole.dataCenter.queryModel.pageIndex')} value={String(model.page.index)} onChange={(value) => onChange({ page: { ...model.page, index: Number(value) } })} /><LabeledInput id="page-size" label={t('applicationConsole.dataCenter.queryModel.pageSize')} value={String(model.page.size)} onChange={(value) => onChange({ page: { ...model.page, size: Number(value) } })} /></div><button className="secondary-button mt-2 h-8 text-xs" type="button" onClick={() => onChange({ parameters: [...model.parameters, { resourceId: `query-parameter:${Date.now()}`, name: `parameter${model.parameters.length + 1}`, type: 'string', value: '' }] })}>{t('applicationConsole.dataCenter.queryModel.addParameter')}</button>{model.parameters.map((item, index) => <div className="mt-2 grid grid-cols-3 gap-2" key={item.resourceId}><LabeledInput id={`parameter-name-${index}`} label={t('applicationConsole.dataCenter.queryModel.parameterName')} value={item.name} onChange={(value) => onChange({ parameters: model.parameters.map((row, rowIndex) => rowIndex === index ? { ...row, name: value } : row) })} /><LabeledInput id={`parameter-type-${index}`} label={t('applicationConsole.dataCenter.queryModel.parameterType')} value={item.type} onChange={(value) => onChange({ parameters: model.parameters.map((row, rowIndex) => rowIndex === index ? { ...row, type: value } : row) })} /><LabeledInput id={`parameter-value-${index}`} label={t('applicationConsole.dataCenter.queryModel.parameterValue')} value={String(item.value ?? '')} onChange={(value) => onChange({ parameters: model.parameters.map((row, rowIndex) => rowIndex === index ? { ...row, value } : row) })} /></div>)}</section>; }
function DiagnosticPanel({ items, t }: { items: ReturnType<typeof validateQueryModel>; t: (key: string) => string }) { return <section className="rounded border bg-white p-3" aria-live="polite"><h2 className="text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.diagnosis')}</h2>{items.length === 0 ? <p className="mt-2 text-xs text-slate-500">{t('applicationConsole.dataCenter.queryModel.runDiagnosis')}</p> : items.map((item, index) => <p className={`mt-2 rounded p-2 text-xs ${item.level === 'error' ? 'bg-red-50 text-red-700' : 'bg-amber-50 text-amber-700'}`} key={`${item.message}-${index}`}>{item.message}</p>)}</section>; }
function PreviewPanel({ result, t }: { result: ApplicationQueryPlanResponse | null; t: (key: string) => string }) { return <section className="rounded border bg-white p-3"><h2 className="text-sm font-semibold">{t('applicationConsole.dataCenter.queryModel.preview')}</h2>{result ? <p className="mt-2 text-xs text-slate-600">{t('applicationConsole.dataCenter.queryModel.previewSummary').replace('{rows}', String(result.data.rows.length)).replace('{total}', String(result.total)).replace('{auditId}', result.auditId)}</p> : <p className="mt-2 text-xs text-slate-500">{t('applicationConsole.dataCenter.queryModel.noPreview')}</p>}</section>; }
function parseJson(value: string): unknown { try { return JSON.parse(value); } catch { return {}; } }
function isRecord(value: unknown): value is Record<string, unknown> { return Boolean(value && typeof value === 'object' && !Array.isArray(value)); }
function createNodeAlias(tableName: string, nodes: readonly QueryModelNode[]): string {
  const base = (tableName.trim().charAt(0) || 't').toLowerCase();
  const occupied = new Set(nodes.map((node) => node.alias.toLowerCase()));
  if (!occupied.has(base)) return base;
  let suffix = 2;
  while (occupied.has(`${base}${suffix}`)) suffix += 1;
  return `${base}${suffix}`;
}
function encodeFieldReference(nodeId: string, fieldResourceId: string): string { return JSON.stringify([nodeId, fieldResourceId]); }
function decodeFieldReference(value: string): QueryModelFieldReference | undefined { try { const parsed: unknown = JSON.parse(value); return Array.isArray(parsed) && parsed.length === 2 && typeof parsed[0] === 'string' && typeof parsed[1] === 'string' ? { nodeId: parsed[0], fieldResourceId: parsed[1] } : undefined; } catch { return undefined; } }
