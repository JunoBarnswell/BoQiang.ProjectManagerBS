import { useQueryClient } from '@tanstack/react-query';
import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';

import type {
  WorkflowMessageTemplateUpsertRequest,
  WorkflowNotificationChannelUpsertRequest
} from '../../api/workflow/workflows.api';
import {
  deleteWorkflowMessageTemplate,
  deleteWorkflowNotificationChannel,
  deleteWorkflowNotificationRule,
  getWorkflowMessageTemplates,
  getWorkflowNotificationChannels,
  getWorkflowNotificationLogs,
  getWorkflowNotificationRules,
  getWorkflowNotificationTasks,
  saveWorkflowMessageTemplate,
  saveWorkflowNotificationChannel,
  saveWorkflowNotificationRule
} from '../../api/workflow/workflows.api';
import { useI18n } from '../../core/i18n/I18nProvider';
import { useApiMutation } from '../../core/query/useApiMutation';
import { useApiQuery } from '../../core/query/useApiQuery';
import { useWorkspaceStore } from '../../core/state';
import { PermissionButton } from '../../shared/auth/PermissionButton';
import { CrudPage } from '../../shared/components/crud-page/CrudPage';
import { useConfirm } from '../../shared/feedback/useConfirm';
import { useMessage } from '../../shared/feedback/useMessage';
import { ModalForm } from '../../shared/forms/ModalForm';
import { AppIcon } from '../../shared/icons/AppIcon';
import { DataTable } from '../../shared/table/DataTable';
import { getErrorMessage } from '../../shared/utils/errorMessage';

import {
  renderChannelActions,
  renderRuleActions,
  renderTemplateActions
} from './workflowNotificationActions';
import {
  createChannelDraft,
  createChannelFields,
  createRuleDraft,
  createRuleFields,
  createTemplateDraft,
  createTemplateFields
} from './workflowNotificationForms';
import { parseChannelCodes } from './workflowNotificationOptions';
import type { ConfigModal, NotificationTab, WorkflowNodeNotificationRuleForm } from './workflowNotificationsTypes';
import {
  createChannelColumns,
  createLogColumns,
  createNotificationTabs,
  createRuleColumns,
  createTaskColumns,
  createTemplateColumns
} from './workflowNotificationTables';
import './workflow-bpmn.css';

