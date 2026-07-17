import type {
  WorkflowMessageTemplateUpsertRequest,
  WorkflowNotificationChannelUpsertRequest
} from '../../api/workflow/workflows.api';
import type { FormFieldConfig } from '../../shared/forms/formTypes';

import {
  channelTypeOptions,
  failurePolicyOptions,
  receiverTypeOptions,
  triggerOptions
} from './workflowNotificationOptions';
import type { TranslateFn, WorkflowNodeNotificationRuleForm } from './workflowNotificationsTypes';

export function createChannelFields(translate: TranslateFn): FormFieldConfig<WorkflowNotificationChannelUpsertRequest>[] {
  return [
    { label: translate('page.workflowNotifications.field.appCode'), name: 'appCode', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.channelCode'), name: 'channelCode', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.channelName'), name: 'channelName', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.channelType'), name: 'channelType', options: channelTypeOptions(translate), required: true, span: 1, type: 'select' },
    { label: translate('page.workflowNotifications.field.isEnabled'), name: 'isEnabled', span: 1, type: 'switch' },
    { label: translate('page.workflowNotifications.field.failurePolicy'), name: 'failurePolicy', options: failurePolicyOptions(translate), span: 1, type: 'select' },
    { label: translate('page.workflowNotifications.field.configJson'), name: 'configJson', rows: 5, span: 2, type: 'textarea' }
  ];
}

export function createTemplateFields(translate: TranslateFn): FormFieldConfig<WorkflowMessageTemplateUpsertRequest>[] {
  return [
    { label: translate('page.workflowNotifications.field.appCode'), name: 'appCode', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.templateCode'), name: 'templateCode', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.templateName'), name: 'templateName', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.channelType'), name: 'channelType', options: channelTypeOptions(translate), required: true, span: 1, type: 'select' },
    { label: translate('page.workflowNotifications.field.isEnabled'), name: 'isEnabled', span: 1, type: 'switch' },
    { label: translate('page.workflowNotifications.field.subjectTemplate'), name: 'subjectTemplate', span: 2, type: 'text' },
    { label: translate('page.workflowNotifications.field.bodyTemplate'), name: 'bodyTemplate', required: true, rows: 6, span: 2, type: 'textarea' },
    { label: translate('page.workflowNotifications.field.variablesJson'), name: 'variablesJson', rows: 4, span: 2, type: 'textarea' }
  ];
}

export function createRuleFields(translate: TranslateFn): FormFieldConfig<WorkflowNodeNotificationRuleForm>[] {
  return [
    { label: translate('page.workflowNotifications.field.appCode'), name: 'appCode', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.processDefinitionKey'), name: 'processDefinitionKey', span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.nodeId'), name: 'nodeId', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.trigger'), name: 'trigger', options: triggerOptions(translate), required: true, span: 1, type: 'select' },
    { label: translate('page.workflowNotifications.field.receiverType'), name: 'receiverType', options: receiverTypeOptions(translate), required: true, span: 1, type: 'select' },
    { label: translate('page.workflowNotifications.field.receiverValue'), name: 'receiverValue', span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.templateCode'), name: 'templateCode', required: true, span: 1, type: 'text' },
    { label: translate('page.workflowNotifications.field.failurePolicy'), name: 'failurePolicy', options: failurePolicyOptions(translate), span: 1, type: 'select' },
    { label: translate('page.workflowNotifications.field.isEnabled'), name: 'isEnabled', span: 1, type: 'switch' },
    { label: translate('page.workflowNotifications.field.channelCodesText'), name: 'channelCodesText', rows: 3, span: 2, type: 'textarea' },
    { label: translate('page.workflowNotifications.field.conditionJson'), name: 'conditionJson', rows: 4, span: 2, type: 'textarea' }
  ];
}

export function createChannelDraft(translate: TranslateFn, appCode: string, tenantId: string): WorkflowNotificationChannelUpsertRequest {
  return {
    appCode,
    channelCode: 'in-app',
    channelName: translate('page.workflowNotifications.default.channelName'),
    channelType: 'in-app',
    configJson: '{"delivery":"signalr"}',
    failurePolicy: 'ignore',
    isEnabled: true,
    tenantId
  };
}

export function createTemplateDraft(translate: TranslateFn, appCode: string, tenantId: string): WorkflowMessageTemplateUpsertRequest {
  return {
    appCode,
    bodyTemplate: translate('page.workflowNotifications.default.templateBody'),
    channelType: 'in-app',
    isEnabled: true,
    subjectTemplate: translate('page.workflowNotifications.default.templateSubject'),
    templateCode: 'workflow-node-enter',
    templateName: translate('page.workflowNotifications.default.templateName'),
    tenantId,
    variablesJson: '["processName","businessType","businessKey","nodeName"]'
  };
}

export function createRuleDraft(appCode: string, tenantId: string): WorkflowNodeNotificationRuleForm {
  return {
    appCode,
    channelCodes: ['in-app'],
    channelCodesText: '["in-app"]',
    conditionJson: '',
    failurePolicy: 'ignore',
    isEnabled: true,
    nodeId: '',
    processDefinitionKey: '',
    receiverType: 'approver',
    receiverValue: '',
    tenantId,
    templateCode: 'workflow-node-enter',
    trigger: 'node-enter'
  };
}
