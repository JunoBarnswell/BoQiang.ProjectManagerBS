import type {
  WorkflowMessageTemplateDto,
  WorkflowNodeNotificationRuleDto,
  WorkflowNotificationChannelDto,
  WorkflowNotificationLogDto,
  WorkflowNotificationTaskDto
} from '../../api/workflow/workflows.api';
import type { DataTableColumn } from '../../shared/table/tableTypes';

import {
  channelTypeLabel,
  failurePolicyLabel,
  formatDateTime,
  notificationLogResultLabel,
  notificationTaskStatusLabel,
  normalizeStatus,
  receiverLabel,
  triggerLabel
} from './workflowNotificationOptions';
import type { NotificationTab, TranslateFn } from './workflowNotificationsTypes';

export function createNotificationTabs(translate: TranslateFn): Array<{ description: string; icon: string; label: string; value: NotificationTab }> {
  return [
    { description: translate('page.workflowNotifications.tab.tasks.description'), icon: 'bell-ringing', label: translate('page.workflowNotifications.tab.tasks.label'), value: 'tasks' },
    { description: translate('page.workflowNotifications.tab.logs.description'), icon: 'scroll', label: translate('page.workflowNotifications.tab.logs.label'), value: 'logs' },
    { description: translate('page.workflowNotifications.tab.rules.description'), icon: 'git-branch', label: translate('page.workflowNotifications.tab.rules.label'), value: 'rules' },
    { description: translate('page.workflowNotifications.tab.templates.description'), icon: 'brackets-curly', label: translate('page.workflowNotifications.tab.templates.label'), value: 'templates' },
    { description: translate('page.workflowNotifications.tab.channels.description'), icon: 'plugs-connected', label: translate('page.workflowNotifications.tab.channels.label'), value: 'channels' }
  ];
}

export function createChannelColumns(translate: TranslateFn): DataTableColumn<WorkflowNotificationChannelDto>[] {
  return [
    { key: 'channelCode', title: translate('page.workflowNotifications.column.channelCode'), width: '170px', responsivePriority: 100 },
    { key: 'channelName', title: translate('page.workflowNotifications.column.channelName'), width: '160px', responsivePriority: 95 },
    { key: 'channelType', title: translate('page.workflowNotifications.column.channelType'), width: '110px', responsivePriority: 90, render: (row) => channelTypeLabel(row.channelType, translate) },
    { key: 'isEnabled', title: translate('page.workflowNotifications.column.status'), width: '90px', render: (row) => row.isEnabled ? translate('common.enabled') : translate('common.disabled') },
    { key: 'failurePolicy', title: translate('page.workflowNotifications.column.failurePolicy'), width: '110px', hideBelow: 'lg', render: (row) => failurePolicyLabel(row.failurePolicy, translate) },
    { key: 'updatedTime', title: translate('page.workflowNotifications.column.updatedTime'), width: '180px', hideBelow: 'xl', render: (row) => formatDateTime(row.updatedTime ?? row.createdTime) }
  ];
}

export function createTemplateColumns(translate: TranslateFn): DataTableColumn<WorkflowMessageTemplateDto>[] {
  return [
    { key: 'templateCode', title: translate('page.workflowNotifications.column.templateCode'), width: '210px', responsivePriority: 100 },
    { key: 'templateName', title: translate('page.workflowNotifications.column.templateName'), width: '170px', responsivePriority: 95 },
    { key: 'channelType', title: translate('page.workflowNotifications.column.channelType'), width: '110px', responsivePriority: 90, render: (row) => channelTypeLabel(row.channelType, translate) },
    { key: 'subjectTemplate', title: translate('page.workflowNotifications.column.subjectTemplate'), width: '240px', hideBelow: 'lg', render: (row) => row.subjectTemplate ?? '-' },
    { key: 'isEnabled', title: translate('page.workflowNotifications.column.status'), width: '90px', render: (row) => row.isEnabled ? translate('common.enabled') : translate('common.disabled') }
  ];
}

