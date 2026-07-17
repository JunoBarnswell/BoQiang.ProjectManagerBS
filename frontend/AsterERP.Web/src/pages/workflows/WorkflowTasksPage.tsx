import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

import type { GridPageResult } from '../../api/shared.types';
import type { WorkflowAttachmentDto, WorkflowHistoricTaskDto, WorkflowInstanceListItemDto, WorkflowParticipantDto, WorkflowQuery, WorkflowTaskActionRequest, WorkflowTaskListItemDto } from '../../api/workflow/workflows.api';
import {
  addWorkflowAttachment,
  claimWorkflowTask,
  completeWorkflowTask,
  delegateWorkflowTask,
  downloadWorkflowAttachment,
  getWorkflowCcTaskInstances,
  getWorkflowDelegatedTasks,
  getWorkflowDoneTasks,
  getWorkflowHistoryTasks,
  getWorkflowMineTaskInstances,
  getWorkflowParticipants,
  getWorkflowTaskDetail,
  getWorkflowTaskSummary,
  getWorkflowTimeoutTasks,
  getWorkflowTodoTasks,
  rejectWorkflowTask,
  resolveWorkflowTask,
  transferWorkflowTask,
  unclaimWorkflowTask
} from '../../api/workflow/workflows.api';
import type { ApiEnvelope } from '../../core/http/apiEnvelope';
import { translateCurrentLocale, useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useMessage } from '../../shared/feedback/useMessage';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import { TableActions } from '../../shared/table/TableActions';
import type { DataTableColumn } from '../../shared/table/tableTypes';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import './workflow-bpmn.css';

import { WorkflowApprovalDrawer, type WorkflowApprovalAction, type WorkflowApprovalActionState } from './WorkflowApprovalDrawer';

type TaskTab = 'todo' | 'done' | 'mine' | 'delegated' | 'timeout' | 'cc' | 'history';
type TaskAction = WorkflowApprovalAction;
type TaskActionState = WorkflowApprovalActionState;

const defaultActionState: TaskActionState = {
  comment: '',
  targetUserId: '',
  userId: '',
  variables: null,
  variablesJson: '{}'
};

function emptySummary() {
  return { cc: 0, delegated: 0, done: 0, history: 0, mine: 0, timeout: 0, todo: 0 };
}