export function WorkflowNotificationsPage() {
  const workspace = useWorkspaceStore((state) => state.currentWorkspace);
  const navigate = useNavigate();
  const { translate } = useI18n();
  const message = useMessage();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<NotificationTab>('tasks');
  const [keyword, setKeyword] = useState('');
  const [pageIndex, setPageIndex] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [modal, setModal] = useState<ConfigModal | null>(null);

  const appCode = workspace?.appCode ?? 'SYSTEM';
  const tenantId = workspace?.tenantId ?? 'tenant-system';

  const [channelForm, setChannelForm] = useState<WorkflowNotificationChannelUpsertRequest>(() => createChannelDraft(translate, appCode, tenantId));
  const [templateForm, setTemplateForm] = useState<WorkflowMessageTemplateUpsertRequest>(() => createTemplateDraft(translate, appCode, tenantId));
  const [ruleForm, setRuleForm] = useState<WorkflowNodeNotificationRuleForm>(() => createRuleDraft(appCode, tenantId));

  const query = { appCode: workspace?.appCode, keyword, pageIndex, pageSize, tenantId: workspace?.tenantId };
  const notificationTabs = useMemo(() => createNotificationTabs(translate), [translate]);
  const channelColumns = useMemo(() => createChannelColumns(translate), [translate]);
  const templateColumns = useMemo(() => createTemplateColumns(translate), [translate]);
  const ruleColumns = useMemo(() => createRuleColumns(translate), [translate]);
  const taskColumns = useMemo(() => createTaskColumns(translate), [translate]);
  const logColumns = useMemo(() => createLogColumns(translate), [translate]);
  const channelFields = useMemo(() => createChannelFields(translate), [translate]);
  const templateFields = useMemo(() => createTemplateFields(translate), [translate]);
  const ruleFields = useMemo(() => createRuleFields(translate), [translate]);

  const channelsQuery = useApiQuery({
    enabled: tab === 'channels',
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowNotificationChannels(query, signal),
    queryKey: ['workflows', 'notifications', 'channels', query.appCode, keyword, pageIndex, pageSize]
  });
  const templatesQuery = useApiQuery({
    enabled: tab === 'templates',
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowMessageTemplates(query, signal),
    queryKey: ['workflows', 'notifications', 'templates', query.appCode, keyword, pageIndex, pageSize]
  });
  const rulesQuery = useApiQuery({
    enabled: tab === 'rules',
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowNotificationRules(query, signal),
    queryKey: ['workflows', 'notifications', 'rules', query.appCode, keyword, pageIndex, pageSize]
  });
  const tasksQuery = useApiQuery({
    enabled: tab === 'tasks',
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowNotificationTasks(query, signal),
    queryKey: ['workflows', 'notifications', 'tasks', query.appCode, keyword, pageIndex, pageSize]
  });
  const logsQuery = useApiQuery({
    enabled: tab === 'logs',
    keepPreviousData: true,
    queryFn: ({ signal }) => getWorkflowNotificationLogs(query, signal),
    queryKey: ['workflows', 'notifications', 'logs', query.appCode, keyword, pageIndex, pageSize]
  });

  const saveChannelMutation = useApiMutation({ mutationFn: saveWorkflowNotificationChannel });
  const saveTemplateMutation = useApiMutation({ mutationFn: saveWorkflowMessageTemplate });
  const saveRuleMutation = useApiMutation({ mutationFn: saveWorkflowNotificationRule });
  const deleteChannelMutation = useApiMutation({ mutationFn: deleteWorkflowNotificationChannel });
  const deleteTemplateMutation = useApiMutation({ mutationFn: deleteWorkflowMessageTemplate });
  const deleteRuleMutation = useApiMutation({ mutationFn: deleteWorkflowNotificationRule });

  const openCreate = () => {
    if (tab === 'channels') {
      setChannelForm(createChannelDraft(translate, appCode, tenantId));
      setModal('channel');
      return;
    }

    if (tab === 'templates') {
      setTemplateForm(createTemplateDraft(translate, appCode, tenantId));
      setModal('template');
      return;
    }

    if (tab === 'rules') {
      setRuleForm(createRuleDraft(appCode, tenantId));
      setModal('rule');
    }
  };

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: ['workflows', 'notifications'] });
  };

  const submit = async () => {
    try {
      if (modal === 'channel') {
        await saveChannelMutation.mutateAsync(channelForm);
      }

      if (modal === 'template') {
        await saveTemplateMutation.mutateAsync(templateForm);
      }

      if (modal === 'rule') {
        const { channelCodesText, ...request } = ruleForm;
        await saveRuleMutation.mutateAsync({ ...request, channelCodes: parseChannelCodes(channelCodesText) });
      }

      setModal(null);
      await refresh();
      message.success(translate('page.workflowNotifications.success.save'));
    } catch (error) {
      message.error(getErrorMessage(error, translate('page.workflowNotifications.error.saveFailed')));
    }
  };

  const channelModalTitle = channelForm.id ? translate('page.workflowNotifications.modal.editChannel') : translate('page.workflowNotifications.modal.createChannel');
  const templateModalTitle = templateForm.id ? translate('page.workflowNotifications.modal.editTemplate') : translate('page.workflowNotifications.modal.createTemplate');
  const ruleModalTitle = ruleForm.id ? translate('page.workflowNotifications.modal.editRule') : translate('page.workflowNotifications.modal.createRule');

  return (
    <CrudPage
      title={translate('page.workflowNotifications.title')}
      description={translate('page.workflowNotifications.description')}
      actions={(
        <div className="workflow-page-actions">
          <label className="workflow-toolbar-search workflow-toolbar-search--wide">
            <AppIcon name="magnifying-glass" />
            <input placeholder={translate('page.workflowNotifications.searchPlaceholder')} value={keyword} onChange={(event) => { setKeyword(event.target.value); setPageIndex(1); }} />
          </label>
          {tab === 'channels' ? <PermissionButton code="workflow:notification:channel:edit" className="workflow-primary-action" type="button" onClick={openCreate}><AppIcon name="plus" /> {translate('page.workflowNotifications.action.createChannel')}</PermissionButton> : null}
          {tab === 'templates' ? <PermissionButton code="workflow:notification:template:edit" className="workflow-primary-action" type="button" onClick={openCreate}><AppIcon name="plus" /> {translate('page.workflowNotifications.action.createTemplate')}</PermissionButton> : null}
          {tab === 'rules' ? <PermissionButton code="workflow:notification:rule:edit" className="workflow-primary-action" type="button" onClick={openCreate}><AppIcon name="plus" /> {translate('page.workflowNotifications.action.createRule')}</PermissionButton> : null}
          <button className="workflow-refresh-button" title={translate('page.workflowNotifications.action.refresh')} type="button" onClick={() => void refresh()}><AppIcon name="arrows-clockwise" /></button>
        </div>
      )}
    >
      <div className="workflow-page-body">
        <div className="workflow-tab-strip workflow-tab-strip--notifications" role="tablist" aria-label={translate('page.workflowNotifications.tabListLabel')}>
          {notificationTabs.map((item) => (
            <button
              key={item.value}
              aria-selected={tab === item.value}
              className={`workflow-tab-button ${tab === item.value ? 'workflow-tab-button--active' : ''}`}
              role="tab"
              type="button"
              onClick={() => { setTab(item.value); setPageIndex(1); }}
            >
              <AppIcon name={item.icon} />
              <span>
                <strong>{item.label}</strong>
                <em>{item.description}</em>
              </span>
            </button>
          ))}
        </div>
        <div className="workflow-table-surface">
          {tab === 'channels' ? (
            <DataTable
              columnSettingsKey="workflow-notification-channels"
              columns={channelColumns}
              emptyText={channelsQuery.isError ? translate('page.workflowNotifications.empty.loadFailed') : translate('page.workflowNotifications.empty.channels')}
              fitScreen
              loading={channelsQuery.isLoading}
              onPageChange={setPageIndex}
              onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
              pagination={{ current: pageIndex, pageSize, total: channelsQuery.data?.data.total ?? 0 }}
              rowActions={(row) => renderChannelActions(row, setChannelForm, setModal, confirm, deleteChannelMutation.mutateAsync, refresh, message, translate)}
              rowKey={(row) => row.id}
              rows={channelsQuery.data?.data.items ?? []}
            />
          ) : null}
          {tab === 'templates' ? (
            <DataTable
              columnSettingsKey="workflow-notification-templates"
              columns={templateColumns}
              emptyText={templatesQuery.isError ? translate('page.workflowNotifications.empty.loadFailed') : translate('page.workflowNotifications.empty.templates')}
              fitScreen
              loading={templatesQuery.isLoading}
              onPageChange={setPageIndex}
              onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
              pagination={{ current: pageIndex, pageSize, total: templatesQuery.data?.data.total ?? 0 }}
              rowActions={(row) => renderTemplateActions(row, setTemplateForm, setModal, confirm, deleteTemplateMutation.mutateAsync, refresh, message, translate)}
              rowKey={(row) => row.id}
              rows={templatesQuery.data?.data.items ?? []}
            />
          ) : null}
          {tab === 'rules' ? (
            <DataTable
              columnSettingsKey="workflow-notification-rules"
              columns={ruleColumns}
              emptyText={rulesQuery.isError ? translate('page.workflowNotifications.empty.loadFailed') : translate('page.workflowNotifications.empty.rules')}
              fitScreen
              loading={rulesQuery.isLoading}
              onPageChange={setPageIndex}
              onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
              pagination={{ current: pageIndex, pageSize, total: rulesQuery.data?.data.total ?? 0 }}
              rowActions={(row) => renderRuleActions(row, setRuleForm, setModal, confirm, deleteRuleMutation.mutateAsync, refresh, message, translate)}
              rowKey={(row) => row.id}
              rows={rulesQuery.data?.data.items ?? []}
            />
          ) : null}
          {tab === 'tasks' ? (
            <DataTable
              columnSettingsKey="workflow-notification-tasks"
              columns={taskColumns}
              emptyText={tasksQuery.isError ? translate('page.workflowNotifications.empty.loadFailed') : translate('page.workflowNotifications.empty.tasks')}
              fitScreen
              loading={tasksQuery.isLoading}
              onPageChange={setPageIndex}
              onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
              pagination={{ current: pageIndex, pageSize, total: tasksQuery.data?.data.total ?? 0 }}
              rowActions={(row) => row.processInstanceId ? <button className="hover:text-primary-600" title={translate('page.workflowNotifications.action.traceProcess')} type="button" onClick={() => navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="git-branch" /></button> : null}
              rowKey={(row) => row.id}
              rows={tasksQuery.data?.data.items ?? []}
            />
          ) : null}
          {tab === 'logs' ? (
            <DataTable
              columnSettingsKey="workflow-notification-logs"
              columns={logColumns}
              emptyText={logsQuery.isError ? translate('page.workflowNotifications.empty.loadFailed') : translate('page.workflowNotifications.empty.logs')}
              fitScreen
              loading={logsQuery.isLoading}
              onPageChange={setPageIndex}
              onPageSizeChange={(next) => { setPageSize(next); setPageIndex(1); }}
              pagination={{ current: pageIndex, pageSize, total: logsQuery.data?.data.total ?? 0 }}
              rowActions={(row) => row.processInstanceId ? <button className="hover:text-primary-600" title={translate('page.workflowNotifications.action.traceProcess')} type="button" onClick={() => navigate(`/workflows/instances/${row.processInstanceId}`)}><AppIcon className="text-base" name="git-branch" /></button> : null}
              rowKey={(row) => row.id}
              rows={logsQuery.data?.data.items ?? []}
            />
          ) : null}
        </div>
      </div>

      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => setModal(null) },
          { label: translate('common.save'), loading: saveChannelMutation.isPending, onClick: () => void submit(), variant: 'primary' }
        ]}
        fields={channelFields}
        open={modal === 'channel'}
        title={channelModalTitle}
        value={channelForm}
        onClose={() => setModal(null)}
        onValueChange={(name, value) => setChannelForm((current) => ({ ...current, [name]: value }))}
      />
      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => setModal(null) },
          { label: translate('common.save'), loading: saveTemplateMutation.isPending, onClick: () => void submit(), variant: 'primary' }
        ]}
        fields={templateFields}
        open={modal === 'template'}
        title={templateModalTitle}
        value={templateForm}
        onClose={() => setModal(null)}
        onValueChange={(name, value) => setTemplateForm((current) => ({ ...current, [name]: value }))}
      />
      <ModalForm
        actions={[
          { label: translate('common.cancel'), onClick: () => setModal(null) },
          { label: translate('common.save'), loading: saveRuleMutation.isPending, onClick: () => void submit(), variant: 'primary' }
        ]}
        fields={ruleFields}
        open={modal === 'rule'}
        title={ruleModalTitle}
        value={ruleForm}
        onClose={() => setModal(null)}
        onValueChange={(name, value) => setRuleForm((current) => ({ ...current, [name]: value }))}
      />
    </CrudPage>
  );
}
