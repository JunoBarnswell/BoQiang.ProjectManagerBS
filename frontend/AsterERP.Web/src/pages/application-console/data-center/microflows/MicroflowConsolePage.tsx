import { useQueryClient } from '@tanstack/react-query';
import {
  ArrowLeft,
  Box,
  CheckCircle2,
  ChevronDown,
  CircleCheck,
  Clock3,
  Ellipsis,
  GitBranch,
  Grid2X2,
  Play,
  Rocket,
  Save,
  Settings,
  Workflow
} from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';

import {
  createApplicationDataCenterObject,
  getApplicationDataCenterObject,
  listApplicationDataCenterObjects,
  listApplicationMicroflowRevisions,
  publishApplicationMicroflowRevision,
  restoreApplicationMicroflowRevision,
  updateApplicationDataCenterObject,
  validateApplicationMicroflowRevision
} from '../../../../api/application-data-center/applicationDataCenter.api';
import type {
  ApplicationDataCenterObjectDetail,
  MicroflowDefinition
} from '../../../../api/application-data-center/applicationDataCenter.types';
import { translateCurrentLiteral } from '../../../../core/i18n/I18nProvider';
import { useApiMutation } from '../../../../core/query/useApiMutation';
import { useApiQuery } from '../../../../core/query/useApiQuery';
import { PermissionButton } from '../../../../shared/auth/PermissionButton';
import { useConfirm } from '../../../../shared/feedback/useConfirm';
import { useMessage } from '../../../../shared/feedback/useMessage';
import { getErrorMessage } from '../../../../shared/utils/errorMessage';
import { ApplicationConsolePageFrame } from '../../ApplicationConsolePageFrame';
import { WorkspaceEmptyState } from '../../workspace-shell/WorkspaceEmptyState';

import { MicroflowCanvas } from './MicroflowCanvas';
import { findMicroflowEdge, findMicroflowNode } from './microflowCanvasModel';
import { createDefaultMicroflowDefinition, normalizeMicroflowCode } from './microflowDefaults';
import { normalizeMicroflowDefinitionForSave } from './microflowDefinitionNormalizer';
import { MicroflowEdgeConfigDialog } from './MicroflowEdgeConfigDialog';
import { MicroflowNodeConfigEditor } from './MicroflowNodeConfigEditor';
import { MicroflowPreviewDialog } from './MicroflowPreviewDialog';

interface MicroflowConsolePageProps {
  contextDataSourceId?: string;
}

const resourcePath = 'microflows' as const;