export function WorkflowTasksPage() {
  const { translate } = useI18n();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const message = useMessage();
  const queryClient = useQueryClient();
  const initialTab = normalizeTaskTab(searchParams.get('tab'));
  const [tab, setTab] = useState<TaskTab>(initialTab);
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [actionType, setActionType] = useState<TaskAction>('complete');
  const [targetTask, setTargetTask] = useState<WorkflowTaskListItemDto | null>(null);
  const [actionState, setActionState] = useState<TaskActionState>(defaultActionState);
  const queryTab = normalizeTaskTab(searchParams.get('tab'));

  useEffect(() => {
    if (queryTab === tab) {
      return;
    }

    setTab(queryTab);
    setPageIndex(1);
  }, [queryTab, tab]);

  const query = { keyword, pageIndex, pageSize };
  const summaryQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowTaskSummary(signal),
    queryKey: ['workflows', 'tasks', 'summary']
  });
  const todoQuery = useTaskBoxQuery('todo', tab, query, getWorkflowTodoTasks);
  const delegatedQuery = useTaskBoxQuery('delegated', tab, query, getWorkflowDelegatedTasks);
  const timeoutQuery = useTaskBoxQuery('timeout', tab, query, getWorkflowTimeoutTasks);
  const doneQuery = useHistoricTaskBoxQuery('done', tab, query, getWorkflowDoneTasks);
  const historyQuery = useHistoricTaskBoxQuery('history', tab, query, getWorkflowHistoryTasks);
  const mineQuery = useInstanceBoxQuery('mine', tab, query, getWorkflowMineTaskInstances);
  const ccQuery = useInstanceBoxQuery('cc', tab, query, getWorkflowCcTaskInstances);
  const participantsQuery = useApiQuery({
    queryFn: ({ signal }) => getWorkflowParticipants({ type: 'user' }, signal),
    queryKey: ['workflows', 'participants', 'user', 'task-action']
  });
  const taskDetailQuery = useApiQuery({
    enabled: Boolean(targetTask),
    queryFn: ({ signal }) => getWorkflowTaskDetail(targetTask?.id ?? '', signal),
    queryKey: ['workflows', 'tasks', 'detail', targetTask?.id ?? 'none']
  });

  const claimMutation = useApiMutation({ mutationFn: (taskId: string) => claimWorkflowTask(taskId, {}) });
  const unclaimMutation = useApiMutation({ mutationFn: unclaimWorkflowTask });
  const completeMutation = useApiMutation({ mutationFn: ({ taskId, request }: { request: WorkflowTaskActionRequest; taskId: string }) => completeWorkflowTask(taskId, request) });
  const rejectMutation = useApiMutation({ mutationFn: ({ taskId, request }: { request: WorkflowTaskActionRequest; taskId: string }) => rejectWorkflowTask(taskId, request) });
  const transferMutation = useApiMutation({ mutationFn: ({ taskId, request }: { request: WorkflowTaskActionRequest; taskId: string }) => transferWorkflowTask(taskId, request) });
  const delegateMutation = useApiMutation({ mutationFn: ({ taskId, request }: { request: WorkflowTaskActionRequest; taskId: string }) => delegateWorkflowTask(taskId, request) });
  const resolveMutation = useApiMutation({ mutationFn: ({ taskId, request }: { request: WorkflowTaskActionRequest; taskId: string }) => resolveWorkflowTask(taskId, request) });
  const downloadAttachmentMutation = useApiMutation({ mutationFn: (attachment: WorkflowAttachmentDto) => downloadWorkflowAttachment(attachment, translate('workflow.drawer.noDownload')) });
  const uploadAttachmentMutation = useApiMutation({
    mutationFn: async ({ file, taskId }: { file: File; taskId: string }) =>
      addWorkflowAttachment(taskId, {
        attachmentType: file.type || 'application/octet-stream',
        base64Content: await readFileAsBase64(file),
        name: file.name
      })
  });
  const taskTabs: Array<{ countKey: keyof NonNullable<ReturnType<typeof emptySummary>>; hint: string; icon: string; label: string; value: TaskTab }> = [
    { countKey: 'todo', hint: translate('workflow.tasks.tabs.todoHint'), icon: 'tray', label: translate('workflow.tasks.tabs.todo'), value: 'todo' },
    { countKey: 'done', hint: translate('workflow.tasks.tabs.doneHint'), icon: 'check-circle', label: translate('workflow.tasks.tabs.done'), value: 'done' },
    { countKey: 'mine', hint: translate('workflow.tasks.tabs.mineHint'), icon: 'paper-plane-tilt', label: translate('workflow.tasks.tabs.mine'), value: 'mine' },
    { countKey: 'delegated', hint: translate('workflow.tasks.tabs.delegatedHint'), icon: 'user-switch', label: translate('workflow.tasks.tabs.delegated'), value: 'delegated' },
    { countKey: 'timeout', hint: translate('workflow.tasks.tabs.timeoutHint'), icon: 'clock-countdown', label: translate('workflow.tasks.tabs.timeout'), value: 'timeout' },
    { countKey: 'cc', hint: translate('workflow.tasks.tabs.ccHint'), icon: 'bell-ringing', label: translate('workflow.tasks.tabs.cc'), value: 'cc' },
    { countKey: 'history', hint: translate('workflow.tasks.tabs.historyHint'), icon: 'clock-counter-clockwise', label: translate('workflow.tasks.tabs.history'), value: 'history' }
  ];

  const taskColumns = useMemo<DataTableColumn<WorkflowTaskListItemDto>[]>(() => [
    { key: 'name', title: translate('workflow.tasks.columns.task'), width: '260px', responsivePriority: 100, render: renderTaskName },
    { key: 'business', title: translate('workflow.tasks.columns.business'), width: '190px', responsivePriority: 95, render: renderTaskBusiness },
    { key: 'processName', title: translate('workflow.tasks.columns.process'), width: '180px', responsivePriority: 85, render: (row) => row.processName ?? row.processDefinitionId ?? '-' },
    { key: 'assignee', title: translate('workflow.tasks.columns.assignee'), width: '190px', responsivePriority: 90, render: renderTaskAssignee },
    { key: 'dueAt', title: translate('workflow.tasks.columns.dueAt'), width: '160px', hideBelow: 'lg', render: renderDueAt },
    { key: 'counts', title: translate('workflow.tasks.columns.counts'), width: '120px', hideBelow: 'xl', render: (row) => `${row.commentsCount}/${row.attachmentsCount}` }
  ], [translate]);
  const historicColumns = useMemo<DataTableColumn<WorkflowHistoricTaskDto>[]>(() => [
    { key: 'name', title: translate('workflow.tasks.columns.task'), width: '240px', responsivePriority: 100, render: (row) => <><div className="font-medium text-gray-900">{row.name ?? row.id}</div><div className="text-xs text-gray-500">{row.processName ?? row.processDefinitionId ?? '-'}</div></> },
    { key: 'business', title: translate('workflow.tasks.columns.business'), width: '190px', responsivePriority: 95, render: (row) => <><div>{row.businessType ?? '-'}</div><div className="text-xs text-gray-500">{row.businessKey ?? '-'}</div></> },
    { key: 'assignee', title: translate('workflow.tasks.columns.handler'), width: '150px', responsivePriority: 90, render: (row) => row.assigneeName ?? row.assignee ?? '-' },
    { key: 'startedBy', title: translate('workflow.tasks.columns.startedBy'), width: '150px', hideBelow: 'lg', render: (row) => row.starterUserName ?? '-' },
    { key: 'endTime', title: translate('workflow.tasks.columns.endTime'), width: '180px', hideBelow: 'lg', render: (row) => formatDateTime(row.endTime) },
    { key: 'counts', title: translate('workflow.tasks.columns.counts'), width: '120px', hideBelow: 'xl', render: (row) => `${row.commentsCount}/${row.attachmentsCount}` }
  ], [translate]);
  const instanceColumns = useMemo<DataTableColumn<WorkflowInstanceListItemDto>[]>(() => [
    { key: 'businessKey', title: translate('workflow.tasks.columns.business'), width: '220px', responsivePriority: 100, render: (row) => <><div className="font-medium text-gray-900">{row.businessType}</div><div className="text-xs text-gray-500">{row.businessKey}</div></> },
    { key: 'processDefinitionKey', title: translate('workflow.tasks.columns.processKey'), width: '180px', responsivePriority: 90 },
    { key: 'status', title: translate('workflow.tasks.columns.status'), width: '120px', responsivePriority: 80 },
    { key: 'startedBy', title: translate('workflow.tasks.columns.startedBy'), width: '140px', hideBelow: 'lg' },
    { key: 'startedAt', title: translate('workflow.tasks.columns.startedAt'), width: '180px', hideBelow: 'lg', render: (row) => formatDateTime(row.startedAt) }
  ], [translate]);

  const summary = summaryQuery.data?.data ?? emptySummary();
  const userOptions = (participantsQuery.data?.data ?? []).map((participant: WorkflowParticipantDto) => ({ label: `${participant.name} (${participant.code})`, value: participant.id }));
  const actionSubmitting = completeMutation.isPending || rejectMutation.isPending || transferMutation.isPending || delegateMutation.isPending || resolveMutation.isPending;

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['workflows', 'tasks'] });
    await queryClient.invalidateQueries({ queryKey: ['workflows', 'history'] });
  };

  const openAction = (row: WorkflowTaskListItemDto, nextAction: TaskAction) => {
    setTargetTask(row);
    setActionType(nextAction);
    setActionState({ ...defaultActionState });
  };

  const closeAction = () => {
    setTargetTask(null);
    setActionState({ ...defaultActionState });
  };

  const submitAction = async () => {
    if (!targetTask) return;
    if ((actionType === 'delegate' || actionType === 'transfer') && !actionState.targetUserId) {
      message.error(translate('workflow.tasks.selectTargetUser'));
      return;
    }

    const actionPolicy = taskDetailQuery.data?.data.nodePolicy?.actionPolicies.find((item) => item.action === actionType);
    if (actionPolicy && !actionPolicy.enabled) {
      message.error(translate('workflow.tasks.actionDisabled'));
      return;
    }

    if (actionPolicy?.commentRequired && !actionState.comment?.trim()) {
      message.error(translate('workflow.tasks.commentRequired'));
      return;
    }

    const attachmentCount = taskDetailQuery.data?.data.attachments.length ?? 0;
    if (actionPolicy?.attachmentPolicy === 'required' && attachmentCount === 0) {
      message.error(translate('workflow.tasks.attachmentRequired'));
      return;
    }

    if (actionPolicy?.attachmentPolicy === 'none' && attachmentCount > 0) {
      message.error(translate('workflow.tasks.attachmentForbidden'));
      return;
    }

    let variables: Record<string, unknown> | null = null;
    try {
      variables = actionState.variablesJson.trim() ? JSON.parse(actionState.variablesJson) as Record<string, unknown> : null;
    } catch {
      message.error(translate('workflow.tasks.invalidVariablesJson'));
      return;
    }

    const request: WorkflowTaskActionRequest = {
      comment: actionState.comment,
      targetUserId: actionState.targetUserId || null,
      variables
    };

    try {
      if (actionType === 'complete') await completeMutation.mutateAsync({ request, taskId: targetTask.id });
      if (actionType === 'reject') await rejectMutation.mutateAsync({ request, taskId: targetTask.id });
      if (actionType === 'transfer') await transferMutation.mutateAsync({ request, taskId: targetTask.id });
      if (actionType === 'delegate') await delegateMutation.mutateAsync({ request, taskId: targetTask.id });
      if (actionType === 'resolve') await resolveMutation.mutateAsync({ request, taskId: targetTask.id });
      closeAction();
      await refresh();
      message.success(translate('workflow.tasks.actionSubmitted'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.tasks.actionSubmitFailed')));
    }
  };

  const downloadAttachment = async (attachment: WorkflowAttachmentDto) => {
    try {
      const result = await downloadAttachmentMutation.mutateAsync(attachment);
      saveBlob(result.blob, result.fileName || attachment.name || 'workflow-attachment');
      message.success(translate('workflow.tasks.attachmentDownloadStarted'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.tasks.attachmentDownloadFailed')));
    }
  };

  const uploadAttachment = async (file: File) => {
    if (!targetTask) return;
    try {
      await uploadAttachmentMutation.mutateAsync({ file, taskId: targetTask.id });
      await queryClient.invalidateQueries({ queryKey: ['workflows', 'tasks', 'detail', targetTask.id] });
      await refresh();
      message.success(translate('workflow.tasks.attachmentUploadSuccess'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('workflow.tasks.attachmentUploadFailed')));
    }
  };

  const setActiveTab = (next: TaskTab) => {
    setTab(next);
    setPageIndex(1);
    const nextSearchParams = new URLSearchParams(searchParams);
    nextSearchParams.set('tab', next);
    setSearchParams(nextSearchParams);
  };
  const actionTitle = ({
    complete: translate('workflow.tasks.complete'),
    reject: translate('workflow.tasks.reject'),
    transfer: translate('workflow.tasks.transfer'),
    delegate: translate('workflow.tasks.delegate'),
    resolve: translate('workflow.tasks.resolve')
  } as Record<TaskAction, string>)[actionType];

  return (
    <CrudPage
      title={translate('workflow.tasks.title')}
      description={translate('workflow.tasks.description')}
      actions={(
        <div className="workflow-page-actions">
          <label className="workflow-toolbar-search">
            <AppIcon name="magnifying-glass" />
            <input placeholder={translate('workflow.tasks.searchPlaceholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          </label>
          <button className="workflow-refresh-button" title={translate('workflow.tasks.refresh')} type="button" onClick={() => void refresh()}><AppIcon name="arrows-clockwise" /></button>
        </div>
      )}
    >
      <div className="workflow-page-body workflow-page-body--tasks">
        <div className="workflow-workbench-summary">
          {taskTabs.map((item) => (
            <button
              key={item.value}
              aria-pressed={tab === item.value}
              className={`workflow-summary-card workflow-summary-card--button ${tab === item.value ? 'workflow-summary-card--active' : ''}`}
              type="button"
              onClick={() => setActiveTab(item.value)}
            >
              <span className="workflow-summary-card__icon"><AppIcon name={item.icon} /></span>
              <span className="workflow-summary-card__main">
                <span className="workflow-summary-card__label">{item.label}</span>
                <span className="workflow-summary-card__hint">{item.hint}</span>
              </span>
              <strong className="workflow-summary-card__count">{summary[item.countKey]}</strong>
            </button>
          ))}
        </div>

        <div className="workflow-table-surface">
          {renderActiveTable({
            ccQuery,
            delegatedQuery,
            doneQuery,
            historyQuery,
            historicColumns,
            instanceColumns,
            mineQuery,
            navigate,
            openAction,
            pageIndex,
            pageSize,
            setPageIndex,
            setPageSize,
            tab,
            taskColumns,
            timeoutQuery,
            todoQuery,
            onClaim: async (taskId) => { await claimMutation.mutateAsync(taskId); await refresh(); },
            onUnclaim: async (taskId) => { await unclaimMutation.mutateAsync(taskId); await refresh(); }
          })}
        </div>
      </div>

      <WorkflowApprovalDrawer
        actionState={actionState}
        actionTitle={actionTitle}
        actionType={actionType}
        detail={taskDetailQuery.data?.data ?? null}
        loading={taskDetailQuery.isLoading}
        open={Boolean(targetTask)}
        submitting={actionSubmitting}
        task={targetTask}
        uploadingAttachment={uploadAttachmentMutation.isPending}
        userOptions={userOptions}
        onClose={closeAction}
        onDownloadAttachment={(attachment) => void downloadAttachment(attachment)}
        onOpenProcess={(processInstanceId) => navigate(`/workflows/instances/${processInstanceId}`)}
        onSubmit={() => void submitAction()}
        onUploadAttachment={(file) => void uploadAttachment(file)}
        onValueChange={(name, value) => setActionState((current) => ({ ...current, [name]: value }))}
      />
    </CrudPage>
  );
}

function readFileAsBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(reader.error ?? new Error('Failed to read file'));
    reader.onload = () => {
      const value = typeof reader.result === 'string' ? reader.result : '';
      resolve(value.includes(',') ? value.slice(value.indexOf(',') + 1) : value);
    };
    reader.readAsDataURL(file);
  });
}

function normalizeTaskTab(value: string | null): TaskTab {
  const allowedTabs = new Set<TaskTab>(['todo', 'done', 'mine', 'delegated', 'timeout', 'cc', 'history']);
  return allowedTabs.has(value as TaskTab) ? value as TaskTab : 'todo';
}

function useTaskBoxQuery(
  queryKey: TaskTab,
  activeTab: TaskTab,
  query: WorkflowQuery,
  queryFn: (query: WorkflowQuery, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<WorkflowTaskListItemDto>>>
) {
  return useApiQuery({
    enabled: activeTab === queryKey,
    keepPreviousData: true,
    queryFn: ({ signal }) => queryFn(query, signal),
    queryKey: ['workflows', 'tasks', queryKey, query.keyword, query.pageIndex, query.pageSize]
  });
}

function useHistoricTaskBoxQuery(
  queryKey: TaskTab,
  activeTab: TaskTab,
  query: WorkflowQuery,
  queryFn: (query: WorkflowQuery, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<WorkflowHistoricTaskDto>>>
) {
  return useApiQuery({
    enabled: activeTab === queryKey,
    keepPreviousData: true,
    queryFn: ({ signal }) => queryFn(query, signal),
    queryKey: ['workflows', 'tasks', queryKey, query.keyword, query.pageIndex, query.pageSize]
  });
}

function useInstanceBoxQuery(
  queryKey: TaskTab,
  activeTab: TaskTab,
  query: WorkflowQuery,
  queryFn: (query: WorkflowQuery, signal?: AbortSignal) => Promise<ApiEnvelope<GridPageResult<WorkflowInstanceListItemDto>>>
) {
  return useApiQuery({
    enabled: activeTab === queryKey,
    keepPreviousData: true,
    queryFn: ({ signal }) => queryFn(query, signal),
    queryKey: ['workflows', 'tasks', queryKey, query.keyword, query.pageIndex, query.pageSize]
  });
}

interface ActiveTableProps {
  ccQuery: ReturnType<typeof useInstanceBoxQuery>;
  delegatedQuery: ReturnType<typeof useTaskBoxQuery>;
  doneQuery: ReturnType<typeof useHistoricTaskBoxQuery>;
  historyQuery: ReturnType<typeof useHistoricTaskBoxQuery>;
  historicColumns: DataTableColumn<WorkflowHistoricTaskDto>[];
  instanceColumns: DataTableColumn<WorkflowInstanceListItemDto>[];
  mineQuery: ReturnType<typeof useInstanceBoxQuery>;
  navigate: ReturnType<typeof useNavigate>;
  openAction: (row: WorkflowTaskListItemDto, action: TaskAction) => void;
  pageIndex: number;
  pageSize: number;
  setPageIndex: (page: number) => void;
  setPageSize: (pageSize: number) => void;
  tab: TaskTab;
  taskColumns: DataTableColumn<WorkflowTaskListItemDto>[];
  timeoutQuery: ReturnType<typeof useTaskBoxQuery>;
  todoQuery: ReturnType<typeof useTaskBoxQuery>;
  onClaim: (taskId: string) => Promise<void>;
  onUnclaim: (taskId: string) => Promise<void>;
}

function renderActiveTable(props: ActiveTableProps) {
  if (props.tab === 'todo' || props.tab === 'delegated' || props.tab === 'timeout') {
    const queryMap = { delegated: props.delegatedQuery, timeout: props.timeoutQuery, todo: props.todoQuery };
    const query = queryMap[props.tab];
    return (
      <DataTable
        columnSettingsKey={`workflow-${props.tab}-tasks`}
        columns={props.taskColumns}
        emptyText={query.isError ? translateCurrentLocale('workflow.tasks.loadFailed') : translateCurrentLocale('workflow.tasks.empty.tasks')}
        fitScreen
        loading={query.isLoading}
        onPageChange={props.setPageIndex}
        onPageSizeChange={(next) => { props.setPageSize(next); props.setPageIndex(1); }}
        pagination={{ current: props.pageIndex, pageSize: props.pageSize, total: query.data?.data.total ?? 0 }}
        rowActions={(row) => renderTaskActions(row, props)}
        rowKey={(row) => row.id}
        rows={query.data?.data.items ?? []}
      />
    );
  }

  if (props.tab === 'done' || props.tab === 'history') {
    const query = props.tab === 'done' ? props.doneQuery : props.historyQuery;
    return (
      <DataTable
        columnSettingsKey={`workflow-${props.tab}-history-tasks`}
        columns={props.historicColumns}
        emptyText={query.isError ? translateCurrentLocale('workflow.tasks.loadFailed') : translateCurrentLocale('workflow.tasks.empty.history')}
        fitScreen
        loading={query.isLoading}
        onPageChange={props.setPageIndex}
        onPageSizeChange={(next) => { props.setPageSize(next); props.setPageIndex(1); }}
        pagination={{ current: props.pageIndex, pageSize: props.pageSize, total: query.data?.data.total ?? 0 }}
        rowActions={(row) => row.processInstanceId ? <button className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.openProcess')} type="button" onClick={() => props.navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="eye" /></button> : null}
        rowKey={(row) => row.id}
        rows={query.data?.data.items ?? []}
      />
    );
  }

  const query = props.tab === 'mine' ? props.mineQuery : props.ccQuery;
  return (
    <DataTable
      columnSettingsKey={`workflow-${props.tab}-instances`}
      columns={props.instanceColumns}
      emptyText={query.isError ? translateCurrentLocale('workflow.tasks.loadFailed') : translateCurrentLocale('workflow.tasks.empty.instances')}
      fitScreen
      loading={query.isLoading}
      onPageChange={props.setPageIndex}
      onPageSizeChange={(next) => { props.setPageSize(next); props.setPageIndex(1); }}
      pagination={{ current: props.pageIndex, pageSize: props.pageSize, total: query.data?.data.total ?? 0 }}
      rowActions={(row) => <button className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.openProcess')} type="button" onClick={() => props.navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="eye" /></button>}
      rowKey={(row) => row.id}
      rows={query.data?.data.items ?? []}
    />
  );
}

function renderTaskActions(row: WorkflowTaskListItemDto, props: ActiveTableProps) {
  const actions = new Set(row.availableActions);
  return (
    <TableActions>
      {actions.has('claim') ? <PermissionButton code="workflow:task:claim" className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.claim')} type="button" onClick={() => void props.onClaim(row.id)}><AppIcon className="text-base" name="hand" /></PermissionButton> : null}
      {row.assignee ? <PermissionButton code="workflow:task:claim" className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.unclaim')} type="button" onClick={() => void props.onUnclaim(row.id)}><AppIcon className="text-base" name="hand-withdraw" /></PermissionButton> : null}
      {actions.has('complete') ? <PermissionButton code="workflow:task:approve" className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.complete')} type="button" onClick={() => props.openAction(row, 'complete')}><AppIcon className="text-base" name="check" /></PermissionButton> : null}
      {actions.has('reject') ? <PermissionButton code="workflow:task:approve" className="hover:text-red-600" title={translateCurrentLocale('workflow.tasks.reject')} type="button" onClick={() => props.openAction(row, 'reject')}><AppIcon className="text-base" name="x" /></PermissionButton> : null}
      {actions.has('transfer') ? <PermissionButton code="workflow:task:transfer" className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.transfer')} type="button" onClick={() => props.openAction(row, 'transfer')}><AppIcon className="text-base" name="arrow-fat-lines-right" /></PermissionButton> : null}
      {actions.has('delegate') ? <PermissionButton code="workflow:task:delegate" className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.delegate')} type="button" onClick={() => props.openAction(row, 'delegate')}><AppIcon className="text-base" name="user-switch" /></PermissionButton> : null}
      {actions.has('resolve') ? <PermissionButton code="workflow:task:delegate" className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.resolve')} type="button" onClick={() => props.openAction(row, 'resolve')}><AppIcon className="text-base" name="arrow-counter-clockwise" /></PermissionButton> : null}
      {row.processInstanceId ? <button className="hover:text-primary-600" title={translateCurrentLocale('workflow.tasks.process')} type="button" onClick={() => props.navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="git-branch" /></button> : null}
    </TableActions>
  );
}

function renderTaskName(row: WorkflowTaskListItemDto) {
  return (
    <>
      <div className="font-medium text-gray-900">{row.name ?? row.id}</div>
      <div className="text-xs text-gray-500">{row.taskDefinitionKey ?? '-'}</div>
    </>
  );
}

function renderTaskBusiness(row: WorkflowTaskListItemDto) {
  return (
    <>
      <div>{row.businessType ?? '-'}</div>
      <div className="text-xs text-gray-500">{row.businessKey ?? row.processInstanceId ?? '-'}</div>
    </>
  );
}

function renderTaskAssignee(row: WorkflowTaskListItemDto) {
  if (row.assigneeName || row.assignee) {
    return row.assigneeName ?? row.assignee;
  }

  return row.candidateNames.length > 0 ? row.candidateNames.join('、') : translateCurrentLocale('workflow.tasks.candidate');
}

function renderDueAt(row: WorkflowTaskListItemDto) {
  if (!row.dueAt) {
    return '-';
  }

  return <span className={row.isOverdue ? 'text-red-600 font-medium' : ''}>{formatDateTime(row.dueAt)}</span>;
}

function formatDateTime(value?: string | null) {
  return value ? new Date(value).toLocaleString() : '-';
}

function saveBlob(blob: Blob, fileName: string) {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.setTimeout(() => window.URL.revokeObjectURL(url), 0);
}