export function createRuleColumns(translate: TranslateFn): DataTableColumn<WorkflowNodeNotificationRuleDto>[] {
  return [
    { key: 'nodeId', title: translate('page.workflowNotifications.column.nodeId'), width: '160px', responsivePriority: 100 },
    { key: 'trigger', title: translate('page.workflowNotifications.column.trigger'), width: '130px', responsivePriority: 95, render: (row) => triggerLabel(row.trigger, translate) },
    { key: 'receiverType', title: translate('page.workflowNotifications.column.receiverType'), width: '160px', responsivePriority: 90, render: (row) => `${receiverLabel(row.receiverType, translate)} ${row.receiverValue ?? ''}`.trim() },
    { key: 'templateCode', title: translate('page.workflowNotifications.column.templateCode'), width: '190px', hideBelow: 'lg' },
    { key: 'isEnabled', title: translate('page.workflowNotifications.column.status'), width: '90px', render: (row) => row.isEnabled ? translate('common.enabled') : translate('common.disabled') },
    { key: 'processDefinitionKey', title: translate('page.workflowNotifications.column.processDefinitionKey'), width: '180px', hideBelow: 'xl', render: (row) => row.processDefinitionKey ?? '-' }
  ];
}

export function createTaskColumns(translate: TranslateFn): DataTableColumn<WorkflowNotificationTaskDto>[] {
  return [
    { key: 'content', title: translate('page.workflowNotifications.column.notificationContent'), width: '320px', responsivePriority: 100, render: (row) => renderTaskContent(row) },
    { key: 'status', title: translate('page.workflowNotifications.column.taskStatus'), width: '110px', responsivePriority: 95, render: (row) => renderTaskStatus(row, translate) },
    { key: 'receiverUserId', title: translate('page.workflowNotifications.column.receiver'), width: '140px', responsivePriority: 90, render: (row) => row.receiverUserId ?? row.receiverAddress ?? '-' },
    { key: 'channelCode', title: translate('page.workflowNotifications.column.channel'), width: '100px', hideBelow: 'lg', render: (row) => channelTypeLabel(row.channelCode, translate) },
    { key: 'trigger', title: translate('page.workflowNotifications.column.trigger'), width: '130px', hideBelow: 'lg', render: (row) => triggerLabel(row.trigger ?? '', translate) },
    { key: 'dueAt', title: translate('page.workflowNotifications.column.scheduleTime'), width: '190px', hideBelow: 'xl', render: (row) => `${formatDateTime(row.dueAt)} / ${formatDateTime(row.sentAt)}` }
  ];
}

export function createLogColumns(translate: TranslateFn): DataTableColumn<WorkflowNotificationLogDto>[] {
  return [
    { key: 'eventName', title: translate('page.workflowNotifications.column.eventName'), width: '130px', responsivePriority: 100 },
    { key: 'result', title: translate('page.workflowNotifications.column.result'), width: '100px', responsivePriority: 95, render: (row) => renderLogResult(row, translate) },
    { key: 'message', title: translate('page.workflowNotifications.column.message'), width: '300px', responsivePriority: 90, render: (row) => row.errorMessage ?? row.message ?? '-' },
    { key: 'receiverUserId', title: translate('page.workflowNotifications.column.receiver'), width: '140px', hideBelow: 'lg', render: (row) => row.receiverUserId ?? '-' },
    { key: 'channelCode', title: translate('page.workflowNotifications.column.channel'), width: '100px', hideBelow: 'lg', render: (row) => channelTypeLabel(row.channelCode ?? '', translate) },
    { key: 'provider', title: translate('page.workflowNotifications.column.provider'), width: '140px', hideBelow: 'lg', render: (row) => row.provider ?? '-' },
    { key: 'traceId', title: translate('page.workflowNotifications.column.traceId'), width: '180px', hideBelow: 'xl', render: (row) => row.traceId ?? '-' },
    { key: 'createdTime', title: translate('page.workflowNotifications.column.createdTime'), width: '180px', hideBelow: 'xl', render: (row) => formatDateTime(row.createdTime) }
  ];
}

function renderTaskContent(row: WorkflowNotificationTaskDto) {
  return (
    <>
      <div className="font-medium text-gray-900">{row.subject ?? row.templateCode ?? row.id}</div>
      <div className="text-xs text-gray-500 line-clamp-2">{row.content}</div>
    </>
  );
}

function renderTaskStatus(row: WorkflowNotificationTaskDto, translate: TranslateFn) {
  const status = normalizeStatus(row.status);
  const tone = status === 'failed' ? 'text-red-600' : status === 'sent' ? 'text-green-700' : status === 'retrying' ? 'text-amber-700' : 'text-gray-700';
  return <span className={tone}>{notificationTaskStatusLabel(row.status, translate)}</span>;
}

function renderLogResult(row: WorkflowNotificationLogDto, translate: TranslateFn) {
  const result = normalizeStatus(row.result);
  const tone = result === 'failed' ? 'text-red-600' : result === 'success' ? 'text-green-700' : 'text-gray-700';
  return <span className={tone}>{notificationLogResultLabel(row.result, translate)}</span>;
}