export function MicroflowConsolePage({ contextDataSourceId }: MicroflowConsolePageProps) {
  const queryClient = useQueryClient();
  const message = useMessage();
  const confirm = useConfirm();
  const [searchParams, setSearchParams] = useSearchParams();
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [draftMeta, setDraftMeta] = useState({ objectCode: '', objectName: '', remark: '' });
  const [definition, setDefinition] = useState<MicroflowDefinition | null>(null);
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [previewOpen, setPreviewOpen] = useState(false);
  const [executeNonce, setExecuteNonce] = useState(0);
  const [nodeConfigEditorNodeId, setNodeConfigEditorNodeId] = useState<string | null>(null);
  const [edgeConfigEditorEdgeId, setEdgeConfigEditorEdgeId] = useState<string | null>(null);
  const [selectedRevisionId, setSelectedRevisionId] = useState<string | null>(null);
  const [isDirty, setIsDirty] = useState(false);

  const listQuery = useApiQuery({
    keepPreviousData: true,
    queryFn: ({ signal }) => listApplicationDataCenterObjects(resourcePath, { keyword: '', pageIndex: 1, pageSize: 100 }, signal),
    queryKey: ['application-data-center', resourcePath, 'list', 'designer']
  });
  const detailQuery = useApiQuery({
    enabled: Boolean(selectedId),
    gcTimeMs: 0,
    queryFn: ({ signal }) => getApplicationDataCenterObject(resourcePath, selectedId ?? '', signal),
    queryKey: buildMicroflowDetailQueryKey(selectedId),
    refetchOnMount: 'always',
    staleTimeMs: 0
  });
  const revisionsQuery = useApiQuery({
    enabled: Boolean(selectedId),
    queryFn: ({ signal }) => listApplicationMicroflowRevisions(selectedId ?? '', signal),
    queryKey: ['application-data-center', resourcePath, 'versions', selectedId],
    staleTimeMs: 0
  });
  const rows = useMemo(() => listQuery.data?.data.items ?? [], [listQuery.data?.data.items]);
  const selectedDetail = detailQuery.data?.data ?? null;
  const revisions = useMemo(() => revisionsQuery.data?.data ?? [], [revisionsQuery.data?.data]);
  const currentRevision = useMemo(() => revisions.find((item) => item.isCurrent) ?? null, [revisions]);
  const selectedRevision = useMemo(
    () => selectedRevisionId ? revisions.find((item) => item.id === selectedRevisionId) ?? null : currentRevision,
    [currentRevision, revisions, selectedRevisionId]
  );
  const viewingHistory = Boolean(selectedRevisionId && selectedRevision && !selectedRevision.isCurrent);
  const routeMicroflowId = searchParams.get('microflowId')?.trim() ?? '';
  const selectMicroflow = useCallback(
    (id: string) => {
      queryClient.removeQueries({ exact: true, queryKey: buildMicroflowDetailQueryKey(id) });
      setSelectedId(id);
      setDefinition(null);
      setPreviewOpen(false);
      setSelectedEdgeId(null);
      setSelectedNodeId(null);
      setNodeConfigEditorNodeId(null);
      setEdgeConfigEditorEdgeId(null);
      setSelectedRevisionId(null);
      setIsDirty(false);
      const next = new URLSearchParams(searchParams);
      next.set('microflowId', id);
      setSearchParams(next, { replace: true });
    },
    [queryClient, searchParams, setSearchParams]
  );

  const activeConfigNodeId = nodeConfigEditorNodeId ?? selectedNodeId;
  const nodeConfigEditorNode = useMemo(
    () => definition ? findMicroflowNode(definition, activeConfigNodeId) : null,
    [activeConfigNodeId, definition]
  );
  const edgeConfigEditorEdge = useMemo(
    () => definition ? findMicroflowEdge(definition, edgeConfigEditorEdgeId) : null,
    [definition, edgeConfigEditorEdgeId]
  );
  const loadDetail = useCallback(
    (detail: ApplicationDataCenterObjectDetail) => {
      setDraftMeta({ objectCode: detail.objectCode, objectName: detail.objectName, remark: detail.remark ?? '' });
      try {
        setDefinition(applyContextDataSource(JSON.parse(detail.configJson) as MicroflowDefinition, contextDataSourceId));
        setSelectedEdgeId(null);
        setSelectedNodeId(null);
        setNodeConfigEditorNodeId(null);
        setEdgeConfigEditorEdgeId(null);
      } catch {
        setDefinition(createContextualMicroflowDefinition(detail.objectCode, contextDataSourceId));
        setSelectedEdgeId(null);
        setSelectedNodeId(null);
        setNodeConfigEditorNodeId(null);
        setEdgeConfigEditorEdgeId(null);
      }
    },
    [contextDataSourceId]
  );

  const saveMutation = useApiMutation({
    mutationFn: () => {
      const code = normalizeMicroflowCode(draftMeta.objectCode || draftMeta.objectName || 'microflow');
      const request = {
        configJson: JSON.stringify(normalizeMicroflowDefinitionForSave(definition ?? createDefaultMicroflowDefinition(code))),
        endpoint: definition?.apiEndpoints[0]?.routePath ?? '',
        environment: 'default',
        objectCode: code,
        objectName: draftMeta.objectName.trim() || '未命名微流',
        objectType: 'Microflow',
        remark: draftMeta.remark,
        secretConfigJson: null
      };
      return selectedId
        ? updateApplicationDataCenterObject(resourcePath, selectedId, request)
        : createApplicationDataCenterObject(resourcePath, request);
    },
    onError: (error) => message.error(getErrorMessage(error, '保存微流失败')),
    onSuccess: async (response) => {
      message.success('微流已保存');
      setSelectedId(response.data.object.id);
      setSelectedRevisionId(null);
      setIsDirty(false);
      await refresh();
    }
  });
  const validateMutation = useApiMutation({
    mutationFn: () => validateApplicationMicroflowRevision(selectedId ?? '', { revisionId: currentRevision?.id ?? '' }),
    onError: (error) => message.error(getErrorMessage(error, '微流校验失败')),
    onSuccess: async (response) => {
      message[response.data.success ? 'success' : 'error'](response.data.message);
      await revisionsQuery.refetch();
    }
  });
  const publishMutation = useApiMutation({
    mutationFn: () => publishApplicationMicroflowRevision(selectedId ?? '', { revisionId: currentRevision?.id ?? '' }),
    onError: (error) => message.error(getErrorMessage(error, '发布微流失败')),
    onSuccess: async () => {
      message.success('微流已发布，接口端点已同步');
      await refresh();
    }
  });
  const restoreMutation = useApiMutation({
    mutationFn: (revisionId: string) => restoreApplicationMicroflowRevision(selectedId ?? '', { revisionId }),
    onError: (error) => message.error(getErrorMessage(error, '恢复历史版本失败')),
    onSuccess: async () => {
      message.success('已恢复为新的草稿版本，请重新校验后发布');
      setSelectedRevisionId(null);
      setIsDirty(false);
      await refresh();
    }
  });
  useEffect(() => {
    if (selectedDetail && selectedDetail.id === selectedId) {
      if (!viewingHistory) {
        loadDetail(selectedDetail);
        setIsDirty(false);
      }
    }
  }, [loadDetail, selectedDetail, selectedId, viewingHistory]);

  useEffect(() => {
    if (!viewingHistory || !selectedRevision) {
      return;
    }
    try {
      setDefinition(JSON.parse(selectedRevision.configJson) as MicroflowDefinition);
      setSelectedEdgeId(null);
      setSelectedNodeId(null);
      setNodeConfigEditorNodeId(null);
      setEdgeConfigEditorEdgeId(null);
      setIsDirty(false);
    } catch {
      message.error('历史版本配置无效，无法打开。');
    }
  }, [message, selectedRevision, viewingHistory]);

  useEffect(() => {
    if (!routeMicroflowId || selectedId === routeMicroflowId) {
      return;
    }

    setSelectedId(routeMicroflowId);
    queryClient.removeQueries({ exact: true, queryKey: buildMicroflowDetailQueryKey(routeMicroflowId) });
    setDefinition(null);
    setSelectedEdgeId(null);
    setSelectedNodeId(null);
    setNodeConfigEditorNodeId(null);
    setEdgeConfigEditorEdgeId(null);
  }, [queryClient, routeMicroflowId, selectedId]);

  useEffect(() => {
    if (routeMicroflowId || selectedId || definition || rows.length === 0) {
      return;
    }

    selectMicroflow(rows[0].id);
  }, [definition, routeMicroflowId, rows, selectedId, selectMicroflow]);

  return (
    <ApplicationConsolePageFrame density="compact" hideDescription pageKey="data-center">
      {() => (
        <div className="microflow-designer-shell">
          <header className="microflow-designer-topbar">
            <div className="microflow-designer-topbar__left">
              <button className="microflow-designer-icon-button" title={translateCurrentLiteral("返回")} type="button" onClick={() => window.history.back()}>
                <ArrowLeft size={17} />
              </button>
              <span className="microflow-designer-app-icon">
                <Workflow size={16} />
              </span>
              <div className="microflow-designer-title">
                <strong>{draftMeta.objectName || '操作体验配置'} / 工作流节点配置</strong>
                <span title={draftMeta.objectCode || 'microflow'}>{draftMeta.objectCode || 'microflow'}</span>
              </div>
              <span className="microflow-designer-saved">
                <CircleCheck size={13} />{isDirty ? translateCurrentLiteral('未保存') : translateCurrentLiteral('已保存')}</span>
            </div>
            <div className="microflow-designer-topbar__actions">
              <select
                aria-label={translateCurrentLiteral('微流版本')}
                className="microflow-designer-version"
                disabled={!selectedId || revisionsQuery.isFetching}
                value={selectedRevisionId ?? ''}
                onChange={(event) => setSelectedRevisionId(event.target.value || null)}
              >
                <option value="">{currentRevision ? `v${currentRevision.revisionNo} ${currentRevision.status === 'Published' ? '已发布' : '草稿'}` : '未保存'}</option>
                {revisions.filter((item) => !item.isCurrent).map((item) => (
                  <option key={item.id} value={item.id}>v{item.revisionNo} {item.status === 'Published' ? '已发布' : '历史草稿'}</option>
                ))}
              </select>
              {viewingHistory && selectedRevision ? (
                <PermissionButton className="microflow-designer-action microflow-designer-action--secondary" code="app:data-center:microflow:edit" disabled={restoreMutation.isPending} type="button" onClick={() => restoreMutation.mutate(selectedRevision.id)}>
                  <Save size={14} />{translateCurrentLiteral('恢复为新草稿')}</PermissionButton>
              ) : null}
              <PermissionButton className="microflow-designer-action microflow-designer-action--secondary" code="app:data-center:microflow:test" disabled={!selectedId || !currentRevision || isDirty || viewingHistory || validateMutation.isPending} type="button" onClick={() => validateMutation.mutate()}>
                <CheckCircle2 size={14} />{translateCurrentLiteral("校验")}</PermissionButton>
              <PermissionButton className="microflow-designer-action microflow-designer-action--secondary" code={selectedId ? 'app:data-center:microflow:edit' : 'app:data-center:microflow:add'} disabled={!definition || viewingHistory || saveMutation.isPending} type="button" onClick={() => saveMutation.mutate()}>
                <Save size={14} />{translateCurrentLiteral("保存")}</PermissionButton>
              <PermissionButton className="microflow-designer-action microflow-designer-action--primary" code="app:data-center:microflow:preview" disabled={!selectedId || !definition} type="button" onClick={executeMicroflow}>
                <Play size={14} />{translateCurrentLiteral("测试运行")}</PermissionButton>
              <PermissionButton className="microflow-designer-action microflow-designer-action--primary" code="app:data-center:microflow:publish" disabled={!selectedId || !currentRevision || currentRevision.validationStatus !== 'Passed' || isDirty || viewingHistory || publishMutation.isPending} type="button" onClick={requestPublish}>
                <Rocket size={14} />{translateCurrentLiteral("发布")}<ChevronDown size={13} />
              </PermissionButton>
              <button className="microflow-designer-icon-button" title={translateCurrentLiteral("更多")} type="button">
                <Ellipsis size={17} />
              </button>
            </div>
          </header>
          <div className="microflow-designer-body">
            <nav className="microflow-designer-rail" aria-label="工作流设计器导航">
              <button className="microflow-designer-rail__item" type="button" title={translateCurrentLiteral("资源")}>
                <Box size={17} />
              </button>
              <button className="microflow-designer-rail__item microflow-designer-rail__item--active" type="button" title={translateCurrentLiteral("工作流")}>
                <GitBranch size={17} />
              </button>
              <button className="microflow-designer-rail__item" type="button" title={translateCurrentLiteral("节点库")}>
                <Grid2X2 size={17} />
              </button>
              <button className="microflow-designer-rail__item" type="button" title={translateCurrentLiteral("运行记录")}>
                <Clock3 size={17} />
              </button>
              <span className="microflow-designer-rail__spacer" />
              <button className="microflow-designer-rail__item" type="button" title={translateCurrentLiteral("设置")}>
                <Settings size={17} />
              </button>
            </nav>
            <main className="microflow-designer-canvas-pane">
              {definition ? (
                <MicroflowCanvas
                  definition={definition}
                  selectedEdgeId={selectedEdgeId}
                  selectedNodeId={selectedNodeId}
                  readOnly={viewingHistory}
                  onChange={updateDefinition}
                  onEditEdgeConfig={viewingHistory ? undefined : setEdgeConfigEditorEdgeId}
                  onEditNodeConfig={viewingHistory ? undefined : openNodeConfigEditor}
                  onSelectEdge={setSelectedEdge}
                  onSelectNode={setSelectedNode}
                />
              ) : (
                <WorkspaceEmptyState className="h-full min-h-[420px]">
                  {detailQuery.isFetching ? '正在加载微流画布...' : detailQuery.isError ? '微流详情加载失败，请刷新后重试。' : '新建或选择一个微流开始设计。'}
                </WorkspaceEmptyState>
              )}
            </main>
            <section className="microflow-designer-config-pane">
              {definition && nodeConfigEditorNode ? (
                <MicroflowNodeConfigEditor
                  definition={definition}
                  microflowId={selectedId}
                  node={nodeConfigEditorNode}
                  open={Boolean(nodeConfigEditorNode)}
                  variant="panel"
                  onClose={() => {
                    setNodeConfigEditorNodeId(null);
                    setSelectedNodeId(null);
                  }}
                  onSave={updateDefinition}
                />
              ) : (
                <div className="microflow-node-config-panel microflow-node-config-panel--empty">
                  <div>
                    <GitBranch size={18} />
                    <strong>{translateCurrentLiteral("选择一个节点")}</strong>
                    <span>{translateCurrentLiteral("节点配置会在这里显示，画布空间保持稳定。")}</span>
                  </div>
                </div>
              )}
            </section>
          </div>
          <MicroflowPreviewDialog
            autoRunNonce={executeNonce}
            definition={definition}
            microflowId={selectedId}
            open={previewOpen}
            title={draftMeta.objectName || draftMeta.objectCode || '微流'}
            onClose={() => setPreviewOpen(false)}
          />
          {definition ? (
            <MicroflowEdgeConfigDialog
              definition={definition}
              edge={edgeConfigEditorEdge}
              open={Boolean(edgeConfigEditorEdge)}
              onChange={updateDefinition}
              onClose={() => setEdgeConfigEditorEdgeId(null)}
              onSelectEdge={setSelectedEdge}
            />
          ) : null}
          </div>
      )}
    </ApplicationConsolePageFrame>
  );

  async function refresh() {
    await queryClient.invalidateQueries({ queryKey: ['application-data-center', resourcePath] });
  }

  function updateDefinition(nextDefinition: MicroflowDefinition) {
    if (viewingHistory) {
      return;
    }
    setDefinition(nextDefinition);
    setIsDirty(true);
  }

  function requestPublish() {
    confirm({
      content: '确认发布当前已校验版本吗？发布后将同步接口端点。',
      onConfirm: async () => {
        await publishMutation.mutateAsync();
      },
      title: '发布微流'
    });
  }

  function executeMicroflow() {
    setPreviewOpen(true);
    setExecuteNonce((value) => value + 1);
  }

  function setSelectedNode(nodeId: string | null) {
    setSelectedNodeId(nodeId);
    if (nodeId) {
      setSelectedEdgeId(null);
    }
  }

  function setSelectedEdge(edgeId: string | null) {
    setSelectedEdgeId(edgeId);
    if (edgeId) {
      setSelectedNodeId(null);
    }
  }

  function openNodeConfigEditor(nodeId: string) {
    setSelectedNodeId(nodeId);
    setSelectedEdgeId(null);
    setNodeConfigEditorNodeId(nodeId);
  }
}

function buildMicroflowDetailQueryKey(id: string | null) {
  return ['application-data-center', resourcePath, 'detail', id] as const;
}

function createContextualMicroflowDefinition(code: string, dataSourceId?: string): MicroflowDefinition {
  return applyContextDataSource(createDefaultMicroflowDefinition(code), dataSourceId);
}

function applyContextDataSource(definition: MicroflowDefinition, dataSourceId?: string): MicroflowDefinition {
  if (!dataSourceId) {
    return definition;
  }

  return {
    ...definition,
    dataMappings: [
      ...definition.dataMappings.filter((item) => item.target !== 'dataSourceId'),
      { expression: { dataType: 'string', kind: 'literal', value: dataSourceId }, mappingCode: 'contextDataSource', target: 'dataSourceId' }
    ]
  };
}
