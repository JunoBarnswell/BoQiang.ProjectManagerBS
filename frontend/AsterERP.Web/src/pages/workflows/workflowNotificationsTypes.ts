import type { WorkflowNodeNotificationRuleUpsertRequest } from '../../api/workflow/workflows.api';

export type TranslateFn = (key: string) => string;

export type NotificationTab = 'tasks' | 'logs' | 'rules' | 'templates' | 'channels';

export type ConfigModal = 'channel' | 'template' | 'rule';

export type WorkflowNodeNotificationRuleForm = WorkflowNodeNotificationRuleUpsertRequest & { channelCodesText: string };
